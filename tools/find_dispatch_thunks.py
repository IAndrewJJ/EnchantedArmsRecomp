"""Find standalone PPC virtual-call thunks for review, without changing hints."""

import argparse
import json
from pathlib import Path
import struct


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("image", type=Path, help="EA_RECOMP_DUMP_IMAGE loaded PE")
    parser.add_argument("partitions", type=Path, help="codegen.partition.json")
    parser.add_argument("output", type=Path)
    parser.add_argument("--image-base", type=lambda value: int(value, 0), default=0x82000000,
                        help="Guest load address; PE preferred base is not the XEX load base")
    args = parser.parse_args()
    image = args.image.read_bytes()
    if image[:2] != b"MZ":
        raise ValueError("Expected a loaded PE image")
    pe = struct.unpack_from("<I", image, 0x3C)[0]
    if image[pe:pe + 4] != b"PE\0\0":
        raise ValueError("Invalid PE signature")
    count = struct.unpack_from("<H", image, pe + 6)[0]
    optional_size = struct.unpack_from("<H", image, pe + 20)[0]
    base = args.image_base
    known = {int(x, 16) for x in json.loads(args.partitions.read_text())["assignments"]}
    proposals = []
    for index in range(count):
        section = pe + 24 + optional_size + index * 40
        size, start = struct.unpack_from("<II", image, section + 8)
        flags = struct.unpack_from("<I", image, section + 36)[0]
        if not flags & 0x20000000:
            continue
        end = min(start + size, len(image))
        for offset in range(max(start, 4), end - 15, 4):
            load_table, load_target, move_ctr, branch = struct.unpack_from(">IIII", image, offset)
            # lwz r11/r12,0(r3); lwz rN,slot(r11/r12); mtctr rN; bctr.
            if load_table not in (0x81630000, 0x81830000) or branch != 0x4E800420:
                continue
            table = (load_table >> 21) & 31
            target = (load_target >> 21) & 31
            if (load_target >> 26 != 32 or ((load_target >> 16) & 31) != table
                    or move_ctr != (0x7C0903A6 | (target << 21))):
                continue
            address = base + offset
            if address in known:
                continue
            previous = struct.unpack_from(">I", image, offset - 4)[0]
            # Exclude matching instruction sequences inside larger routines.
            boundary = (previous in (0, 0x60000000, 0x4E800020, 0x4E800420)
                        or (previous >> 26 == 18 and previous & 1 == 0))
            if not boundary:
                continue
            displacement = load_target & 0xFFFF
            if displacement & 0x8000:
                displacement -= 0x10000
            proposals.append({
                "address": f"0x{address:08X}", "size": 16,
                "table_register": table, "target_register": target,
                "slot_offset": displacement, "previous_word": f"{previous:08X}",
                "words": [f"{word:08X}" for word in (load_table, load_target, move_ctr, branch)],
            })
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(proposals, indent=2) + "\n")
    print(f"Wrote {len(proposals)} standalone thunk proposals. Review before adding hints.")


if __name__ == "__main__":
    main()
