"""Synthetic distribution checks. No game or downloaded runtime is needed."""
import hashlib
import io
import json
from pathlib import Path
import sys
import tempfile
import unittest
import warnings
import zipfile

sys.path.insert(0, str(Path(__file__).resolve().parent))
from package_release import write_member
from validate_package import PREFIX, validate


class ReleaseTests(unittest.TestCase):
    def make_archive(self, path, files, extra=None):
        with zipfile.ZipFile(path, "w") as archive:
            hashes = {}
            for name, data in files.items():
                write_member(archive, PREFIX + name, data)
                hashes[name] = hashlib.sha256(data).hexdigest()
            write_member(archive, PREFIX + "SHA256SUMS.json", json.dumps(hashes).encode())
            if extra:
                with warnings.catch_warnings():
                    warnings.simplefilter("ignore", UserWarning)
                    write_member(archive, PREFIX + extra[0], extra[1])

    def test_valid_kit_and_reproducible_archive(self):
        a, b = io.BytesIO(), io.BytesIO()
        files = {"README.md": b"Example", "EnchantedArmsLauncher.exe": b"synthetic launcher"}
        self.make_archive(a, files)
        self.make_archive(b, files)
        self.assertEqual(a.getvalue(), b.getvalue())
        self.assertEqual(validate(a), 2)

    def test_unlisted_member_rejected(self):
        stream = io.BytesIO()
        self.make_archive(stream, {"README.md": b"Example"}, ("extra.txt", b"unlisted"))
        with self.assertRaisesRegex(ValueError, "ledger"):
            validate(stream)

    def test_duplicate_member_rejected(self):
        stream = io.BytesIO()
        self.make_archive(stream, {"README.md": b"Example"}, ("README.md", b"changed"))
        with self.assertRaisesRegex(ValueError, "Duplicate"):
            validate(stream)

    def test_private_and_unsafe_members_rejected_even_if_hashed(self):
        for name in ("../escape.txt", "C:/secret.txt", ".local/launcher.json", "bin/game.exe",
                     "generated/default/game.cpp", "game.xex", "game.iso", "game.dmp",
                     "enchantedarmsrecomp.exe", "other.dll"):
            with self.subTest(name=name):
                stream = io.BytesIO()
                self.make_archive(stream, {name: b"fixture"})
                with self.assertRaises(ValueError):
                    validate(stream)

    def test_modified_data_rejected(self):
        stream = io.BytesIO()
        with zipfile.ZipFile(stream, "w") as archive:
            write_member(archive, PREFIX + "README.md", b"changed")
            write_member(archive, PREFIX + "SHA256SUMS.json", json.dumps({
                "README.md": hashlib.sha256(b"original").hexdigest()}).encode())
        with self.assertRaisesRegex(ValueError, "Hash mismatch"):
            validate(stream)


if __name__ == "__main__":
    unittest.main()
