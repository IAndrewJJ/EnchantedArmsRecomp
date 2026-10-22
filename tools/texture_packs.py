"""Install local, preconverted DDS/XPR texture packs without changing disc files."""
import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import struct
import sys
import uuid
import zipfile
sys.path.insert(0,str(Path(__file__).resolve().parent))
from local_paths import filesystem_path

GAME_HASH = '79e95ea830119123c2fd9c7b08fb890c4cf40cc6b8422b3568a820b6b3779f22'
TOKEN = re.compile(r'^[a-z0-9][a-z0-9-]{0,47}-[0-9a-f]{16}$')
MAX_FILE = 64*1024*1024
MAX_TOTAL = 1024*1024*1024


def digest(path):
    with path.open('rb') as source:
        return hashlib.file_digest(source, 'sha256').hexdigest()


def relative_path(name):
    if not isinstance(name, str) or not name or len(name)>200:
        raise ValueError('Invalid texture path')
    path = PurePosixPath(name)
    reserved = {'con','prn','aux','nul'} | {f'{p}{n}' for p in ('com','lpt') for n in range(1,10)}
    if (path.is_absolute() or path.as_posix()!=name or '\\' in name or
        any(p in {'.','..'} or p.endswith((' ','.')) or
            any(c in p for c in ':*?"<>|\x00') or p.split('.')[0].lower() in reserved for p in path.parts)):
        raise ValueError('Unsafe texture path')
    return path


def inside(root, name):
    relative_path(name)
    result = root/name
    if not result.resolve().is_relative_to(root.resolve()):
        raise ValueError('Texture path escapes its folder')
    return result


