from pathlib import Path
import hashlib
import re
import zipfile

root=Path(__file__).resolve().parents[2]
def blocks(path):
    text='\n'.join(line.rstrip() for line in path.read_text(encoding='utf-8-sig').splitlines())+'\n'
    parts=re.split(r'^--- !u!(\d+) &(-?\d+)\n',text,flags=re.M)
    return {parts[i+1]:(int(parts[i]),parts[i+2]) for i in range(1,len(parts),3)}

before=blocks(root/'output/ninja-frog/Player.before.prefab.txt')
after=blocks(root/'Assets/Art/Prefabs/Player/Player.prefab')
motion=[]
for name in ['MageHatMotion','WarriorCapeMotion','WarriorPlumeMotion','FrogScarfMotion']:
    meta=(root/f'Assets/Scripts/Gameplay/Player/Presentation/{name}.cs.meta').read_text()
    motion.append(re.search(r'guid: (\w+)',meta)[1])
report=[]
for ident,(kind,body) in before.items():
    if kind==114 and not any(guid in body for guid in motion):
        assert after.get(ident)==(kind,body),f'Existing gameplay settings changed: {ident}'
        report.append('PASS gameplay component preserved: '+ident)
    elif kind==95:
        strip=lambda s:re.sub(r'  m_Avatar:.*\n','',s)
        assert ident in after and strip(body)==strip(after[ident][1]),'Animator identity/controller/settings changed'
        report.append('PASS Animator identity, controller and settings; only avatar changed.')
    elif kind==143:
        assert after.get(ident)==(kind,body),'CharacterController changed'
        report.append('PASS CharacterController preserved.')
with zipfile.ZipFile(root/'Assets/Art/Source/Characters/NinjaFrog/NinjaFrog_Source.zip') as source:
    assert source.testzip() is None
    assert source.read('build_ninja_frog.py')==(root/'Assets/Scripts/Gameplay/Player/Editor/build_ninja_frog.py').read_bytes()
report.append('PASS source archive integrity and generator match.')
fbx=(root/'Assets/Art/FBX/Characters/NinjaFrog.fbx').read_bytes()
export=(root/'output/ninja-frog/NinjaFrog_TPose.fbx').read_bytes()
assert hashlib.sha256(fbx).digest()==hashlib.sha256(export).digest()
report.append('PASS T-pose export matches imported FBX.')
(root/'output/ninja-frog/prefab-preservation.txt').write_text('\n'.join(report)+'\n')
print('\n'.join(report))
