"""Portable build-folder boundaries and save preservation using synthetic files."""
import hashlib
import json
import os
from pathlib import Path
import sys
import subprocess
import tempfile
import unittest
import uuid
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parent))
import install_location as location
import prepare_spatial_sdk as spatial
from package_release import FILES
from validate_package import RENDERER_FILES


class InstallLocationTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory(prefix="ea-location-")
        self.parent = Path(self.directory.name)
        self.directory.name = str(location.filesystem_path(self.parent))
        self.addCleanup(self.directory.cleanup)
        self.source = self.parent / "Launcher source"
        self.target = self.parent / "Games with spaces" / "Enchanted Arms PC"
        self.source.mkdir()
        for name in FILES:
            self.write(self.source / name, b"public fixture")
        self.write(self.source / "EnchantedArmsLauncher.exe", b"MZ-synthetic-launcher")
        bootstrap = b"synthetic bundled Python"
        self.write(self.source / "bootstrap/python-fixture.zip", bootstrap)
        self.write_json(self.source / "launcher/python.lock.json", {
            "name": "python-fixture.zip", "sha256": hashlib.sha256(bootstrap).hexdigest()})
        renderer = {}
        for name in RENDERER_FILES:
            data = ("renderer " + name).encode()
            self.write(self.source / "renderer" / name, data)
            renderer[name] = hashlib.sha256(data).hexdigest()
        self.write_json(self.source / "renderer/renderer-capabilities.json", {"files": renderer})
        self.write_json(self.source / ".local/launcher.json", {"BuildLocation": str(self.target), "FrameLimit": 60})
        self.save = Path(".local/runtime/user/content/save.bin")
        self.write(self.source / self.save, b"original saved progress")

    @staticmethod
    def write(path, data):
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)

    def write_json(self, path, value):
        self.write(path, json.dumps(value).encode())

    def test_separate_folder_keeps_public_kit_preferences_and_saves_together(self):
        self.write(self.source / ".local/runtime/cache/shader.bin", b"private path-dependent cache")
        self.write(self.source / "game.iso", b"original disc")
        self.write(self.source / "bin/graphics-0.3.5/enchantedarmsrecomp.exe", b"old compiled game")
        self.write(self.source / "generated/default/private.cpp", b"private translated code")
        location.prepare(self.source, self.target)
        self.assertEqual((self.target / self.save).read_bytes(), b"original saved progress")
        self.assertEqual((self.source / self.save).read_bytes(), b"original saved progress")
        settings = json.loads((self.target / ".local/launcher.json").read_text())
        self.assertEqual(settings["BuildLocation"], "")
        self.assertEqual(settings["FrameLimit"], 60)
        self.assertTrue((self.target / "EnchantedArmsLauncher.exe").is_file())
        self.assertFalse((self.target / "game.iso").exists())
        self.assertFalse((self.target / "bin").exists())
        self.assertFalse((self.target / "generated/default").exists())
        self.assertFalse((self.target / ".local/runtime/cache").exists())
        self.assertFalse((self.target / ".local/installed-location.json").exists())

    def test_retry_refreshes_public_files_without_overwriting_destination_progress(self):
        location.prepare(self.source, self.target)
        self.write(self.source / "README.md", b"updated public file")
        self.write(self.target / self.save, b"newer destination progress")
        self.write(self.source / self.save, b"different source progress")
        location.prepare(self.source, self.target)
        self.assertEqual((self.target / "README.md").read_bytes(), b"updated public file")
        self.assertEqual((self.target / self.save).read_bytes(), b"newer destination progress")

    def test_unrelated_nonempty_folder_is_untouched(self):
        sentinel = self.target / "unrelated.txt"
        self.write(sentinel, b"keep")
        with self.assertRaisesRegex(ValueError, "empty folder"):
            location.prepare(self.source, self.target)
        self.assertEqual(list(self.target.iterdir()), [sentinel])
        self.assertEqual(sentinel.read_bytes(), b"keep")

    def test_nested_parent_and_drive_roots_are_rejected(self):
        for target in (self.source / "nested", self.parent, Path(self.source.anchor)):
            with self.subTest(target=target), self.assertRaises(ValueError):
                location.prepare(self.source, target)

    def test_other_installation_cannot_claim_an_owned_folder(self):
        location.prepare(self.source, self.target)
        self.write_json(self.source / location.IDENTITY, {"id": str(uuid.uuid4())})
        with self.assertRaisesRegex(ValueError, "empty folder"):
            location.prepare(self.source, self.target)
        self.assertEqual((self.target / self.save).read_bytes(), b"original saved progress")

    def test_incomplete_kit_does_not_create_destination(self):
        (self.source / "EnchantedArmsLauncher.exe").unlink()
        with self.assertRaisesRegex(ValueError, "incomplete"):
            location.prepare(self.source, self.target)
        self.assertFalse(self.target.exists())

    def test_unsupported_toolchain_path_is_rejected_before_copying(self):
        target = self.parent / "Enchanted Arms ü"
        with self.assertRaisesRegex(ValueError, "without accented"):
            location.prepare(self.source, target)
        self.assertFalse(target.exists())

    def test_in_place_build_does_not_copy_or_change_files(self):
        before = {p.relative_to(self.source): p.read_bytes() for p in self.source.rglob("*") if p.is_file()}
        self.assertEqual(location.prepare(self.source, self.source), self.source)
        after = {p.relative_to(self.source): p.read_bytes() for p in self.source.rglob("*") if p.is_file()}
        self.assertEqual(before, after)

    def test_linked_destination_and_local_metadata_are_rejected(self):
        outside = self.parent / "External files"
        outside.mkdir()
        self.target.mkdir(parents=True)
        try:
            (self.target / ".local").symlink_to(outside, target_is_directory=True)
        except OSError as error:
            if os.name != "nt":
                raise
            # Directory junctions exercise the same linked-parent boundary on
            # Windows systems without the symlink creation privilege.
            environment = dict(os.environ)
            environment.pop("PSModulePath", None)
            quote = lambda path: "'" + str(path).replace("'", "''") + "'"
            result = subprocess.run([str(Path(os.environ["SystemRoot"]) / "System32/WindowsPowerShell/v1.0/powershell.exe"),
                "-NoProfile", "-Command", "New-Item -ItemType Junction -Path " + quote(self.target / ".local") +
                " -Target " + quote(outside) + " -ErrorAction Stop | Out-Null"],
                env=environment, capture_output=True)
            self.assertEqual(result.returncode, 0, "Could not create a junction fixture: " + repr(result.stderr))
        with self.assertRaises(ValueError):
            location.prepare(self.source, self.target)
        self.assertEqual(list(outside.iterdir()), [])

    def test_spatial_sdk_overlay_supports_deep_chosen_folders(self):
        manifest = json.loads((self.source / "renderer/renderer-capabilities.json").read_text())
        manifest.update(sdk_commit="f5337cdc947ff6d4c4196737e2c807a48f2a1fc2", spatial_fsr=True, texture_detail=True)
        self.write_json(self.source / "renderer/renderer-capabilities.json", manifest)
        original = self.source / ".local/toolchain/rexglue-setup-0.10.0-1/win-amd64"
        header = Path("include") / ("deep" * 15) / ("header" * 10) / "single_threaded_claim_strategy.hpp"
        path = location.filesystem_path(original / header)
        self.write(path, b"deep synthetic SDK header")
        with patch.object(spatial, "PROJECT", self.source):
            destination = spatial.prepare()
        self.assertEqual(location.filesystem_path(destination / header).read_bytes(), b"deep synthetic SDK header")


if __name__ == "__main__":
    unittest.main()
