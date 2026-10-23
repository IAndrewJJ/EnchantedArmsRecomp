"""Linux launcher backend. Game files and generated code never enter an AppImage."""
from pathlib import Path
import hashlib
import json
import os
import shutil
import subprocess
import sys
import time
import uuid

VERSION = '0.1.0'
NATIVE = '0.3.6'
DEFAULTS = dict(Iso='', BuildLocation='', SaveLocation='', Scale=2, Upscaler='bilinear',
    PostAA='none', Anisotropy=5, TextureDetail=0, Sharpness=80, FrameLimit=30,
    Fullscreen=True, Width=1920, Height=1080, KeepAspect=True, Dither=False,
    NativeMsaa=True, Jobs=2, MouseCamera=False, MouseSensitivity=100,
    LeftDeadzone=0, RightDeadzone=0, CameraResponse=100, InvertCameraY=False,
    SwapTriggers=False, FrameTimeGraph=False)
CONTROL_FLAGS = ['a','b','x','y','start','back','left_shoulder','right_shoulder',
    'lstick_press','rstick_press','dpad_up','dpad_down','dpad_left','dpad_right',
    'left_trigger','right_trigger','lstick_up','lstick_down','lstick_left','lstick_right',
    'rstick_up','rstick_down','rstick_left','rstick_right']
CONTROL_NAMES = ['A / Confirm','B / Back','X','Y / Menu','Start','Back','Left shoulder',
    'Right shoulder','Left stick press','Right stick press','D-pad up','D-pad down',
    'D-pad left','D-pad right','Left trigger','Right trigger','Move up','Move down',
    'Move left','Move right','Camera up','Camera down','Camera left','Camera right']
DEFAULT_KEYS = ['Space,Semicolon','Backspace,Quote','L','P','Return,X','Tab,Z','1','3',
    'F','K','Shift+Up','Shift+Down','Shift+Left','Shift+Right','Q,I','E,O','W','S','A','D',
    'Up','Down','Left','Right']

def resources():
    return Path(getattr(sys, '_MEIPASS', Path(__file__).resolve().parent))

def appimage():
    value = os.environ.get('APPIMAGE')
    return Path(value).absolute() if value else None

def default_root():
    image = appimage()
    return (image.parent if image else Path.home() / 'Games') / 'EnchantedArmsPC'

def settings_file():
    return Path(os.environ.get('XDG_CONFIG_HOME', Path.home() / '.config')) / 'EnchantedArmsPC/launcher.json'

