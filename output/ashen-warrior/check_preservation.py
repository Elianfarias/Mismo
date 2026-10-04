from pathlib import Path
import re

root=Path(__file__).resolve().parents[2]
def blocks(path):
    text='\n'.join(line.rstrip() for line in path.read_text(encoding='utf-8-sig').splitlines())+'\n'
    pieces=re.split(r'^--- !u!(\d+) &(-?\d+)\n',text,flags=re.M)
    return {pieces[i+1]:(int(pieces[i]),pieces[i+2]) for i in range(1,len(pieces),3)}

before=blocks(root/'output/ashen-warrior/Player.before.prefab.txt')
after=blocks(root/'Assets/Art/Prefabs/Player/Player.prefab')
hat=(root/'Assets/Scripts/Gameplay/Player/Presentation/MageHatMotion.cs.meta').read_text()
hat_guid=re.search(r'guid: (\w+)',hat)[1]
report=[]
for ident,(kind,body) in before.items():
    if kind==114 and hat_guid not in body:
        assert ident in after, f'Existing gameplay component removed: {ident}'
        assert after[ident]==(kind,body), f'Existing gameplay settings changed: {ident}'
        report.append('PASS gameplay component preserved: '+ident)
    if kind==95:
        assert ident in after,'Animator object replaced'
        strip=lambda s:re.sub(r'  m_Avatar:.*\n','',s)
        assert strip(body)==strip(after[ident][1]),'Animator settings changed beyond avatar'
        report.append('PASS Animator identity, controller and settings preserved; only avatar changed.')
    if kind==143:
        assert after[ident]==(kind,body),'CharacterController changed'
        report.append('PASS CharacterController preserved.')
(root/'output/ashen-warrior/prefab-preservation.txt').write_text('\n'.join(report)+'\n')
print('\n'.join(report))
