"""Synthetic Linux settings, folder, save and distribution tests; no game data."""
from pathlib import Path
import json
import os
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parent))
import core
import package_appimage

class LinuxTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory(prefix='ea-linux-')
        self.addCleanup(self.directory.cleanup)
        self.base = Path(self.directory.name)
        self.root = self.base/'Game with spaces'
        self.settings = core.normalize({'BuildLocation': str(self.root)})

    def test_both_frame_rates_lock_presentation_without_guest_speed_change(self):
        for limit in (30,60):
            for old in (False, True):
                selected = core.normalize(dict(self.settings, FrameLimit=limit, AllowTearing=old))
                args = core.game_arguments(selected, self.root/'boot.log')
                self.assertIn('--ea_fps_limit='+str(limit), args)
                self.assertIn('--no-vsync', args)
                for mode in ('immediate','mailbox','fifo_relaxed'):
                    self.assertIn('--no-vulkan_allow_present_mode_'+mode, args)
                self.assertNotIn('AllowTearing', selected)
                self.assertFalse(any('d3d12' in arg for arg in args))

    def test_graphics_arguments_and_supersampling(self):
        selected = core.normalize(dict(self.settings, Upscaler='fsr', Sharpness=80,
            TextureDetail=2, PostAA='ssaa', Width=2560, Scale=1))
        args = core.game_arguments(selected, self.root/'boot.log')
        self.assertEqual(selected['Scale'], 3)
        self.assertIn('--present_effect=fsr', args)
        self.assertIn('--present_fsr_sharpness_reduction=0.40', args)
        self.assertIn('--swap_post_effect=fxaa', args)
        self.assertIn('--ea_texture_lod_bias=-1.0', args)

    def test_pad_mapping_keeps_keyboard_actions(self):
        pads = list(range(14)); pads[0], pads[1] = pads[1], pads[0]
        selected = core.normalize(dict(self.settings, PadButtons=pads, SwapTriggers=True))
        args = core.game_arguments(selected, self.root/'boot.log')
        self.assertIn('--keybind_b=Space,Semicolon', args)
        self.assertIn('--keybind_right_trigger=Q,I', args)
        self.assertIn('--ea_swap_triggers', args)

    def test_invalid_settings_are_normalized(self):
        selected = core.normalize(dict(self.settings, PadButtons=['bad'], FrameLimit=120,
            Sharpness='bad', Scale=90, Keys=None))
        self.assertEqual(selected['FrameLimit'],30)
        self.assertEqual(selected['Sharpness'],80)
        self.assertEqual(selected['Keys'],core.DEFAULT_KEYS)
        self.assertEqual(selected['PadButtons'],list(range(14)))

    def test_legacy_saves_and_retries_keep_latest_progress(self):
        legacy = self.root/'.local/runtime/user/profile/save.bin'
        legacy.parent.mkdir(parents=True)
        legacy.write_bytes(b'original progress')
        saves = core.prepare_saves(self.settings)
        self.assertEqual((saves/'profile/save.bin').read_bytes(),b'original progress')
        (saves/'profile/save.bin').write_bytes(b'new progress')
        core.prepare_saves(self.settings)
        self.assertEqual((saves/'profile/save.bin').read_bytes(),b'new progress')
        self.assertEqual(legacy.read_bytes(),b'original progress')

    def test_changing_save_folder_copies_without_overwriting(self):
        source, destination = self.base/'Old saves', self.base/'New saves'
        source.mkdir(); destination.mkdir()
        (source/'save.bin').write_bytes(b'current progress')
        (source/'other.bin').write_bytes(b'another slot')
        (destination/'save.bin').write_bytes(b'different progress')
        with self.assertRaisesRegex(ValueError,'different saved progress'):
            core.copy_saves(source,destination)
        self.assertFalse((destination/'other.bin').exists())
        (destination/'save.bin').unlink()
        core.copy_saves(source,destination)
        self.assertEqual((destination/'save.bin').read_bytes(),b'current progress')
        self.assertEqual((source/'save.bin').read_bytes(),b'current progress')

    def test_empty_and_existing_project_roots_and_outer_wrapper(self):
        self.assertEqual(core.validate_build(self.root), self.root)
        self.root.mkdir()
        (self.root/'unrelated.txt').write_text('keep')
        with self.assertRaises(ValueError): core.validate_build(self.root)
        (self.root/'enchantedarmsrecomp_manifest.toml').write_text('name = "enchantedarmsrecomp"')
        self.assertEqual(core.validate_build(self.root),self.root)
        self.assertEqual(core.validate_build(self.base),self.root)

    def test_first_setup_can_resume_after_creating_its_log_and_request(self):
        core.reserve_build_root(self.root)
        (self.root/'.local/setup.log').write_text('setup started')
        self.assertEqual(core.validate_build(self.root), self.root)

    def test_unsafe_save_roots_are_rejected(self):
        for destination in (self.root, self.base, self.root/'.local/game', Path(self.root.anchor)):
            with self.subTest(destination=destination), self.assertRaises(ValueError):
                core.validate_save(self.root,destination)

    @unittest.skipIf(os.name=='nt','Directory symlinks require a Windows privilege')
    def test_linked_save_files_are_rejected(self):
        source = self.base/'Old saves';source.mkdir()
        external = self.base/'outside.bin';external.write_bytes(b'keep')
        (source/'linked').symlink_to(external)
        with self.assertRaises(ValueError):core.copy_saves(source,self.base/'New saves')
        self.assertEqual(external.read_bytes(),b'keep')

    def test_settings_do_not_write_to_appimage_and_preserve_preferences(self):
        with patch.object(core,'settings_file',return_value=self.base/'config/launcher.json'):
            selected = core.save_settings(dict(self.settings, FrameLimit=60))
            self.assertEqual(core.load_settings()['FrameLimit'],60)
            self.assertFalse(self.root.exists())
            self.assertEqual(selected['BuildLocation'],str(self.root))

    def test_public_source_bundle_excludes_game_data(self):
        files = package_appimage.source_files()
        self.assertIn('src/opening_battle.cpp',files)
        self.assertIn('config/timing_hooks.toml',files)
        self.assertIn('tools/inspect_disc.py',files)
        for name in files:
            self.assertNotIn('.local',Path(name).parts)
            self.assertFalse(name.startswith('generated/default/'))
            self.assertNotIn(Path(name).suffix,{'.xex','.iso','.dmp'})

if __name__ == '__main__':
    unittest.main()
