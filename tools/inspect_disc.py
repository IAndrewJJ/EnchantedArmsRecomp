"""Inspect XDVDFS without modifying the disc image; optionally extract files.

Format references: XboxDev/extract-xiso and Xenia's xex2_info.h.
This script only reads XEX headers; it does not decrypt or execute guest code.
"""

import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path, PurePosixPath
import struct

SECTOR = 2048
MAGIC = b"MICROSOFT*XBOX*MEDIA"


def stream_digest(stream, algorithm="sha256"):
    """Hash a stream on both the Windows and Ubuntu 22.04 Python runtimes."""
    if hasattr(hashlib, "file_digest"):
        return hashlib.file_digest(stream, algorithm)
    digest = hashlib.new(algorithm)
    for block in iter(lambda: stream.read(1024 * 1024), b""):
        digest.update(block)
    return digest


class Disc:
    def __init__(self, path):
        self.path = Path(path).resolve(strict=True)
        self.stream = self.path.open("rb")
        self.size = self.path.stat().st_size
        candidates = (0, 0xFD90000, 0x2080000, 0x18300000)
        self.base = None
        for base in candidates:
            if base + 33 * SECTOR > self.size:
                continue
            descriptor = self.read(base + 32 * SECTOR, SECTOR)
            if descriptor[:20] == MAGIC and descriptor[-20:] == MAGIC:
                self.base = base
                self.root_sector, self.root_size = struct.unpack_from("<II", descriptor, 20)
                break
        if self.base is None:
            raise ValueError("No supported XDVDFS volume descriptor found")

    def read(self, offset, size):
        if offset < 0 or size < 0 or offset + size > self.size:
            raise ValueError(f"Read outside ISO: offset={offset}, size={size}")
        self.stream.seek(offset)
        result = self.stream.read(size)
        if len(result) != size:
            raise ValueError("Short read from ISO")
        return result

    def inventory(self):
        result = []
        pending = [(PurePosixPath(), self.root_sector, self.root_size)]
        visited_directories = set()
        seen_paths = set()
        while pending:
            parent, sector, size = pending.pop()
            identity = (sector, size)
            if identity in visited_directories:
                raise ValueError("Repeated directory extent")
            visited_directories.add(identity)
            if size > 64 * 1024 * 1024:
                raise ValueError("Unreasonably large directory table")
            table = self.read(self.base + sector * SECTOR, size)
            nodes = [0] if size else []
            visited_nodes = set()
            while nodes:
                offset = nodes.pop()
                if offset in visited_nodes:
                    raise ValueError("Cycle in directory tree")
                visited_nodes.add(offset)
                if offset + 14 > len(table):
                    raise ValueError("Directory node outside its table")
                left, right, start, length, flags, name_size = struct.unpack_from("<HHIIBB", table, offset)
                if not name_size or offset + 14 + name_size > len(table):
                    raise ValueError("Invalid directory filename")
                name = table[offset + 14:offset + 14 + name_size].decode("ascii", errors="strict")
                if (name in (".", "..") or any(c in name for c in '/\\:\0<>"|?*')
                        or name.endswith((" ", ".")) or any(ord(c) < 32 for c in name)
                        or name.split(".")[0].upper() in {"CON", "PRN", "AUX", "NUL", *[f"{p}{i}" for p in ("COM", "LPT") for i in range(1, 10)]}):
                    raise ValueError("Unsafe filename in ISO")
                path = (parent / name).as_posix()
                if path.casefold() in seen_paths:
                    raise ValueError("Duplicate case-insensitive filename")
                seen_paths.add(path.casefold())
                absolute_offset = self.base + start * SECTOR
                if absolute_offset < self.base or absolute_offset + length > self.size:
                    raise ValueError(f"File extent outside disc: {path}")
                directory = bool(flags & 0x10)
                result.append({"path": path, "directory": directory, "bytes": length,
                               "sector": start, "iso_offset": absolute_offset, "attributes": flags})
                if directory:
                    pending.append((PurePosixPath(path), start, length))
                for link in (right, left):
                    if link:
                        nodes.append(link * 4)
        return sorted(result, key=lambda entry: entry["path"].casefold())

    def extract(self, entry, destination):
        destination = Path(destination).resolve()
        target = destination.joinpath(*PurePosixPath(entry["path"]).parts).resolve()
        if not target.is_relative_to(destination):
            raise ValueError("Extraction target outside destination")
        if entry["directory"]:
            target.mkdir(parents=True, exist_ok=True)
            return None
        target.parent.mkdir(parents=True, exist_ok=True)
        if target.exists():
            digest = hashlib.sha256()
            self.stream.seek(entry["iso_offset"])
            remaining = entry["bytes"]
            while remaining:
                block = self.stream.read(min(8 * 1024 * 1024, remaining))
                if not block:
                    raise ValueError("Short source read while verifying existing file")
                digest.update(block)
                remaining -= len(block)
            with target.open("rb") as check:
                readback = stream_digest(check).hexdigest()
            if readback != digest.hexdigest():
                raise ValueError(f"Existing file differs from ISO; refusing to overwrite: {target}")
            return {"path": str(target), "bytes": entry["bytes"], "sha256": readback,
                    "existing_file_verified": True}
        digest = hashlib.sha256()
        self.stream.seek(entry["iso_offset"])
        remaining = entry["bytes"]
        # Exclusive creation prevents overwriting existing files.
        temporary = target.with_name(target.name + ".ea-extract-partial")
        with temporary.open("wb") as output:
            while remaining:
                data = self.stream.read(min(8 * 1024 * 1024, remaining))
                if not data:
                    raise ValueError("Short read while extracting")
                output.write(data)
                digest.update(data)
                remaining -= len(data)
        if temporary.stat().st_size != entry["bytes"]:
            raise ValueError("Extracted size mismatch")
        with temporary.open("rb") as check:
            readback = stream_digest(check).hexdigest()
        if readback != digest.hexdigest():
            raise ValueError("Extracted SHA-256 mismatch")
        # Windows rename refuses an existing destination. Never overwrite game data.
        temporary.rename(target)
        return {"path": str(target), "bytes": entry["bytes"], "sha256": readback}


