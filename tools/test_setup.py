"""Original synthetic fixtures only; no game ISO or game data needed."""
import hashlib
import io
from pathlib import Path
import struct
import tempfile
import unittest
from unittest.mock import patch
import zipfile
import sys
sys.path.insert(0, str(Path(__file__).resolve().parent))
from inspect_disc import Disc, MAGIC, SECTOR
import setup_game


class SetupTests(unittest.TestCase):
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


if __name__ == "__main__":
    unittest.main()
