"""Synthetic texture-pack fixtures; no copyrighted game files required."""
import hashlib
import json
from pathlib import Path
import struct
import sys
import tempfile
import unittest
from unittest import mock
import zipfile
sys.path.insert(0,str(Path(__file__).resolve().parent))
import texture_packs as packs


def sha(data):return hashlib.sha256(data).hexdigest()


def dds(width=2, height=2):
    header=bytearray(128)
    header[:4]=b'DDS '
    for offset,value in [(4,124),(12,height),(16,width),(28,1),(76,32),(80,0x41),(88,32),(108,0x1000)]:
        struct.pack_into('<I',header,offset,value)
    return bytes(header)+bytes(width*height*4)


class TextureTests(unittest.TestCase):
    def setUp(self):
        self.temporary=tempfile.TemporaryDirectory();self.root=Path(self.temporary.name)
        self.game=self.root/'.local/game';self.game.mkdir(parents=True)
        (self.game/'menu').mkdir();(self.game/'menu/image.dds').write_bytes(dds())
        (self.game/'default.xex').write_bytes(b'synthetic executable')
        (self.game/'other.bnd').write_bytes(b'keep original bundle')
        self.patch=mock.patch.object(packs,'GAME_HASH',sha(b'synthetic executable'));self.patch.start()
        self.archive=self.root/'pack.zip'
        self.manifest={'schema':1,'id':'example','name':'Example textures','game_xex_sha256':packs.GAME_HASH,'files':[
            {'target':'menu/image.dds','file':'textures/image.dds','original_sha256':sha(dds()),'sha256':sha(dds(4,4))}]}
        self.write_pack()
    def tearDown(self):self.patch.stop();self.temporary.cleanup()
    def write_pack(self, extra=None, data=None):
        with zipfile.ZipFile(self.archive,'w') as target:
            target.writestr('pack.json',json.dumps(self.manifest))
            target.writestr('textures/image.dds',dds(4,4) if data is None else data)
            if extra:target.writestr(extra,b'unlisted')
    def test_install_prepare_and_reuse_preserve_originals(self):
        token=packs.install(self.root,self.archive)['token']
        overlay=Path(packs.prepare(self.root,token)['game_root'])
        self.assertEqual((overlay/'menu/image.dds').read_bytes(),dds(4,4))
        self.assertEqual((self.game/'menu/image.dds').read_bytes(),dds())
        self.assertEqual((overlay/'default.xex').read_bytes(),b'synthetic executable')
        self.assertEqual((overlay/'other.bnd').read_bytes(),b'keep original bundle')
        self.assertEqual(packs.prepare(self.root,token)['game_root'],str(overlay))
        (overlay/'menu/image.dds').write_bytes(b'changed replacement')
        with self.assertRaisesRegex(ValueError,'changed'):packs.prepare(self.root,token)
        self.assertEqual((self.game/'menu/image.dds').read_bytes(),dds())
    def test_wrong_game_rejected(self):
        (self.game/'default.xex').write_bytes(b'wrong edition')
        with self.assertRaisesRegex(ValueError,'disc build'):packs.install(self.root,self.archive)
    def test_wrong_original_rejected(self):
        (self.game/'menu/image.dds').write_bytes(dds(3,3))
        with self.assertRaisesRegex(ValueError,'Original texture'):packs.install(self.root,self.archive)
    def test_changed_payload_rejected(self):
        self.write_pack(data=dds(8,8))
        with self.assertRaisesRegex(ValueError,'checksum'):packs.install(self.root,self.archive)
    def test_extra_files_rejected(self):
        self.write_pack(extra='run.exe')
        with self.assertRaisesRegex(ValueError,'unlisted'):packs.install(self.root,self.archive)
    def test_unsafe_paths_rejected(self):
        for path in ['../escape.dds','C:/escape.dds','menu\\image.dds','menu/CON.dds','menu/a:stream.dds','menu/image.dds.','/root.dds','menu//image.dds']:
            with self.subTest(path=path),self.assertRaises(ValueError):packs.relative_path(path)
    def test_executable_replacements_rejected(self):
        self.manifest['files'][0]['target']='default.xex';self.write_pack()
        with self.assertRaisesRegex(ValueError,'only DDS'):packs.install(self.root,self.archive)
    def test_truncated_dds_rejected(self):
        with self.assertRaisesRegex(ValueError,'truncated'):packs.texture_header(dds(8,8)[:128],'.dds')
    def test_preconverted_xpr_sizes(self):
        valid=b'XPR2'+struct.pack('>II',4,4)+b'headbody'
        self.assertEqual(packs.texture_header(valid,'.xpr'),b'XPR2')
        with self.assertRaisesRegex(ValueError,'size'):packs.texture_header(valid[:-1],'.xpr')
    def test_changed_installed_archive_rejected(self):
        token=packs.install(self.root,self.archive)['token']
        (self.root/'.local/texture-packs'/token/'pack.zip').write_bytes(b'changed')
        with self.assertRaisesRegex(ValueError,'checksum'):packs.prepare(self.root,token)
    def test_link_failure_keeps_base_intact(self):
        token=packs.install(self.root,self.archive)['token']
        with mock.patch.object(packs.os,'link',side_effect=OSError('different drive')):
            with self.assertRaisesRegex(ValueError,'NTFS'):packs.prepare(self.root,token)
        self.assertEqual((self.game/'menu/image.dds').read_bytes(),dds())


if __name__=='__main__':unittest.main()
