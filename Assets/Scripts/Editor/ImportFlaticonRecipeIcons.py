"""Import exact Flaticon Uicons glyphs from their official npm distribution.

Usage: python ImportFlaticonRecipeIcons.py path/to/flaticon-uicons.tgz
Then run RenderFlaticonRecipeIcons.cjs with Node and Playwright available.
No icon silhouette is drawn or redesigned by this script.
"""
from pathlib import Path
import hashlib
import json
import re
import tarfile
import sys
import uuid

ROOT=next(p for p in Path(__file__).resolve().parents if (p/'ProjectSettings/ProjectVersion.txt').exists())
OUT=ROOT/'Assets/Art/UI/Flaticon/Recipes'
FONT=ROOT/'Assets/Art/Fonts/Flaticon'
OUT.mkdir(parents=True,exist_ok=True); FONT.mkdir(parents=True,exist_ok=True)

def meta(path,folder=False):
    dst=Path(str(path)+'.meta')
    if not dst.exists():
        extra='folderAsset: yes\n' if folder else ''
        dst.write_text(f'fileFormatVersion: 2\nguid: {uuid.uuid4().hex}\n{extra}DefaultImporter:\n',encoding='utf-8')

with tarfile.open(sys.argv[1]) as archive:
    package=json.loads(archive.extractfile('package/package.json').read())
    if package['name']!='@flaticon/flaticon-uicons' or package['version']!='3.3.1':
        raise ValueError('Expected official @flaticon/flaticon-uicons 3.3.1')
    css=archive.extractfile('package/css/solid/rounded.css').read().decode()
    font_name=re.search(r'\.\./(uicons-solid-rounded-[\w]+\.woff)\)',css).group(1)
    font_data=archive.extractfile('package/css/'+font_name).read()
    font_path=FONT/'uicons-solid-rounded-3.3.1.woff'
    font_path.write_bytes(font_data); meta(font_path)
    license_path=ROOT/'Assets/Documentation/FlaticonUicons-LICENSE.txt'
    license_path.write_bytes(archive.extractfile('package/LICENSE').read()); meta(license_path)

new_names={'IconHandcraft':'hand-paper','IconStation':'tools','IconCraft':'hammer',
           'IconRecipes':'book-bookmark','IconLocation':'marker','IconTree':'tree',
           'IconLock':'lock','IconCheck':'check'}
icons=[]
for key,name in new_names.items():
    match=re.search(r'\.fi-sr-'+re.escape(name)+r':before\{content:"\\([0-9a-f]+)"\}',css)
    if not match: raise ValueError('Missing official glyph: '+name)
    icons.append({'key':key,'name':'fi-sr-'+name,'codepoint':int(match.group(1),16),
                  'path':(OUT/('fi-sr-'+name+'.png')).relative_to(ROOT).as_posix(),
                  'source':'https://www.flaticon.com/uicons','style':'solid-rounded'})

shared=ROOT/'Assets/Art/UI/WhiteAndBlackGUI'
inventory=(ROOT/'Assets/Data/UI/InventoryUIIcons.asset').read_text(encoding='utf-8-sig')
for key,field in [('IconAll','all'),('IconWeapons','weapons'),('IconConsumables','consumables'),('IconMaterials','materials'),('IconClose','close')]:
    guid=re.search(r'^  '+field+r': \{fileID: \d+, guid: (\w+)',inventory,re.M).group(1)
    matches=[p for p in shared.glob('*.meta') if '\nguid: '+guid in p.read_text(encoding='utf-8-sig')]
    if len(matches)!=1: raise ValueError('Cannot resolve existing inventory icon: '+field)
    path=Path(str(matches[0])[:-5])
    icons.append({'key':key,'path':path.relative_to(ROOT).as_posix(),'inventoryField':field,'guid':guid})
for key,name in [('IconArrowUp','arrow_up'),('IconArrowDown','arrow_down')]:
    path=shared/(name+'.png')
    if not path.exists(): raise ValueError('Missing existing navigation icon: '+str(path))
    icons.append({'key':key,'path':path.relative_to(ROOT).as_posix(),'reused':True})

data={'package':'@flaticon/flaticon-uicons','version':package['version'],
      'documentation':'https://www.flaticon.com/uicons/get-started',
      'download':'https://registry.npmjs.org/@flaticon/flaticon-uicons/-/flaticon-uicons-3.3.1.tgz',
      'archiveSha256':hashlib.sha256(Path(sys.argv[1]).read_bytes()).hexdigest(),
      'font':font_path.relative_to(ROOT).as_posix(),
      'fontSha256':hashlib.sha256(font_data).hexdigest(),
      'attribution':'Uicons by Flaticon — https://www.flaticon.com/uicons',
      'icons':icons}
manifest=OUT/'Sources.json'
manifest.write_text(json.dumps(data,indent=2,ensure_ascii=False)+'\n',encoding='utf-8'); meta(manifest)
for p in [OUT,OUT.parent,FONT]: meta(p,True)
meta(Path(__file__))
print('Imported official Flaticon source font; mapped 8 missing glyphs and 7 existing icons.')
