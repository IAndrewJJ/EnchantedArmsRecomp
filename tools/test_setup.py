"""Original synthetic fixtures only; no game ISO or game data needed."""
import hashlib
import io
import json
from pathlib import Path
import struct
import subprocess
import tempfile
import unittest
import urllib.error
from unittest.mock import patch
import zipfile
import sys
sys.path.insert(0, str(Path(__file__).resolve().parent))
from inspect_disc import Disc, MAGIC, SECTOR, stream_digest
import setup_game


class SetupTests(unittest.TestCase):
    def test_python_310_stream_hash_fallback(self):
        import inspect_disc
        available = hasattr(hashlib, 'file_digest')
        original = getattr(hashlib, 'file_digest', None)
        try:
            if available:
                del inspect_disc.hashlib.file_digest
            self.assertEqual(stream_digest(io.BytesIO(b'portable hashing')).hexdigest(),
                             hashlib.sha256(b'portable hashing').hexdigest())
        finally:
            if available:
                inspect_disc.hashlib.file_digest = original

    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)

    def tearDown(self):
        self.temporary.cleanup()

    def disc(self, name="default.xex", payload=b"synthetic fixture", cycle=False):
        image = bytearray(48 * SECTOR)
        descriptor = 32 * SECTOR
        image[descriptor:descriptor + 20] = MAGIC
        image[descriptor + SECTOR - 20:descriptor + SECTOR] = MAGIC
        struct.pack_into("<II", image, descriptor + 20, 40, SECTOR)
        struct.pack_into("<HHIIBB", image, 40 * SECTOR, 0, 1 if cycle else 0, 44, len(payload), 0, len(name))
        image[40 * SECTOR + 14:40 * SECTOR + 14 + len(name)] = name.encode()
        image[44 * SECTOR:44 * SECTOR + len(payload)] = payload
        path = self.root / "sample.iso"
        path.write_bytes(image)
        return Disc(path)

    def test_extract_resumes_partial_and_preserves_iso(self):
        disc = self.disc()
        before = setup_game.digest(disc.path)
        destination = self.root / "game"
        destination.mkdir()
        (destination / "default.xex.ea-extract-partial").write_bytes(b"interrupted")
        try:
            entry = disc.inventory()[0]
            first = disc.extract(entry, destination)
            second = disc.extract(entry, destination)
            self.assertEqual(first["sha256"], second["sha256"])
            self.assertTrue(second["existing_file_verified"])
            self.assertFalse((destination / "default.xex.ea-extract-partial").exists())
            self.assertEqual(before, setup_game.digest(disc.path))
        finally:
            disc.stream.close()

    def test_existing_conflicting_file_is_not_overwritten(self):
        disc = self.disc()
        destination = self.root / "game"
        destination.mkdir()
        (destination / "default.xex").write_bytes(b"keep this")
        try:
            with self.assertRaisesRegex(ValueError, "refusing to overwrite"):
                disc.extract(disc.inventory()[0], destination)
            self.assertEqual((destination / "default.xex").read_bytes(), b"keep this")
        finally:
            disc.stream.close()

    def test_unsupported_revision_rejected_before_extraction(self):
        disc = self.disc()
        try:
            with self.assertRaisesRegex(ValueError, "Unsupported game revision"):
                setup_game.validate_disc(disc)
            self.assertFalse((self.root / "game").exists())
        finally:
            disc.stream.close()

    def test_unsafe_disc_names_rejected(self):
        for name in ("../escape", "CON.txt", "file.", "folder:stream"):
            disc = self.disc(name=name)
            try:
                with self.assertRaises(ValueError):
                    disc.inventory()
            finally:
                disc.stream.close()

    def test_read_outside_disc_rejected(self):
        disc = self.disc()
        try:
            with self.assertRaises(ValueError): disc.read(disc.size - 1, 2)
        finally:
            disc.stream.close()

    def test_zip_traversal_checked_before_any_extraction(self):
        for name in ("../escape.txt", "C:/escape.txt", "dir/../../escape", "..\\escape"):
            archive = self.root / "bad.zip"
            with zipfile.ZipFile(archive, "w") as z:
                z.writestr("good.txt", "good")
                z.writestr(name, "bad")
            with self.assertRaises(ValueError): setup_game.unpack_zip(archive, self.root / "out")
            self.assertFalse((self.root / "out/good.txt").exists())

    def test_vsix_contents_extraction(self):
        archive = self.root / "tool.vsix"
        with zipfile.ZipFile(archive, "w") as z:
            z.writestr("Contents/include/test.h", "fixture")
            z.writestr("metadata.xml", "metadata")
        setup_game.unpack_zip(archive, self.root / "out", True)
        self.assertEqual((self.root / "out/include/test.h").read_text(), "fixture")
        self.assertFalse((self.root / "out/metadata.xml").exists())

    def test_deep_sdk_members_extract_without_system_long_path_setting(self):
        archive = self.root / "sdk.zip"
        member = "/".join(["SDK headers " + "a" * 70] * 3 + ["Windows.Networking.NetworkOperators.Contract.winmd"])
        with zipfile.ZipFile(archive, "w") as source:
            source.writestr(member, "synthetic SDK member")
        destination = self.root / "project with spaces/SDK"
        self.assertGreater(len(str(destination / member)), 260)
        target = setup_game.filesystem_path(destination / member)
        try:
            setup_game.unpack_zip(archive, destination)
            self.assertEqual(target.read_text(), "synthetic SDK member")
        finally:
            # tempfile's cleanup also lacks extended paths in the embedded interpreter.
            # Remove only this fixture's exact file/empty directories before normal cleanup.
            target.unlink(missing_ok=True)
            for parent in list(target.parents)[:3]:
                if parent.exists():
                    parent.rmdir()

    def test_bad_download_never_replaces_cached_file(self):
        target = self.root / "tool.zip"
        target.write_bytes(b"old cache")
        package = dict(name=target.name,url="https://example.invalid/tool.zip",algorithm="sha256",digest=hashlib.sha256(b"expected").hexdigest(),bytes=8)
        with patch("urllib.request.urlopen", return_value=io.BytesIO(b"tampered")):
            with self.assertRaisesRegex(ValueError, "verification failed"):
                setup_game.download(package, self.root)
        self.assertEqual(target.read_bytes(), b"old cache")

    def test_verified_cache_needs_no_network(self):
        target = self.root / "tool.zip"
        target.write_bytes(b"verified")
        package = dict(name=target.name,algorithm="sha256",digest=setup_game.digest(target))
        with patch("urllib.request.urlopen", side_effect=AssertionError("network should not run")):
            self.assertEqual(setup_game.download(package, self.root), target)

    def package(self, payload=b"verified", **extra):
        return dict(name="tool.zip", url="https://example.invalid/tool.zip", algorithm="sha256",
                    digest=hashlib.sha256(payload).hexdigest(), bytes=len(payload), **extra)

    def test_invalid_argument_during_promotion_retries_without_redownloading(self):
        original = Path.replace
        calls = []

        def interrupted_replace(source, target):
            calls.append(target)
            if len(calls) < 3:
                raise OSError(22, "Invalid argument", str(target))
            return original(source, target)

        with patch("urllib.request.urlopen", return_value=io.BytesIO(b"verified")) as network, \
                patch.object(Path, "replace", interrupted_replace), patch("time.sleep") as sleep:
            target = setup_game.download(self.package(), self.root)
        self.assertEqual(target.read_bytes(), b"verified")
        self.assertEqual(network.call_count, 1)
        self.assertEqual(sleep.call_count, 2)

    def test_failed_promotion_keeps_verified_partial_for_offline_retry(self):
        target = self.root / "tool.zip"
        target.write_bytes(b"old cache")
        with patch("urllib.request.urlopen", return_value=io.BytesIO(b"verified")), \
                patch.object(Path, "replace", side_effect=OSError(22, "Invalid argument", str(target))), \
                patch("time.sleep"):
            with self.assertRaisesRegex(setup_game.SetupError, "finalize verified download.*errno=22"):
                setup_game.download(self.package(), self.root)
        self.assertEqual(target.read_bytes(), b"old cache")
        self.assertEqual((self.root / "tool.zip.partial").read_bytes(), b"verified")
        with patch("urllib.request.urlopen", side_effect=AssertionError("must recover offline")):
            self.assertEqual(setup_game.download(self.package(), self.root).read_bytes(), b"verified")

    def test_unverified_partial_is_downloaded_again(self):
        (self.root / "tool.zip.partial").write_bytes(b"tampered")
        with patch("urllib.request.urlopen", return_value=io.BytesIO(b"verified")) as network:
            self.assertEqual(setup_game.download(self.package(), self.root).read_bytes(), b"verified")
        self.assertEqual(network.call_count, 1)

    def test_interrupted_connection_retries(self):
        with patch("urllib.request.urlopen", side_effect=[ConnectionResetError("reset"), io.BytesIO(b"verified")]) as network, \
                patch("time.sleep"):
            self.assertEqual(setup_game.download(self.package(), self.root).read_bytes(), b"verified")
        self.assertEqual(network.call_count, 2)

    def test_http_404_is_not_retried(self):
        with patch("urllib.request.urlopen", side_effect=urllib.error.HTTPError("url", 404, "Not Found", {}, None)) as network:
            with self.assertRaisesRegex(setup_game.SetupError, "Could not download tool.zip"):
                setup_game.download(self.package(), self.root)
        self.assertEqual(network.call_count, 1)

    def test_persistent_cache_access_error_is_bounded_and_identifies_operation(self):
        target = self.root / "tool.zip"
        target.write_bytes(b"verified")
        with patch("setup_game.digest", side_effect=PermissionError(13, "Access denied")), \
                patch("time.sleep") as sleep, patch("urllib.request.urlopen") as network:
            with self.assertRaisesRegex(setup_game.SetupError, "verify cached download.*errno=13"):
                setup_game.download(self.package(), self.root)
        self.assertEqual(sleep.call_count, 3)
        network.assert_not_called()

    def test_security_block_hidden_by_invalid_argument_is_reported_without_retry(self):
        target = self.root / "tool.zip"
        target.write_bytes(b"verified")
        with patch("setup_game.digest", side_effect=OSError(22, "Invalid argument")), \
                patch("setup_game.windows_open_error", return_value=225), \
                patch("time.sleep") as sleep, patch("urllib.request.urlopen") as network:
            with self.assertRaisesRegex(setup_game.SetupError, "security software blocked.*winerror=225"):
                setup_game.download(self.package(), self.root)
        sleep.assert_not_called()
        network.assert_not_called()
        self.assertEqual(target.read_bytes(), b"verified")

    def test_explicit_security_error_never_probes_or_retries(self):
        error = OSError(22, "Security software removed this file")
        error.winerror = 226
        with patch("setup_game.windows_open_error") as probe, patch("time.sleep") as sleep:
            with self.assertRaisesRegex(setup_game.SetupError, "security software blocked.*winerror=226"):
                setup_game.file_operation("open file", self.root / "tool.zip", lambda: (_ for _ in ()).throw(error))
        probe.assert_not_called()
        sleep.assert_not_called()

    def test_incomplete_sdk_is_repaired_despite_existing_probe_and_receipt(self):
        archive = self.root / "fixture.zip"
        with zipfile.ZipFile(archive, "w") as source:
            source.writestr("bin/compiler.exe", "synthetic tool")
            source.writestr("include/test.h", "synthetic header")
        package = self.package(archive.read_bytes(), kind="zip", destination="sdk", probe="sdk/bin/compiler.exe",
                               required=["sdk/include/test.h"])
        local = self.root / "local"
        root = local / "toolchain"
        (root / "sdk/bin").mkdir(parents=True)
        (root / "sdk/bin/compiler.exe").write_bytes(b"incomplete old tool")
        (root / ".receipts").mkdir()
        (root / ".receipts/tool.zip.json").write_text(package["digest"])
        lock = self.root / "toolchain.lock.json"
        lock.write_text(json.dumps([package]))
        with patch.object(setup_game, "PROJECT", self.root), \
                patch("urllib.request.urlopen", return_value=io.BytesIO(archive.read_bytes())):
            setup_game.prepare_toolchain(local)
        self.assertEqual((root / "sdk/include/test.h").read_text(), "synthetic header")
        self.assertEqual((root / "sdk/bin/compiler.exe").read_text(), "synthetic tool")

    def test_failed_extraction_never_gets_completion_receipt(self):
        package = self.package(kind="zip", destination="sdk", probe="sdk/compiler.exe")
        with patch("setup_game.download", return_value=self.root / "tool.zip"), \
                patch("setup_game.unpack_zip", side_effect=OSError(28, "No space")):
            with self.assertRaisesRegex(setup_game.SetupError, "extract tool package.*errno=28"):
                setup_game.install_package(package, self.root / "toolchain")
        self.assertFalse((self.root / "toolchain/.receipts/tool.zip.json").exists())

    def test_missing_sdk_runtime_dll_has_actionable_error(self):
        (self.root / "compiler.exe").write_bytes(b"synthetic executable")
        package = dict(probe="compiler.exe", version="0.10.0")
        for code in (0xC0000135, -1073741515):
            with patch("subprocess.run", side_effect=subprocess.CalledProcessError(code, "compiler.exe")):
                with self.assertRaisesRegex(setup_game.SetupError, "Visual C\\+\\+.*x64 Redistributable"):
                    setup_game.package_ready(package, self.root)


if __name__ == "__main__":
    unittest.main()
