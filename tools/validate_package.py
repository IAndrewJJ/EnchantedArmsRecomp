"""Verify a Windows kit's member hashes and distribution boundaries without extracting it."""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import zipfile

PREFIX = "EnchantedArmsRecomp/"
PRIVATE_PARTS = {".local", ".git", "bin", "private", "assets", "audit", "__pycache__"}
RENDERER_FILES = {"rexruntimerd.dll", "rexruntimerd.lib", "rexgpu-xenosrd.dll", "rexgpu-xenosrd.lib", "TracyClientrd.dll"}


def validate(path):
    with zipfile.ZipFile(path) as archive:
        members = archive.infolist()
        names = [m.filename for m in members]
        if len({n.casefold() for n in names}) != len(names):
            raise ValueError("Duplicate archive member")
        if sum(m.file_size for m in members) > 256 * 1024 * 1024:
            raise ValueError("Unexpectedly large Windows kit")
        hashes = json.loads(archive.read(PREFIX + "SHA256SUMS.json"))
        if not isinstance(hashes, dict) or set(names) != {PREFIX + n for n in hashes} | {PREFIX + "SHA256SUMS.json"}:
            raise ValueError("Hash ledger does not describe exactly the archive contents")
        for name, expected in hashes.items():
            relative = PurePosixPath(name)
            if (not name or "\\" in name or relative.is_absolute() or relative.as_posix() != name or
                any(p in {"..", "."} or ":" in p for p in relative.parts)):
                raise ValueError("Unsafe archive path: " + name)
            parts = {p.casefold() for p in relative.parts}
            suffix = relative.suffix.casefold()
            if PRIVATE_PARTS & parts or name.casefold().startswith("generated/default/"):
                raise ValueError("Private path in Windows kit: " + name)
            if suffix in {".iso", ".xex", ".xexp", ".dmp", ".pdb", ".log"}:
                raise ValueError("Private file type in Windows kit: " + name)
            if suffix == ".exe" and name != "EnchantedArmsLauncher.exe":
                raise ValueError("Unexpected executable in Windows kit: " + name)
            if suffix in {".dll", ".lib"} and name not in {"renderer/" + n for n in RENDERER_FILES}:
                raise ValueError("Unexpected runtime binary: " + name)
            if hashlib.sha256(archive.read(PREFIX + name)).hexdigest() != expected:
                raise ValueError("Hash mismatch: " + name)
        return len(hashes)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("archive", type=Path)
    args = parser.parse_args()
    print(f"Verified {validate(args.archive)} files, hashes and distribution boundaries")


if __name__ == "__main__":
    main()
