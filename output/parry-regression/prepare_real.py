from pathlib import Path
import re, shutil
root=Path(__file__).resolve().parents[2]
project=root/'.validation/ProgressionImprovements'
index={}
for p in (root/'Assets').rglob('*.meta'):
    m=re.search(r'^guid: (\w+)',p.read_text(encoding='utf-8-sig',errors='ignore'),re.M)
    if m:index[m[1]]=Path(str(p)[:-5])
types={}
for line in (root/'output/parry-regression/scripts.tsv').read_text().splitlines():
    name,guid,fid=line.split('\t');types[name]=(guid,fid)
byname={name.split('.')[-1]:(guid,fid) for name,(guid,fid) in types.items()}
pending=[root/p for p in ['Assets/Art/Prefabs/Player/Player.prefab','Assets/Art/Prefabs/Enemies/Goblin.prefab','Assets/Data/Weapons/Sword/Sword.asset','Assets/Data/Player/DefaultMovementSettings.asset','Assets/Art/Shaders/CombatParticles.shader']]
seen=set(); total=0;missing=[]
while pending:
    source=pending.pop()
    if source in seen or not source.is_file():continue
    seen.add(source)
    if source.suffix in ('.cs','.asmdef','.asmref'):continue
    raw=source.read_bytes();meta=Path(str(source)+'.meta').read_text(encoding='utf-8-sig')
    text=raw.decode('utf-8-sig',errors='ignore') if raw[:5]==b'%YAML' or source.suffix in ('.mat','.controller','.anim','.asset','.prefab','.shader') else ''
    for guid in set(re.findall(r'guid: ([0-9a-f]{32})',text+'\n'+meta)):
        if guid in index:pending.append(index[guid])
    if text.startswith('%YAML'):
        def remap(m):
            old=index.get(m[1]); mapped=byname.get(old.stem) if old else None
            if m[1]=='62899f850307741f2a39c98a8b639597':mapped=types['UnityEngine.InputSystem.PlayerInput']
            if mapped:return 'm_Script: {fileID: '+mapped[1]+', guid: '+mapped[0]+', type: 3}'
            missing.append((str(source.relative_to(root)),str(old)));return m[0]
        text=re.sub(r'm_Script: \{fileID: 11500000, guid: ([0-9a-f]{32}), type: 3\}',remap,text)
        raw=text.encode('utf-8')
    dest=project/source.relative_to(root);dest.parent.mkdir(parents=True,exist_ok=True)
    dest.write_bytes(raw);Path(str(dest)+'.meta').write_text(meta,encoding='utf-8');total+=len(raw)
print('Copied dependency closure:',len(seen),'assets,',round(total/1024/1024,1),'MiB. Unresolved scripts:',missing)
