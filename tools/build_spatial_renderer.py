"""Build the pinned ReXGlue spatial FSR/CAS runtime, with no game data involved."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

sys.path.insert(0, str(Path(__file__).resolve().parent))
from apply_renderer_patches import apply as apply_patches

PROJECT = Path(__file__).resolve().parents[1]
COMMIT = "f5337cdc947ff6d4c4196737e2c807a48f2a1fc2"
SUBMODULES = ["cli11","libmspack","FFmpeg","tomlplusplus","simde","xxHash","spdlog","fmt","utfcpp","imgui","sdl3","tracy","inja","o1heap"]

def run(args, **kwargs):
    return subprocess.run([str(a) for a in args], check=True, **kwargs)

def materialize_mspack_links(git, source):
    module = source / "thirdparty/libmspack"
    listing = run([git,"-c","safe.directory=" + str(module),"-C",module,"ls-files","-s"], capture_output=True,text=True).stdout
    for line in listing.splitlines():
        if not line.startswith("120000 "): continue
        name = line.split("\t",1)[1]
        path = module / name
        if path.is_symlink(): continue
        # Git without Windows symlink privileges checks out symlinks as text.
        # Resolve only recorded, in-repository links; no elevated privileges needed.
        contents = path.read_bytes()
        if len(contents) > 512: continue
        target = (path.parent / contents.decode().strip()).resolve()
        if target.is_relative_to(module.resolve()) and target.is_file():
            path.write_bytes(target.read_bytes())

def package_runtime(source, destination):
    output = source / "out/win-amd64"
    destination.mkdir(parents=True,exist_ok=True)
    files = ["rexruntimerd.dll","rexruntimerd.lib","rexgpu-xenosrd.dll","rexgpu-xenosrd.lib","TracyClientrd.dll"]
    hashes = {}
    for name in files:
        source_file = output / name
        if not source_file.exists(): raise FileNotFoundError(source_file)
        shutil.copy2(source_file,destination / name)
        hashes[name] = hashlib.sha256(source_file.read_bytes()).hexdigest()
    manifest = dict(version="0.3.0",sdk_commit=COMMIT,spatial_fsr=True,cas=True,texture_detail=True,temporal_fsr=False,frame_generation=False,files=hashes)
    (destination / "renderer-capabilities.json").write_text(json.dumps(manifest,indent=2)+"\n")
    print("Spatial FSR/CAS runtime packaged and hashed.",flush=True)

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--git",default="git")
    parser.add_argument("--source",type=Path,default=PROJECT / ".local/toolchain/rexglue-fsr-source")
    parser.add_argument("--build",type=Path,default=PROJECT / ".local/toolchain/rexglue-spatial-build")
    parser.add_argument("--output",type=Path,default=PROJECT / "renderer")
    parser.add_argument("--jobs",type=int,default=4)
    parser.add_argument("--rc"); parser.add_argument("--mt")
    parser.add_argument("--package-only",action="store_true")
    parser.add_argument("--offline-source",action="store_true",help="Use the accompanying renderer source archive, without Git downloads")
    args = parser.parse_args()
    if not args.package_only:
        if args.offline_source:
            provenance = json.loads((args.source / "SOURCE_PROVENANCE.json").read_text())
            if provenance["sdk_commit"] != COMMIT or set(provenance["submodules"]) != set(SUBMODULES):
                raise ValueError("Unsupported renderer source archive")
        else:
            if not (args.source / ".git").exists():
                run([args.git,"-c","http.sslBackend=openssl","clone","--depth","1","--branch","v0.10.0","https://github.com/rexglue/rexglue-sdk.git",args.source])
            actual = run([args.git,"-c","safe.directory=" + str(args.source),"-C",args.source,"rev-parse","HEAD"],capture_output=True,text=True).stdout.strip()
            if actual != COMMIT: raise ValueError("Unexpected SDK source revision")
            run([args.git,"-c","http.sslBackend=openssl","-c","safe.directory=" + str(args.source),"-C",args.source,"submodule","update","--init","--depth","1","--jobs","4","--",*["thirdparty/" + s for s in SUBMODULES]])
            materialize_mspack_links(args.git,args.source)
        apply_patches(args.source)
        prefix = Path(os.path.commonpath([str(PROJECT.resolve()),str(args.source.resolve())])).as_posix()
        maps = " -ffile-prefix-map=" + prefix + "=/build -fdebug-prefix-map=" + prefix + "=/build"
        linker = "-fuse-ld=lld -Wl,/pdbaltpath:ReXGlueSpatial.pdb"
        command = ["cmake","-S",args.source,"-B",args.build,"-G","Ninja","-DCMAKE_BUILD_TYPE=RelWithDebInfo",
            "-DCMAKE_CXX_COMPILER=clang++","-DCMAKE_C_COMPILER=clang","-DCMAKE_CXX_FLAGS=-DREX_HAS_FIDELITYFX_SDK=1 -msse4.1" + maps,
            "-DCMAKE_C_FLAGS=" + maps.strip(),
            "-DREXGLUE_ENABLE_FIDELITYFX=OFF","-DCMAKE_EXE_LINKER_FLAGS=" + linker,"-DCMAKE_SHARED_LINKER_FLAGS=" + linker]
        # The SDK already contains compiled EASU/RCAS/CAS spatial shaders. Enable
        # their presenter paths consistently across all C++ targets. The optional
        # temporal FidelityFX API/backend is intentionally not built or advertised.
        if args.rc: command.append("-DCMAKE_RC_COMPILER:FILEPATH=" + args.rc.replace("\\","/"))
        if args.mt: command.append("-DCMAKE_MT:FILEPATH=" + args.mt.replace("\\","/"))
        run(command)
        run(["cmake","--build",args.build,"--target","rexruntime","rexgpu-xenos","--parallel",str(args.jobs)])
    package_runtime(args.source,args.output)

if __name__ == "__main__": main()
