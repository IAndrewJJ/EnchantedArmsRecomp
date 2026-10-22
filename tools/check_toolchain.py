"""Install verified build tools in a NEW directory, without an ISO or existing SDK.

This network integration check is separate from the offline synthetic tests.
It never launches the game and does not modify a player's installation.
"""
import argparse
import json
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parent))
import setup_game


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--directory", type=Path, required=True, help="Must not already exist")
    parser.add_argument("--sdk-only", action="store_true", help="Download/check only the 43 MiB ReXGlue SDK")
    args = parser.parse_args()
    local = args.directory.resolve()
    local.mkdir(parents=True, exist_ok=False)
    if args.sdk_only:
        packages = json.loads((setup_game.PROJECT / "toolchain.lock.json").read_text(encoding="utf-8"))
        package = next(p for p in packages if p["name"].startswith("rexglue-sdk-"))
        setup_game.install_package(package, local / "toolchain")
        # Exercise verification/reuse of the cache as well as the initial download.
        setup_game.download(package, local / "toolchain/downloads")
    else:
        setup_game.prepare_toolchain(local)
        setup_game.prepare_toolchain(local)
    print("Fresh toolchain check passed: " + str(local), flush=True)


if __name__ == "__main__":
    main()
