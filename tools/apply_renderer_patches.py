"""Apply the small, version-pinned renderer changes without requiring Git."""
import json
from pathlib import Path

PROJECT = Path(__file__).resolve().parents[1]


def apply(source):
    source = Path(source).resolve()
    for patch in sorted((PROJECT / "renderer/patches").glob("*.json")):
        changes = json.loads(patch.read_text())
        for change in changes["files"]:
            path = (source / change["path"]).resolve()
            if not path.is_relative_to(source): raise ValueError("Invalid renderer patch path")
            contents = original = path.read_text()
            for edit in change["replacements"]:
                if edit["after"] in contents: continue
                if contents.count(edit["before"]) != 1:
                    raise ValueError("Unsupported renderer source at " + change["path"])
                contents = contents.replace(edit["before"], edit["after"], 1)
            if contents != original: path.write_text(contents, newline="\n")
    print("Renderer texture-detail patches applied.", flush=True)
