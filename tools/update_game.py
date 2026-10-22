"""Verified launcher-kit updates with local transaction backups. Never installs game data."""
import argparse
import contextlib
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import time
import urllib.request
import uuid
import zipfile

sys.path.insert(0, str(Path(__file__).resolve().parent))
from validate_package import validate, validate_name, PREFIX, PRIVATE_PARTS
from local_paths import filesystem_path

REPO = "IAndrewJJ/EnchantedArmsRecomp"
VERSION = re.compile(r"^\d+\.\d+\.\d+$")


def version(value):
    if not isinstance(value, str) or not VERSION.fullmatch(value):
        raise ValueError("Unsupported release version")
    return tuple(map(int, value.split('.')))


def read_url(url, limit):
    request = urllib.request.Request(url, headers={"User-Agent": "EnchantedArmsPC-Updater"})
    with urllib.request.urlopen(request, timeout=45) as response:
        data = response.read(limit + 1)
    if len(data) > limit:
        raise ValueError("Download exceeds the permitted size")
    return data


def check(root):
    root=filesystem_path(root)
    current = json.loads((root / 'release.json').read_text())['version']
    releases = json.loads(read_url(f"https://api.github.com/repos/{REPO}/releases?per_page=50", 2*1024*1024))
    choices = []
    # Preview builds are intentional for this project. Never select drafts or
    # execute arbitrary links returned by a release description.
    for release in releases:
        tag = release.get('tag_name', '')
        if release.get('draft') or not tag.startswith('v') or not VERSION.fullmatch(tag[1:]):
            continue
        kit = f"EnchantedArmsPC-{tag[1:]}-windows-x64.zip"
        names = {a.get('name'): a for a in release.get('assets', [])}
        if kit in names and 'SHA256SUMS.txt' in names:
            choices.append(tag[1:])
    latest = max(choices, key=version) if choices else current
    return {'current': current, 'latest': latest, 'available': version(latest)>version(current),
            'url': f'https://github.com/{REPO}/releases/tag/v{latest}'}


def stage(root, target):
    root=filesystem_path(root)
    version(target)
    kit = f"EnchantedArmsPC-{target}-windows-x64.zip"
    base = f"https://github.com/{REPO}/releases/download/v{target}/"
    ledger = read_url(base+'SHA256SUMS.txt', 32768).decode('utf-8')
    matches = [line.split() for line in ledger.splitlines() if len(line.split()) == 2]
    hashes = [h.lower() for h, name in matches if name == kit and re.fullmatch(r'[0-9a-fA-F]{64}', h)]
    if len(hashes) != 1:
        raise ValueError("Release does not contain exactly one kit checksum")
    data = read_url(base+kit, 128*1024*1024)
    if hashlib.sha256(data).hexdigest() != hashes[0]:
        raise ValueError("Downloaded kit checksum does not match")
    directory = root/'.local/updates'/('staged-'+target+'-'+uuid.uuid4().hex)
    directory.mkdir(parents=True, exist_ok=False)
    archive = directory/'kit.zip'
    archive.write_bytes(data)
    validate(archive)
    with zipfile.ZipFile(archive) as source:
        info = json.loads(source.read(PREFIX+'release.json'))
    if info['version'] != target:
        raise ValueError("Kit version does not match the requested release")
    (directory/'verified.json').write_text(json.dumps({'version': target, 'sha256': hashes[0]}))
    return {'version': target, 'stage': str(directory), 'restart_required': True}


def safe_target(root, name):
    # Validation checks the member allowlist. Check resolved destinations too:
    # an existing junction/symlink must not redirect writes out of the project.
    root=filesystem_path(root)
    validate_name(name)
    path = root/name
    if not path.resolve().is_relative_to(root.resolve()):
        raise ValueError("Update destination escapes the installation")
    resolved = path.resolve().relative_to(root.resolve())
    if {p.casefold() for p in resolved.parts} & PRIVATE_PARTS:
        raise ValueError("Update destination resolves to private files")
    return path


def atomic_bytes(path, data):
    path=filesystem_path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name+'.'+uuid.uuid4().hex+'.update-partial')
    with temporary.open('xb') as stream:
        stream.write(data)
    for attempt in range(30):
        try:
            os.replace(temporary, path)
            return
        except OSError:
            if attempt == 29:
                raise
            time.sleep(0.1)


@contextlib.contextmanager
def update_lock(root):
    root=filesystem_path(root)
    path = root/'.local/update.lock'
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open('a+b') as stream:
        stream.seek(0)
        if os.name == 'nt':
            import msvcrt
            msvcrt.locking(stream.fileno(), msvcrt.LK_NBLCK, 1)
        else:
            import fcntl
            fcntl.flock(stream, fcntl.LOCK_EX | fcntl.LOCK_NB)
        yield


