"""Propose missing callback entries for manual review, without changing hints."""

import argparse
from collections import defaultdict
import json
from pathlib import Path
import struct
import sys


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("image", type=Path, help="Loaded image diagnostic from EA_RECOMP_DUMP_IMAGE")
    parser.add_argument("partitions", type=Path, help="ReXGlue codegen.partition.json")
    parser.add_argument("output", type=Path)
    parser.add_argument("--python-packages", type=Path)
    parser.add_argument("--image-base", type=lambda value: int(value, 0), default=0x82000000,
                        help="Guest load address; PE preferred base is not the XEX load base")
    args = parser.parse_args()
    if args.python_packages:
        sys.path.insert(0, str(args.python_packages.resolve()))
    from capstone import Cs, CS_ARCH_PPC, CS_MODE_32, CS_MODE_BIG_ENDIAN

    image = args.image.read_bytes()
    if image[:2] != b"MZ":
        raise ValueError("Expected a loaded PE image")
    pe_offset = struct.unpack_from("<I", image, 0x3C)[0]
    if image[pe_offset:pe_offset + 4] != b"PE\0\0":
        raise ValueError("Invalid PE signature")
    section_count = struct.unpack_from("<H", image, pe_offset + 6)[0]
    optional_size = struct.unpack_from("<H", image, pe_offset + 20)[0]
    image_base = args.image_base
    sections = []
    for index in range(section_count):
        at = pe_offset + 24 + optional_size + index * 40
        name = image[at:at + 8].rstrip(b"\0").decode("ascii")
        size, address = struct.unpack_from("<II", image, at + 8)
        flags = struct.unpack_from("<I", image, at + 36)[0]
        sections.append({"name": name, "offset": address, "size": size, "flags": flags})
    read_only_data = next(section for section in sections if section["name"] == ".rdata")
    executable_sections = [section for section in sections if section["flags"] & 0x20000000]
    known = {int(address, 16) for address in json.loads(args.partitions.read_text(encoding="utf-8"))["assignments"]}
    start = read_only_data["offset"]
    end = min(start + read_only_data["size"], len(image))
    references = defaultdict(list)
    for offset in range(start, end - 3, 4):
        value = struct.unpack_from(">I", image, offset)[0]
        if value % 4 or value in known:
            continue
        if not any(image_base + section["offset"] <= value < image_base + section["offset"] + section["size"]
                   for section in executable_sections):
            continue
        neighbors = [struct.unpack_from(">I", image, other)[0]
                     for other in range(max(start, offset - 16), min(end - 3, offset + 20), 4)
                     if other != offset]
        known_count = sum(neighbor in known for neighbor in neighbors)
        if known_count >= 2:
            references[value].append({"reference": f"0x{image_base + offset:08X}", "known_neighbors": known_count})
    disassembler = Cs(CS_ARCH_PPC, CS_MODE_32 | CS_MODE_BIG_ENDIAN)
    proposals = []
    for address, locations in sorted(references.items()):
        instructions = []
        for instruction in disassembler.disasm(image[address - image_base:address - image_base + 128], address):
            instructions.append(f"{instruction.address:08X} {instruction.mnemonic} {instruction.op_str}".rstrip())
            if instruction.mnemonic in ("blr", "bctr", "b"):
                break
        proposals.append({"address": f"0x{address:08X}", "references": locations, "instructions": instructions})
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(proposals, indent=2), encoding="utf-8")
    print(f"Wrote {len(proposals)} proposals to {args.output}. Review before adding any hint.")


if __name__ == "__main__":
    main()