def inspect_xex(data):
    if len(data) < 24 or data[:4] != b"XEX2":
        raise ValueError("Not an XEX2 executable")
    flags, pe_offset, reserved, security, count = struct.unpack_from(">5I", data, 4)
    if count > 4096 or 24 + count * 8 > len(data):
        raise ValueError("Invalid optional header count")
    headers = dict(struct.unpack_from(">II", data, 24 + i * 8) for i in range(count))
    result = {"magic": "XEX2", "module_flags": hex(flags), "pe_data_offset": pe_offset,
              "security_offset": security,
              "optional_headers": {f"0x{k:08X}": f"0x{v:08X}" for k, v in headers.items()}}
    if 0x10100 in headers:
        result["entry_point"] = f"0x{headers[0x10100]:08X}"
    if 0x10201 in headers:
        result["image_base"] = f"0x{headers[0x10201]:08X}"
    if 0x40006 in headers:
        at = headers[0x40006]
        media, version, base_version, title, platform, executable_type, disc, discs, savegame = struct.unpack_from(">IIIIBBBBI", data, at)
        def version_string(value):
            return f"{value >> 28}.{(value >> 24) & 15}.{(value >> 8) & 65535}.{value & 255}"
        result["execution_info"] = {"media_id": f"{media:08X}", "title_id": f"{title:08X}",
                                    "version": version_string(version), "base_version": version_string(base_version),
                                    "platform": platform, "executable_type": executable_type,
                                    "disc_number": disc, "disc_count": discs}
    if 0x3FF in headers:
        size, encryption, compression = struct.unpack_from(">IHH", data, headers[0x3FF])
        result["file_format"] = {"encryption": encryption, "compression": compression}
    if security + 0x184 <= len(data):
        result["image_size"] = struct.unpack_from(">I", data, security + 4)[0]
        result["region_mask"] = f"0x{struct.unpack_from('>I', data, security + 0x178)[0]:08X}"
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("iso", type=Path)
    parser.add_argument("--report", required=True, type=Path)
    parser.add_argument("--destination", type=Path)
    parser.add_argument("--extract-all", action="store_true")
    parser.add_argument("--extract", action="append", default=[])
    parser.add_argument("--hash-iso", action="store_true")
    args = parser.parse_args()
    if (args.extract_all or args.extract) and not args.destination:
        parser.error("Extraction requires --destination")
    disc = Disc(args.iso)
    before = disc.path.stat()
    entries = disc.inventory()
    files = [e for e in entries if not e["directory"]]
    report = {"source": str(disc.path), "iso_bytes": disc.size,
              "partition_offset": f"0x{disc.base:X}", "file_count": len(files),
              "directory_count": len(entries) - len(files), "file_bytes": sum(e["bytes"] for e in files),
              "extensions": dict(Counter(PurePosixPath(e["path"]).suffix.lower() or "(none)" for e in files)),
              "entries": entries, "extracted": [], "xex": {}}
    if args.hash_iso:
        disc.stream.seek(0)
        report["iso_sha256"] = stream_digest(disc.stream).hexdigest()
    for entry in files:
        if entry["path"].lower().endswith(".xex"):
            data = disc.read(entry["iso_offset"], min(entry["bytes"], 2 * 1024 * 1024))
            report["xex"][entry["path"]] = inspect_xex(data)
    requested = {name.casefold() for name in args.extract}
    available = {e["path"].casefold() for e in entries}
    if requested - available:
        raise ValueError(f"Requested files not present: {sorted(requested - available)}")
    for entry in entries:
        if args.extract_all or entry["path"].casefold() in requested:
            result = disc.extract(entry, args.destination)
            if result:
                report["extracted"].append(result)
                print(f"Extracted {entry['path']} ({entry['bytes']:,} bytes)", flush=True)
    after = disc.path.stat()
    if (before.st_size, before.st_mtime_ns) != (after.st_size, after.st_mtime_ns):
        raise ValueError("Source changed while reading")
    report["source_size_and_mtime_unchanged"] = True
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({key: report[key] for key in ["file_count", "directory_count", "file_bytes", "extensions", "xex"]}, indent=2))


if __name__ == "__main__":
    main()
