"""Synthetic update transactions. No game, network, or real launcher processes."""
import hashlib
import json
import shutil
from pathlib import Path
import sys
import tempfile
import unittest
import zipfile
sys.path.insert(0,str(Path(__file__).resolve().parent))
import update_game as update
from local_paths import filesystem_path
from package_release import write_member
from validate_package import PREFIX

class UpdateTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.root=Path(self.temp.name)
        (self.root/'README.md').write_bytes(b'old readme')
        (self.root/'.local/runtime/user').mkdir(parents=True)
        (self.root/'.local/runtime/user/save').write_bytes(b'my save')
        (self.root/'bin').mkdir();(self.root/'bin/game.exe').write_bytes(b'private game')
        self.stage=self.root/'.local/updates/staged-test';self.stage.mkdir(parents=True)
        self.archive=self.stage/'kit.zip'
        self.make_kit({'README.md':b'new readme','release.json':b'{"version":"9.0.0"}','src/new.cpp':b'new file'})
    def tearDown(self):
        target=filesystem_path(self.root).resolve()
        if not target.is_relative_to(filesystem_path(tempfile.gettempdir()).resolve()):
            raise ValueError('Test cleanup escaped its temporary folder')
        shutil.rmtree(target)
        self.temp.cleanup()
    def make_kit(self,files):
        with zipfile.ZipFile(self.archive,'w') as z:
            for name,data in files.items():write_member(z,PREFIX+name,data)
            write_member(z,PREFIX+'SHA256SUMS.json',json.dumps({n:hashlib.sha256(d).hexdigest() for n,d in files.items()}).encode())
        (self.stage/'verified.json').write_text(json.dumps({'version':'9.0.0','sha256':hashlib.sha256(self.archive.read_bytes()).hexdigest()}))
    def protected(self):
        self.assertEqual((self.root/'.local/runtime/user/save').read_bytes(),b'my save')
        self.assertEqual((self.root/'bin/game.exe').read_bytes(),b'private game')
    def test_install_and_rollback_preserve_game_and_saves(self):
        result=update.install(self.root,self.stage)
        self.assertEqual((self.root/'README.md').read_bytes(),b'new readme');self.protected()
        self.assertTrue((self.root/'SHA256SUMS.json').exists())
        update.rollback(self.root,Path(result['transaction']))
        self.assertEqual((self.root/'README.md').read_bytes(),b'old readme')
        self.assertFalse((self.root/'src/new.cpp').exists());self.protected()
        self.assertFalse((self.root/'SHA256SUMS.json').exists())
        self.assertFalse((self.root/'.local/updates/latest.json').exists())
        with self.assertRaisesRegex(ValueError,'already'):update.rollback(self.root,Path(result['transaction']))
    def test_interrupted_install_restores_originals(self):
        with self.assertRaises(OSError):update.install(self.root,self.stage,failure_after=2)
        self.assertEqual((self.root/'README.md').read_bytes(),b'old readme');self.assertFalse((self.root/'release.json').exists());self.protected()
    def test_changed_stage_is_rejected_before_writes(self):
        with self.archive.open('ab') as f:f.write(b'changed')
        with self.assertRaisesRegex(ValueError,'changed'):update.install(self.root,self.stage)
        self.assertEqual((self.root/'README.md').read_bytes(),b'old readme')
    def test_private_paths_rejected_in_rollback_journal(self):
        for name in ['.local/runtime/user/save','../escape','bin/game.exe','generated/default/game.cpp','other.exe','C:/escape']:
            with self.subTest(name=name),self.assertRaises(ValueError):update.safe_target(self.root,name)
    def test_changed_backup_is_rejected(self):
        result=update.install(self.root,self.stage)
        (self.stage/'backup/README.md').write_bytes(b'changed')
        with self.assertRaisesRegex(ValueError,'backup'):update.rollback(self.root,Path(result['transaction']))
    def test_different_installation_stage_is_rejected(self):
        with self.assertRaisesRegex(ValueError,'staging'):update.install(self.root,self.root/'somewhere')
    def test_invalid_version_cannot_become_a_download_url(self):
        for v in ['../9','9.0.0/evil','https://other','v9.0.0','9.0']:
            with self.assertRaises(ValueError):update.version(v)
    def test_atomic_write_supports_deep_windows_paths(self):
        path=self.root.joinpath(*(['portable-installation-backup-'+'x'*30]*5),'data.txt')
        update.atomic_bytes(path,b'deep path')
        self.assertEqual(filesystem_path(path).read_bytes(),b'deep path')
    def test_unfinished_update_must_be_restored_first(self):
        (self.root/'.local/updates/recovery.json').write_text('{}')
        with self.assertRaisesRegex(ValueError,'unfinished'):update.install(self.root,self.stage)
    def test_update_lock_prevents_overlapping_transactions(self):
        with update.update_lock(self.root):
            with self.assertRaises(OSError):
                with update.update_lock(self.root):pass
        with update.update_lock(self.root):pass

if __name__=='__main__':unittest.main()