def texture_header(data, extension):
    if extension=='.xpr':
        if len(data)<16 or data[:4]!=b'XPR2':
            raise ValueError('Expected a converted Xbox 360 XPR2 bundle')
        header, body = struct.unpack_from('>II', data, 4)
        if header<4 or header+body+12!=len(data):
            raise ValueError('XPR2 bundle size is invalid')
        return data[:4]
    if len(data)<128 or data[:4]!=b'DDS ' or struct.unpack_from('<I',data,4)[0]!=124:
        raise ValueError('Expected a converted DDS texture')
    height,width = struct.unpack_from('<II',data,12)
    mips = struct.unpack_from('<I',data,28)[0] or 1
    if not 1<=width<=16384 or not 1<=height<=16384 or not 1<=mips<=15:
        raise ValueError('DDS dimensions or mip count are invalid')
    if struct.unpack_from('<I',data,76)[0]!=32:
        raise ValueError('DDS pixel format is invalid')
    flags, fourcc, bits = struct.unpack_from('<I4sI',data,80)
    if flags&4:
        if fourcc not in (b'DXT1',b'DXT3',b'DXT5'):
            raise ValueError('Only legacy DXT1/3/5 or RGB DDS textures are supported')
        size=lambda w,h: max(1,(w+3)//4)*max(1,(h+3)//4)*(8 if fourcc==b'DXT1' else 16)
    elif flags&0x40 and bits in (8,16,24,32):
        size=lambda w,h: w*h*(bits//8)
    else:
        raise ValueError('Unsupported DDS pixel format')
    minimum=128
    for level in range(mips):
        minimum+=size(max(1,width>>level),max(1,height>>level))
    if len(data)<minimum:
        raise ValueError('DDS pixel data is truncated')
    # Preserve the game's format/channel masks and resource type. Dimensions
    # may increase; PNG/JPEG and arbitrary shader/model files are not accepted.
    return data[76:108]+data[112:128]


def validate_pack(archive, game):
    if archive.stat().st_size>MAX_TOTAL:
        raise ValueError('Texture pack exceeds 1 GB')
    if digest(game/'default.xex')!=GAME_HASH:
        raise ValueError('Texture packs require the supported Enchanted Arms disc build')
    with zipfile.ZipFile(archive) as source:
        members=source.infolist()
        names=[m.filename for m in members]
        if len(members)>4097 or len(set(n.casefold() for n in names))!=len(names):
            raise ValueError('Duplicate or excessive texture pack entries')
        for member in members:
            relative_path(member.filename)
            if member.is_dir() or member.file_size>MAX_FILE or member.flag_bits&1:
                raise ValueError('Unsupported texture pack entry')
        if sum(m.file_size for m in members)>MAX_TOTAL:
            raise ValueError('Expanded texture pack exceeds 1 GB')
        if source.getinfo('pack.json').file_size>1024*1024:
            raise ValueError('Texture pack manifest is too large')
        pack=json.loads(source.read('pack.json'))
        if (pack.get('schema')!=1 or pack.get('game_xex_sha256')!=GAME_HASH or
            not re.fullmatch(r'[a-z0-9][a-z0-9-]{0,47}',pack.get('id','')) or
            not isinstance(pack.get('name'),str) or not 1<=len(pack['name'])<=80 or
            not isinstance(pack.get('files'),list) or not 1<=len(pack['files'])<=4096):
            raise ValueError('Unsupported texture pack manifest')
        targets=set();payloads=set()
        for item in pack['files']:
            target=relative_path(item['target']);payload=relative_path(item['file'])
            if target.suffix.lower() not in {'.dds','.xpr'} or payload.suffix.lower()!=target.suffix.lower():
                raise ValueError('Packs may replace only DDS textures or XPR bundles')
            if target.as_posix().casefold() in targets or payload.as_posix().casefold() in payloads or payload.as_posix()=='pack.json':
                raise ValueError('Duplicate texture replacement')
            targets.add(target.as_posix().casefold());payloads.add(payload.as_posix().casefold())
            original=inside(game,item['target'])
            if not original.is_file() or original.stat().st_size>MAX_FILE or digest(original)!=item['original_sha256']:
                raise ValueError('Original texture does not match: '+item['target'])
            data=source.read(item['file'])
            if hashlib.sha256(data).hexdigest()!=item['sha256']:
                raise ValueError('Texture checksum does not match: '+item['target'])
            if texture_header(data,target.suffix.lower())!=texture_header(original.read_bytes(),target.suffix.lower()):
                raise ValueError('Texture format must match the original: '+item['target'])
        if set(names)!={'pack.json'}|{i['file'] for i in pack['files']}:
            raise ValueError('Texture pack contains unlisted files')
    return pack


def install(root, archive):
    root,archive=filesystem_path(root),filesystem_path(archive)
    game=root/'.local/game'
    pack=validate_pack(archive,game)
    checksum=digest(archive)
    token=pack['id']+'-'+checksum[:16]
    destination=root/'.local/texture-packs'/token
    if destination.exists():
        if digest(destination/'pack.zip')!=checksum:
            raise ValueError('Installed texture pack changed; reinstall in a fresh folder')
        return {'token':token,'name':pack['name']}
    temporary=destination.with_name(token+'.partial-'+uuid.uuid4().hex)
    temporary.mkdir(parents=True)
    # Keep a single verified archive rather than trusting editable loose files.
    shutil.copyfile(archive,temporary/'pack.zip')
    if digest(temporary/'pack.zip')!=checksum:
        raise ValueError('Texture pack changed during installation')
    (temporary/'receipt.json').write_text(json.dumps({'token':token,'name':pack['name'],'sha256':checksum}),encoding='utf-8')
    temporary.rename(destination)
    return {'token':token,'name':pack['name']}


def prepare(root, token):
    root=filesystem_path(root)
    if not TOKEN.fullmatch(token):
        raise ValueError('Invalid texture pack selection')
    pack_root=root/'.local/texture-packs'/token
    receipt=json.loads((pack_root/'receipt.json').read_text(encoding='utf-8'))
    archive=pack_root/'pack.zip'
    if digest(archive)!=receipt['sha256'] or not token.endswith(receipt['sha256'][:16]):
        raise ValueError('Installed texture pack checksum changed')
    game=root/'.local/game'
    pack=validate_pack(archive,game)
    overlay=root/'.local/texture-overlays'/token
    if (overlay/'ready.json').exists():
        ready=json.loads((overlay/'ready.json').read_text())
        if ready.get('sha256')==receipt['sha256'] and ready.get('game_root')==str(game.resolve()):
            if all(digest(inside(overlay/'game',i['target']))==i['sha256'] for i in pack['files']):
                return {'game_root':str(overlay/'game')}
        raise ValueError('Prepared texture files changed; choose Original or reinstall the pack')
    temporary=overlay.with_name(token+'.partial-'+uuid.uuid4().hex)
    shadow=temporary/'game';shadow.mkdir(parents=True)
    targets={i['target'].casefold() for i in pack['files']}
    for original in game.rglob('*'):
        if not original.is_file():
            continue
        name=original.relative_to(game).as_posix()
        if name.casefold() in targets:
            continue
        destination=inside(shadow,name);destination.parent.mkdir(parents=True,exist_ok=True)
        if not original.resolve().is_relative_to(game.resolve()):
            raise ValueError('Base game contains an external file link')
        try:
            os.link(original,destination)
        except OSError as error:
            raise ValueError('Texture overlays need an NTFS installation on the same drive as the extracted game. Original textures are still available.') from error
    with zipfile.ZipFile(archive) as source:
        for item in pack['files']:
            destination=inside(shadow,item['target']);destination.parent.mkdir(parents=True,exist_ok=True)
            destination.write_bytes(source.read(item['file']))
    (temporary/'ready.json').write_text(json.dumps({'sha256':receipt['sha256'],'game_root':str(game.resolve())}))
    temporary.rename(overlay)
    return {'game_root':str(overlay/'game')}


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command',choices=['install','prepare'])
    parser.add_argument('--root',type=Path,default=Path(__file__).resolve().parents[1])
    parser.add_argument('--archive',type=Path)
    parser.add_argument('--token')
    args=parser.parse_args();root=filesystem_path(args.root).resolve()
    if not (root/'enchantedarmsrecomp_manifest.toml').is_file():
        raise ValueError('Not an EnchantedArmsPC installation')
    result=install(root,args.archive) if args.command=='install' else prepare(root,args.token)
    print(json.dumps(result))


if __name__=='__main__':
    try:
        main()
    except Exception as error:
        print(json.dumps({'error':str(error)}))
        sys.exit(1)