def normalize(raw):
    settings = dict(DEFAULTS)
    settings.update({k: v for k, v in raw.items() if k in DEFAULTS})
    for name, choices in [('Scale', (1,2,3,4)), ('FrameLimit', (30,60)),
        ('Width', (1280,1920,2560,3840)), ('Anisotropy', (-1,0,1,2,3,4,5)),
        ('TextureDetail', (0,1,2)), ('Upscaler', ('bilinear','fsr','cas')),
        ('PostAA', ('none','fxaa','fxaa_extreme','ssaa'))]:
        if settings[name] not in choices:
            settings[name] = DEFAULTS[name]
    for name, minimum, maximum in [('Sharpness',0,100), ('Jobs',1,8), ('MouseSensitivity',1,1000),
        ('LeftDeadzone',0,40), ('RightDeadzone',0,40), ('CameraResponse',25,200)]:
        try:
            settings[name] = max(minimum, min(maximum, int(settings[name])))
        except (TypeError, ValueError):
            settings[name] = DEFAULTS[name]
    for name, default in DEFAULTS.items():
        if isinstance(default, bool):
            settings[name] = settings[name] if isinstance(settings[name], bool) else default
        if isinstance(default, str) and not isinstance(settings[name], str):
            settings[name] = default
    settings['BuildLocation'] = settings['BuildLocation'] or str(default_root())
    keys = raw.get('Keys', DEFAULT_KEYS)
    settings['Keys'] = [str(k) for k in keys] if isinstance(keys, list) and len(keys) == 24 else list(DEFAULT_KEYS)
    pads = raw.get('PadButtons', list(range(14)))
    settings['PadButtons'] = pads if isinstance(pads, list) and all(type(p) is int for p in pads) and sorted(pads) == list(range(14)) else list(range(14))
    settings['Height'] = dict(zip((1280,1920,2560,3840), (720,1080,1440,2160)))[settings['Width']]
    if settings['PostAA'] == 'ssaa':
        settings['Scale'] = max(settings['Scale'], min(4, settings['Width'] // 1280 + 1))
    return settings

def atomic_json(path, data):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + '.' + uuid.uuid4().hex + '.partial')
    try:
        temporary.write_text(json.dumps(data, indent=2) + '\n', encoding='utf-8')
        os.replace(temporary, path)
    finally:
        temporary.unlink(missing_ok=True)

def load_settings():
    path = settings_file()
    if path.exists():
        return normalize(json.loads(path.read_text(encoding='utf-8-sig')))
    return normalize({})

def build_root(settings):
    return Path(settings['BuildLocation']).expanduser().absolute()

def save_root(settings):
    return Path(settings['SaveLocation']).expanduser().absolute() if settings['SaveLocation'] else build_root(settings) / 'Saves'

def protected(path):
    path = Path(path).expanduser()
    if not path.is_absolute():
        raise ValueError('Choose a full folder path.')
    path = path.absolute()
    shared = {Path(path.anchor), Path.home(), Path.home() / 'Desktop', Path.home() / 'Documents',
              Path.home() / 'Downloads', Path('/usr'), Path('/etc'), Path('/var'), Path('/tmp'), Path('/home')}
    if path in shared or '\n' in str(path) or '\r' in str(path):
        raise ValueError('Choose a dedicated EnchantedArmsPC folder, not a shared system or personal folder.')
    for parent in (path, *path.parents):
        if parent.is_symlink():
            raise ValueError('Choose a regular folder without linked parent folders.')
    if path.exists() and not path.is_dir():
        raise ValueError('This location is a file. Choose a folder.')
    return path

def validate_build(path):
    path = protected(path)
    template = resources() / 'project'
    if path == template or template in path.parents:
        raise ValueError('Choose a writable game folder outside the AppImage.')
    if path.exists() and any(path.iterdir()):
        manifest = path / 'enchantedarmsrecomp_manifest.toml'
        owner = path / '.local/linux-build-owner.json'
        if owner.is_file() and not owner.is_symlink() and json.loads(owner.read_text()).get('project') == 'EnchantedArmsPC-linux':
            return path
        if not manifest.is_file() or 'name = "enchantedarmsrecomp"' not in manifest.read_text():
            children = [p for p in path.iterdir() if p.is_dir() and
                        (p / 'enchantedarmsrecomp_manifest.toml').is_file()]
            if len(children) == 1:
                return validate_build(children[0])
            raise ValueError('Choose an empty folder or an existing EnchantedArmsPC project folder.')
    return path

def reserve_build_root(path):
    root = validate_build(path)
    root.mkdir(parents=True, exist_ok=True)
    if (root / '.local').is_symlink():
        raise ValueError('Choose a game folder without linked tool folders.')
    atomic_json(root / '.local/linux-build-owner.json', {'project': 'EnchantedArmsPC-linux'})
    return root

def validate_save(root, destination):
    destination = protected(destination)
    root = Path(root).absolute()
    if destination == root or destination in root.parents:
        raise ValueError('Choose a dedicated save folder, such as Saves inside the game folder.')
    for name in ('.local','.git','bin','src','config','generated','data','renderer','tools','scripts','launcher','linux'):
        reserved = root / name
        if destination == reserved or reserved in destination.parents:
            raise ValueError('Keep saves outside the game\'s program and tool folders.')
    return destination

def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest() if hasattr(hashlib, 'file_digest') else _digest(stream)

def _digest(stream):
    value = hashlib.sha256()
    for block in iter(lambda: stream.read(1024 * 1024), b''):
        value.update(block)
    return value.hexdigest()

def files(root):
    root = Path(root)
    if not root.exists():
        return []
    protected(root)
    pending, result = [root], []
    while pending:
        for path in pending.pop().iterdir():
            if path.is_symlink():
                raise ValueError('A linked file was found in the save folder. No save was overwritten.')
            if path.is_dir():
                pending.append(path)
            elif path.is_file():
                result.append(path)
    return result

def copy_saves(source, destination):
    source, destination = Path(source), protected(destination)
    source_files = files(source)
    if source == destination:
        destination.mkdir(parents=True, exist_ok=True)
        return
    if source in destination.parents or destination in source.parents:
        raise ValueError('Choose a folder outside your current save folder.')
    files(destination)
    for path in source_files:
        target = destination / path.relative_to(source)
        if target.exists() and (not target.is_file() or sha(path) != sha(target)):
            raise ValueError('That folder contains different saved progress. Choose an empty folder or keep your current save location.')
        if any(parent.is_file() for parent in target.parents if parent != destination):
            raise ValueError('A file is blocking the chosen save folder. Choose another folder.')
    destination.mkdir(parents=True, exist_ok=True)
    for path in source_files:
        target = destination / path.relative_to(source)
        if target.exists():
            if sha(path) != sha(target):
                raise ValueError('The destination saves changed. Your originals are unchanged.')
            continue
        target.parent.mkdir(parents=True, exist_ok=True)
        temporary = target.with_name(target.name + '.' + uuid.uuid4().hex + '.partial')
        try:
            shutil.copy2(path, temporary)
            if sha(path) != sha(temporary):
                raise ValueError('Could not verify copied saves. Your originals are unchanged.')
            # Hard-link promotion cannot overwrite progress created during the copy.
            os.link(temporary, target)
        finally:
            temporary.unlink(missing_ok=True)

def save_settings(settings, previous=None):
    settings = normalize(settings)
    root = validate_build(build_root(settings))
    settings['BuildLocation'] = str(root)
    destination = validate_save(root, save_root(settings))
    if previous:
        old = save_root(previous)
        if old.exists() and old != destination:
            copy_saves(old, destination)
    atomic_json(settings_file(), settings)
    if root.is_dir() and (root / 'enchantedarmsrecomp_manifest.toml').exists():
        atomic_json(root / '.local/launcher-linux.json', settings)
    return settings

def prepare_saves(settings):
    root = build_root(settings)
    destination = validate_save(root, save_root(settings))
    legacy = root / '.local/runtime/user'
    marker = root / '.local/linux-saves.json'
    if not marker.exists() and legacy.is_dir() and not destination.exists():
        copy_saves(legacy, destination)
    else:
        files(destination)
        destination.mkdir(parents=True, exist_ok=True)
    atomic_json(marker, {'save_location': str(destination)})
    return destination

def native_path(settings):
    return build_root(settings) / ('bin/linux-' + NATIVE) / 'enchantedarmsrecomp'

def ready(settings):
    root = build_root(settings)
    try:
        receipt = json.loads((root / '.local/installation-linux.json').read_text())
        return (receipt['build'] == NATIVE and native_path(settings).is_file() and
                (root / '.local/game/default.xex').is_file())
    except (OSError, ValueError, KeyError):
        return False

def tool_environment(root):
    tools = Path(root) / '.local/linux-tools'
    environment = dict(os.environ)
    environment['PATH'] = str(tools / 'bin') + os.pathsep + environment.get('PATH', '')
    environment['LD_LIBRARY_PATH'] = ':'.join([str(tools / 'lib'), str(tools / 'sdk/lib'), environment.get('LD_LIBRARY_PATH','')])
    environment['CC'] = str(tools / 'bin/clang')
    environment['CXX'] = str(tools / 'bin/ea-clang++')
    return tools, environment

def game_arguments(settings, log):
    p = normalize(settings)
    args = ['--game_data_root=' + str(build_root(p) / '.local/game'),
        '--user_data_root=' + str(save_root(p)), '--cache_root=' + str(build_root(p) / '.local/runtime/cache'),
        '--log_file=' + str(log), '--log_level=info', '--log_max_file_size_mb=16', '--log_max_files=3',
        '--no-ea_trace_input', '--mnk_mode', '--no-vsync', '--video_mode_refresh_rate=60',
        '--ea_fps_limit=' + str(p['FrameLimit']), '--draw_resolution_scale_x=' + str(p['Scale']),
        '--draw_resolution_scale_y=' + str(p['Scale']), '--resolution_scale=' + str(p['Scale']),
        '--present_effect=' + p['Upscaler'], '--swap_post_effect=' + ('fxaa' if p['PostAA'] == 'ssaa' else p['PostAA']),
        '--anisotropic_override=' + str(p['Anisotropy']), '--ea_texture_lod_bias=' + str(-p['TextureDetail'] * 0.5),
        '--window_width=' + str(p['Width']), '--window_height=' + str(p['Height']),
        '--present_fsr_sharpness_reduction=' + format(2 - p['Sharpness']/50, '.2f'),
        '--present_cas_additional_sharpness=' + format(p['Sharpness']/100, '.2f'),
        '--present_fsr_max_upsampling_passes=4', '--no-vulkan_allow_present_mode_immediate',
        '--no-vulkan_allow_present_mode_mailbox', '--no-vulkan_allow_present_mode_fifo_relaxed']
    for field, flag in [('Fullscreen','fullscreen'), ('KeepAspect','present_letterbox'),
        ('Dither','present_dither'), ('NativeMsaa','native_2x_msaa'), ('MouseCamera','mnk_mouse'),
        ('InvertCameraY','ea_invert_camera_y'), ('SwapTriggers','ea_swap_triggers'), ('FrameTimeGraph','ea_frame_graph')]:
        args.append('--' + ('' if p[field] else 'no-') + flag)
    for field, flag in [('MouseSensitivity','mnk_sensitivity'), ('LeftDeadzone','ea_left_deadzone'),
        ('RightDeadzone','ea_right_deadzone'), ('CameraResponse','ea_camera_response')]:
        args.append('--' + flag + '=' + format(p[field]/100, '.2f'))
    args.append('--ea_pad_map=' + ','.join(map(str, p['PadButtons'])))
    for action, key in enumerate(p['Keys']):
        source = p['PadButtons'][action] if action < 14 else (29-action if p['SwapTriggers'] and action in (14,15) else action)
        args.append('--keybind_' + CONTROL_FLAGS[source] + '=' + key)
    return args

def launch_game(settings):
    if not ready(settings):
        raise ValueError('Build your game from your ISO first.')
    prepare_saves(settings)
    root = build_root(settings)
    log = root / '.local/runtime' / time.strftime('boot-%Y%m%dT%H%M%SZ.log', time.gmtime())
    log.parent.mkdir(parents=True, exist_ok=True)
    tools, environment = tool_environment(root)
    return subprocess.Popen([str(native_path(settings)), *game_arguments(settings, log)],
        cwd=native_path(settings).parent, env=environment, start_new_session=True)

def run(command, root, environment):
    print('RUN: ' + str(command[0]), flush=True)
    subprocess.run([str(p) for p in command], cwd=root, env=environment, check=True)

def build(settings):
    import fcntl
    settings = normalize(settings)
    root = validate_build(build_root(settings))
    iso = Path(settings['Iso']).expanduser()
    if not iso.is_file():
        raise ValueError('Choose an existing Enchanted Arms ISO.')
    validate_save(root, save_root(settings))
    # The read-only source bundle is independent of the player's ISO.
    template = resources() / 'project'
    sys.path.insert(0, str(template / 'tools'))
    from inspect_disc import Disc
    from setup_game import validate_disc, extract_game
    print('STAGE: Checking your game ISO', flush=True)
    disc = Disc(iso)
    try:
        entries = validate_disc(disc)
        root.mkdir(parents=True, exist_ok=True)
        local = root / '.local'
        if local.is_symlink():
            raise ValueError('Choose a game folder without linked tool folders.')
        local.mkdir(exist_ok=True)
        with (local / 'linux-setup.lock').open('a+b') as lock:
            try:
                fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
            except OSError as error:
                raise ValueError('Another setup is using this game folder.') from error
            print('STAGE: Preparing your game folder', flush=True)
            ledger = json.loads((template / 'source-files.json').read_text())
            for name, expected in ledger.items():
                source = template / name
                if sha(source) != expected:
                    raise ValueError('The AppImage source bundle is damaged. Download it again.')
                target = root / name
                if target.is_symlink() or any(parent.is_symlink() for parent in target.parents if parent != root):
                    raise ValueError('A linked project file is blocking setup: ' + name)
                target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(source, target)
            destination = local / 'linux-tools'
            if destination.is_symlink() or (local / 'game').is_symlink():
                raise ValueError('Choose a game folder without linked tools or game data.')
            tools_complete = local / 'linux-tools.complete.json'
            if not tools_complete.is_file():
                print('STAGE: Preparing the bundled Linux build tools', flush=True)
                shutil.copytree(resources() / 'tools', destination, dirs_exist_ok=True, symlinks=True)
            tools, environment = tool_environment(root)
            for relative in ('bin/ea-clang++', 'bin/clang', 'bin/cmake', 'bin/ninja', 'sdk/bin/rexglue'):
                if not (tools / relative).is_file():
                    raise ValueError('The bundled Linux build tools are incomplete. Retry setup.')
            if not tools_complete.is_file():
                run([tools/'sdk/bin/rexglue', '--version'], root, environment)
                run([tools/'bin/ea-clang++', '--version'], root, environment)
                atomic_json(tools_complete, {'version': VERSION})
            extract_game(disc, entries, local)
            print('STAGE: Generating native code from your game', flush=True)
            run([tools/'sdk/bin/rexglue', 'codegen', root/'enchantedarmsrecomp_manifest.toml'], root, environment)
            print('STAGE: Building Enchanted Arms for Linux', flush=True)
            directory = local / 'build/linux'
            compiler_flags = '-DREX_HAS_FIDELITYFX_SDK=1 -march=x86-64-v2'
            run([tools/'bin/cmake', '-S', root, '-B', directory, '-G', 'Ninja', '-DCMAKE_BUILD_TYPE=Release',
                '-DCMAKE_PREFIX_PATH=' + str(tools/'sdk'), '-DCMAKE_CXX_COMPILER=' + str(tools/'bin/ea-clang++'),
                '-DZLIB_INCLUDE_DIR=' + str(tools/'sysroot/usr/include'),
                '-DZLIB_LIBRARY=' + str(tools/'sysroot/usr/lib/x86_64-linux-gnu/libz.so'),
                '-DCMAKE_CXX_FLAGS=' + compiler_flags, '-DCMAKE_EXE_LINKER_FLAGS=-fuse-ld=lld',
                '-DEA_SPATIAL_FSR=ON'], root, environment)
            run([tools/'bin/cmake', '--build', directory, '--parallel', str(settings['Jobs'])], root, environment)
            staged = native_path(settings).parent
            staged.mkdir(parents=True, exist_ok=True)
            shutil.copy2(directory / 'enchantedarmsrecomp', staged / 'enchantedarmsrecomp')
            for path in (tools/'sdk/lib').glob('*.so*'):
                shutil.copy2(path, staged / path.name)
            shutil.copy2(root/'data/gamecontrollerdb.txt', staged/'gamecontrollerdb.txt')
            prepare_saves(settings)
            atomic_json(local/'installation-linux.json', dict(build=NATIVE, launcher=VERSION,
                xex_sha256='79e95ea830119123c2fd9c7b08fb890c4cf40cc6b8422b3568a820b6b3779f22',
                completed_utc=time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime())))
            atomic_json(local/'launcher-linux.json', settings)
    finally:
        disc.stream.close()
    print('STAGE: Ready to play', flush=True)

def desktop_shortcut(settings):
    image = appimage()
    if image is None or not image.is_file():
        raise ValueError('Open the AppImage to create its desktop shortcut.')
    desktop = Path.home() / 'Desktop'
    if shutil.which('xdg-user-dir'):
        selected = subprocess.check_output(['xdg-user-dir','DESKTOP'], text=True).strip()
        if selected:
            desktop = Path(selected)
    desktop.mkdir(parents=True, exist_ok=True)
    icon = settings_file().parent / 'icon.png'
    if not icon.is_file():
        icon.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(resources()/'icon.png', icon)
    escaped = str(image).replace('\\','\\\\').replace('"','\\"').replace('`','\\`').replace('$','\\$').replace('%','%%')
    target = desktop / 'EnchantedArmsPC.desktop'
    target.write_text('[Desktop Entry]\nType=Application\nName=EnchantedArmsPC\nExec="' + escaped +
        '"\nIcon=' + str(icon) + '\nTerminal=false\nCategories=Game;\n', encoding='utf-8')
    target.chmod(0o755)
    return target
