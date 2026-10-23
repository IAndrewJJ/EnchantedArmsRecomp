"""Build a self-contained, game-data-free Linux AppImage on Ubuntu 22.04."""
from pathlib import Path
import argparse
import hashlib
import importlib.metadata
import json
import os
import shutil
import subprocess
import sys
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
SOURCE_FILES = ['CMakeLists.txt', 'enchantedarmsrecomp_manifest.toml', 'generated/rexglue.cmake',
    'release.json', 'LICENSE', 'THIRD_PARTY_NOTICES.md', 'data/gamecontrollerdb.txt',
    'tools/inspect_disc.py', 'tools/setup_game.py', 'tools/local_paths.py']
PINNED = {
    'appimagetool-x86_64.AppImage': ('https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage',
        '95cbe7cce9717fce90c484e34052ee7c7f1d7635b33c12525b4776826a7d29b6'),
    'runtime-x86_64': ('https://github.com/AppImage/type2-runtime/releases/download/continuous/runtime-x86_64',
        '617aa77314d57c8b8e4fae9c3f0b35af7b30d5529cb69e98f64f92510ed98a46')}

def sha(path):
    digest = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for block in iter(lambda: stream.read(1024*1024), b''):
            digest.update(block)
    return digest.hexdigest()

def source_files():
    result = list(SOURCE_FILES)
    for directory, suffixes in [('src', {'.cpp','.h'}), ('config', {'.toml'}), ('licenses', {'.md','.txt',''})]:
        result += [p.relative_to(ROOT).as_posix() for p in (ROOT/directory).rglob('*') if p.is_file() and p.suffix in suffixes]
    if any('.local' in Path(name).parts or name.startswith('generated/default/') or Path(name).suffix in {'.xex','.iso','.dmp'} for name in result):
        raise ValueError('Private game data selected')
    return sorted(set(result))

def download(directory, name):
    url, expected = PINNED[name]
    path = directory / name
    if not path.exists():
        with urllib.request.urlopen(url, timeout=90) as source, path.open('wb') as target:
            shutil.copyfileobj(source, target)
    if sha(path) != expected:
        raise ValueError('The pinned AppImage build tool changed: ' + name)
    path.chmod(0o755)
    return path

def main(tools, work, output):
    work.mkdir(parents=True, exist_ok=True)
    project = work / 'project'
    ledger = {}
    for name in source_files():
        destination = project / name
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(ROOT/name, destination)
        ledger[name] = sha(destination)
    (project/'source-files.json').write_text(json.dumps(ledger, indent=2)+'\n')
    icon = ROOT/'launcher/branding/EnchantedArmsPC.png'
    command = [sys.executable, '-m', 'PyInstaller', '--noconfirm', '--clean', '--onedir',
        '--name', 'EnchantedArmsLauncher', '--distpath', str(work/'frozen'),
        '--workpath', str(work/'pyinstaller'), '--specpath', str(work),
        '--paths', str(ROOT/'linux'), '--paths', str(ROOT/'tools'),
        '--hidden-import', 'fcntl', '--hidden-import', 'setup_game', '--hidden-import', 'inspect_disc',
        '--add-data', str(project)+':project',
        '--add-data', str(icon)+':.', str(ROOT/'linux/launcher.py')]
    subprocess.run(command, check=True)
    frozen = work/'frozen/EnchantedArmsLauncher'
    # Keep the compiler/sysroot outside PyInstaller's ELF dependency analysis.
    # Promoting its compile-only glibc libraries to launcher dependencies would
    # interfere with the user's system loader on newer distributions.
    shutil.copytree(tools, frozen/'_internal/tools', dirs_exist_ok=True, symlinks=True)
    shutil.copy2(icon, frozen/'_internal/icon.png')
    # Keep a copy of the original Python launcher and its build recipe alongside
    # the frozen executable, so every included component remains inspectable.
    shutil.copytree(ROOT/'linux', frozen/'_internal/launcher-source',
                    ignore=shutil.ignore_patterns('__pycache__'), dirs_exist_ok=True)
    # Wheels carry component licenses outside the imported Python modules.
    # Keep that metadata with the frozen application, including Qt notices.
    notices = frozen/'_internal/package-notices'
    for name in ('PySide6-Essentials', 'shiboken6', 'pyinstaller'):
        distribution = importlib.metadata.distribution(name)
        folder = notices/name
        folder.mkdir(parents=True, exist_ok=True)
        (folder/'METADATA').write_text(distribution.read_text('METADATA') or '', encoding='utf-8')
        for entry in distribution.files or []:
            if any('license' in part.lower() or 'copyright' in part.lower() for part in entry.parts):
                original = Path(distribution.locate_file(entry))
                if original.is_file():
                    target = folder/entry
                    target.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(original, target)
    for path in frozen.rglob('*'):
        if path.suffix.lower() in {'.iso','.xex','.xexp','.dmp'} or 'generated/default' in path.as_posix():
            raise ValueError('Private game data in AppImage: ' + str(path))
    appdir = work/'EnchantedArmsPC.AppDir'
    (appdir/'usr/bin').mkdir(parents=True, exist_ok=True)
    shutil.copytree(frozen, appdir/'usr/bin/EnchantedArmsLauncher', dirs_exist_ok=True, symlinks=True)
    shutil.copy2(icon, appdir/'enchantedarmspc.png')
    (appdir/'.DirIcon').symlink_to('enchantedarmspc.png')
    (appdir/'enchantedarmspc.desktop').write_text('[Desktop Entry]\nType=Application\nName=EnchantedArmsPC\n'
        'Exec=EnchantedArmsLauncher\nIcon=enchantedarmspc\nTerminal=false\nCategories=Game;\n')
    apprun = appdir/'AppRun'
    apprun.write_text('#!/bin/sh\nbase=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)\n'
        'exec "$base/usr/bin/EnchantedArmsLauncher/EnchantedArmsLauncher" "$@"\n')
    apprun.chmod(0o755)
    appimagetool = download(work, 'appimagetool-x86_64.AppImage')
    runtime = download(work, 'runtime-x86_64')
    subprocess.run([str(appimagetool), '--appimage-extract'], cwd=work, check=True, stdout=subprocess.DEVNULL)
    output.parent.mkdir(parents=True, exist_ok=True)
    environment = dict(os.environ, ARCH='x86_64')
    subprocess.run([str(work/'squashfs-root/AppRun'), '--runtime-file', str(runtime),
        '--comp', 'zstd', str(appdir), str(output)], env=environment, check=True)
    (output.parent/'SHA256SUMS.txt').write_text(sha(output)+'  '+output.name+'\n')
    (output.parent/'linux-package-report.json').write_text(json.dumps({'version':'0.1.0',
        'sha256':sha(output), 'bytes':output.stat().st_size, 'source_files':len(ledger),
        'game_data_included':False, 'native_gameplay_tested':False}, indent=2)+'\n')
    print('AppImage built: ' + str(output))

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--tools', type=Path, required=True)
    parser.add_argument('--work', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    options = parser.parse_args()
    main(options.tools.resolve(), options.work.resolve(), options.output.resolve())