def rollback(root, record):
    root,record=filesystem_path(root),filesystem_path(record)
    if not record.resolve().is_relative_to((root/'.local/updates').resolve()):
        raise ValueError("Rollback must use a local update transaction")
    journal = json.loads(record.read_text())
    if journal.get('state') not in {'installed', 'applying'}:
        raise ValueError("This update has already been rolled back")
    backup = record.parent/'backup'
    # Verify every original before starting the restore.
    for name, old_hash in journal['originals'].items():
        safe_target(root, name)
        if old_hash is not None:
            path = safe_target(backup, name)
            if hashlib.sha256(path.read_bytes()).hexdigest() != old_hash:
                raise ValueError("Update backup is incomplete or changed")
    for name, old_hash in journal['originals'].items():
        path = safe_target(root, name)
        if old_hash is None:
            if path.exists():
                path.unlink()
        else:
            atomic_bytes(path, safe_target(backup, name).read_bytes())
    journal['state'] = 'rolled-back'
    atomic_bytes(record, json.dumps(journal, indent=2).encode())
    latest = root/'.local/updates/latest.json'
    if latest.exists() and Path(json.loads(latest.read_text())['transaction']).resolve() == record.resolve():
        previous = journal.get('previous')
        if previous:
            atomic_bytes(latest, json.dumps(previous).encode())
        else:
            latest.unlink()


def install(root, directory, failure_after=None):
    root, directory = filesystem_path(root).resolve(), filesystem_path(directory).resolve()
    if (root/'.local/updates/recovery.json').exists():
        raise ValueError("Restore the unfinished update before installing another kit")
    if not directory.is_relative_to((root/'.local/updates').resolve()):
        raise ValueError("Update must come from the installation's staging folder")
    marker = json.loads((directory/'verified.json').read_text())
    archive = directory/'kit.zip'
    if hashlib.sha256(archive.read_bytes()).hexdigest() != marker['sha256']:
        raise ValueError("Staged kit changed after verification")
    validate(archive)
    record = directory/'transaction.json'
    if record.exists():
        raise ValueError("This staged update has already been used")
    backup = directory/'backup'
    with zipfile.ZipFile(archive) as source:
        if json.loads(source.read(PREFIX+'release.json'))['version'] != marker['version']:
            raise ValueError("Staged version does not match the verified kit")
        ledger = json.loads(source.read(PREFIX+'SHA256SUMS.json'))
        names = list(ledger) + ['SHA256SUMS.json']
        originals = {}
        for name in names:
            path = safe_target(root, name)
            old = path.read_bytes() if path.exists() else None
            originals[name] = hashlib.sha256(old).hexdigest() if old is not None else None
            if old is not None:
                atomic_bytes(safe_target(backup, name), old)
        latest = root/'.local/updates/latest.json'
        previous = json.loads(latest.read_text()) if latest.exists() else None
        journal = {'state': 'applying', 'version': marker['version'], 'originals': originals, 'previous': previous}
        atomic_bytes(record, json.dumps(journal, indent=2).encode())
        recovery=root/'.local/updates/recovery.json'
        atomic_bytes(recovery,json.dumps({'transaction':str(record)}).encode())
        try:
            for number, name in enumerate(names):
                if failure_after is not None and number == failure_after:
                    raise OSError("Synthetic interrupted update")
                atomic_bytes(safe_target(root, name), source.read(PREFIX+name))
            journal['state'] = 'installed'
            atomic_bytes(record, json.dumps(journal, indent=2).encode())
            atomic_bytes(root/'.local/updates/latest.json', json.dumps({'transaction': str(record)}).encode())
            recovery.unlink()
        except Exception:
            rollback(root, record)
            recovery.unlink(missing_ok=True)
            raise
    return {'version': marker['version'], 'transaction': str(record)}


def wait_for_launcher(pid):
    if not pid:
        return
    if os.name != 'nt':
        raise ValueError("Waiting for the launcher requires Windows")
    import ctypes
    kernel = ctypes.WinDLL('kernel32', use_last_error=True)
    kernel.OpenProcess.restype = ctypes.c_void_p
    kernel.WaitForSingleObject.argtypes = [ctypes.c_void_p, ctypes.c_ulong]
    kernel.CloseHandle.argtypes = [ctypes.c_void_p]
    handle = kernel.OpenProcess(0x00100000, False, pid)
    if handle:
        try:
            if kernel.WaitForSingleObject(handle, 60000) != 0:
                raise ValueError("Close the launcher before applying the update")
        finally:
            kernel.CloseHandle(handle)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=['check', 'stage', 'apply', 'rollback'])
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument('--version')
    parser.add_argument('--stage', type=Path)
    parser.add_argument('--wait-pid', type=int)
    parser.add_argument('--restart', action='store_true')
    args = parser.parse_args()
    root = filesystem_path(args.root).resolve()
    if not (root/'enchantedarmsrecomp_manifest.toml').is_file():
        raise ValueError("This folder is not an EnchantedArmsPC installation")
    if args.command == 'check':
        result = check(root)
    elif args.command == 'stage':
        result = stage(root, args.version)
    else:
        wait_for_launcher(args.wait_pid)
        try:
            with update_lock(root):
                if args.command == 'apply':
                    result = install(root, args.stage)
                else:
                    recovery=root/'.local/updates/recovery.json'
                    pointer=recovery if recovery.exists() else root/'.local/updates/latest.json'
                    record = Path(json.loads(pointer.read_text())['transaction'])
                    rollback(root, record)
                    recovery.unlink(missing_ok=True)
                    result = {'rolled_back': True}
                (root/'.local/updates/last-error.json').unlink(missing_ok=True)
        except Exception as error:
            atomic_bytes(root/'.local/updates/last-error.json',json.dumps({'error':str(error)}).encode())
            raise
        finally:
            if args.restart:
                subprocess.Popen([str(root/'EnchantedArmsLauncher.exe')], cwd=root)
    print(json.dumps(result))


if __name__ == '__main__':
    try:
        main()
    except Exception as error:
        print(json.dumps({'error': str(error)}))
        sys.exit(1)
