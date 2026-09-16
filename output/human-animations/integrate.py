from pathlib import Path
import re, shutil, uuid, hashlib

ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'Assets/Art/Animations/HumanMelee'
RES=ROOT/'Assets/Resources/CombatPresentation'
ART.mkdir(parents=True,exist_ok=True);RES.mkdir(parents=True,exist_ok=True)
def meta(path, folder=False):
    p=Path(str(path)+'.meta')
    if not p.exists():p.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n'+('folderAsset: yes\n' if folder else ''),encoding='utf-8')
    return re.search(r'guid: (\w+)',p.read_text()).group(1)
def guid(path):return meta(path)
def ref(path,id=7400000):return '{fileID: '+str(id)+', guid: '+guid(path)+', type: 2}'
def read(path):
    text=path.read_text(encoding='utf-8-sig')
    result={k:v for k,v in re.findall(r'^  (\w+): ?(.*)$',text,re.M)}
    for k in ['passive','usesSwordCombo']:result[k]=int(result.get(k,'0'))
    result['actions']=[{'ability':{'guid':g}} for g in re.findall(r'^  - ability: .*guid: (\w+)',text,re.M)]
    for k in ['abilities','repertoire']:
        m=re.search(r'^  '+k+r':\n((?:  - .*\n)*)',text,re.M)
        result[k]=[{'guid':g} for g in re.findall(r'guid: (\w+)',m[1])] if m else []
    return result
meta(ART,True);meta(RES,True)
clips={}
for p in (ROOT/'.human-animation-bake/Assets/Baked').glob('*.anim'):
    destination=(RES if 'CombatDamage' in p.name else ART)/p.name
    shutil.copyfile(p,destination)
    meta(destination)
    clips[p.stem]=destination

for rig in ['Player','Goblin']:
    source=clips['Human_'+rig+'_CombatDamage01'].read_text()
    paths=set(re.findall(r'^    path: (.+)$',source,re.M))
    for p in list(paths):
        parts=p.split('/')
        paths.update('/'.join(parts[:i]) for i in range(1,len(parts)))
    mask=RES/(rig+'UpperBody.mask')
    body='''%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!319 &31900000
AvatarMask:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: '''+rig+'''UpperBody
  m_Mask: 00000000010000000100000000000000000000000100000001000000010000000100000000000000000000000100000001000000
  m_Elements:
  - m_Path: 
    m_Weight: 0
'''
    for p in sorted(paths):body+=f'  - m_Path: {p}\n    m_Weight: {1 if "/Spine" in p or "/Chest" in p else 0}\n'
    mask.write_text(body,encoding='utf-8');meta(mask)

assets={}
for p in (ROOT/'Assets/Data').rglob('*.asset'):
    if Path(str(p)+'.meta').exists():assets[guid(p)]=p

report=[]
def clipref(name):return ref(clips['Human_Player_'+name])
families={'OneHandSwordCombatAnimations':'OneHandSword','OneHandSwordAnimations':'OneHandSword','DualSwordsCombatAnimations':'DualSwords','SwordShieldCombatAnimations':'SwordShield'}
special={
    'SwordLunge':'AttackPolearm01','SwordParry':'CombatIdle1H01','SwordSpin':'Attack1H01_R',
    'GuardBreaker':'Attack2H01','SideStep':'CombatIdle1H01','TwoTimes':'CombatIdle1H01',
    'DualFlurry':'DualSequence','DualWhirlwind':'DualSequence','DualCross':'DualCross','OpenWound':'Attack1H01_R',
    'ShieldBash':'AttackShield01','ShieldCharge':'AttackShield01','FirmGuard':'CombatIdle1H01',
    'LowSweep':'Attack1H01_R','ThrownBuckler':'AttackShield01'
}
for name,family in families.items():
    path=ROOT/'Assets/Data/WeaponFamilies'/(name+'.asset')
    data=read(path);familydata=read(path.parent/(family+'.asset'))
    abilities=[]
    for entry in [a.get('ability',{}) for a in data['actions']]+familydata.get('abilities',[])+familydata.get('repertoire',[]):
        g=entry.get('guid')
        if g and g not in abilities:abilities.append(g)
    header=path.read_text().split('  actions:')[0]
    mask=ref(RES/'PlayerUpperBody.mask',31900000)
    header=re.sub(r'^  actionMask:.*$', '  actionMask: '+mask, header, flags=re.M)
    header=re.sub(r'^  (stance|stanceMask):.*\n','',header,flags=re.M)
    header+='  actions:\n'
    for g in abilities:
        ability=read(assets[g]);aname=ability.get('abilityId') or assets[g].stem
        if ability.get('passive',0):continue
        basic=bool(ability.get('usesSwordCombo'))
        selected=special.get(aname,'Attack1H01_R')
        header+='  - ability: {fileID: 11400000, guid: '+g+', type: 2}\n'
        header+='    clip: '+('{fileID: 0}' if basic else clipref(selected))+'\n'
        header+='    maskMode: 0\n    customMask: {fileID: 0}\n'
        if basic:
            sequence=['Attack1H01_R','Attack1H01_L' if family=='DualSwords' else 'Attack1H01_R','Attack1H01_R']
            header+='    comboClips:\n'+''.join('    - '+clipref(c)+'\n' for c in sequence)
        else:header+='    comboClips: []\n'
        header+='    activeStartsAt: 0.3\n    recoveryStartsAt: 0.65\n    blendSeconds: 0.08\n'
        if basic: header+='    torsoUprightDegrees: 12\n'
        report.append(f'{family}: {aname} -> '+(' / '.join(sequence) if basic else selected))
    path.write_text(header,encoding='utf-8')

# Goblin's actual controller gains the imported hit and idle even outside attack playback.
path=ROOT/'Assets/Art/Animations/GoblinConcept/Goblin.controller'
parts=re.split(r'(?=^--- !u!)',path.read_text(),flags=re.M)
for i,part in enumerate(parts):
    for state,clip in [('Hit','CombatDamage01'),('Idle','CombatIdle1H01'),('Attack','Attack1H01_R')]:
        if re.search(r'^  m_Name: '+state+r'$',part,re.M):
            parts[i]=re.sub(r'^  m_Motion:.*$', '  m_Motion: '+ref(clips['Human_Goblin_'+clip]),part,flags=re.M)
path.write_text(''.join(parts),encoding='utf-8')

# Explicit attack bindings otherwise supersede the controller's attack.
path=ROOT/'Assets/Data/Enemies/BaseGoblin.asset'
text=path.read_text()
text=re.sub(r'(?m)^(      clip:).+$',lambda m:m[1]+' '+ref(clips['Human_Goblin_Attack1H01_R']),text)
text=re.sub(r'(?m)^(      compatible(?:Clip|Source):).+$',r'\1 {fileID: 0}',text)
path.write_text(text,encoding='utf-8')

for p in [ROOT/'Assets/Scripts/Gameplay/Player/Presentation/WeaponAnimationSet.cs']:
    assert p.exists()
(ROOT/'output/human-animations/mapping.txt').write_text('\n'.join(report),encoding='utf-8')
shutil.copyfile(ROOT/'.human-animation-bake/bake-result.txt',ROOT/'output/human-animations/bake-result.txt')
print(f'Integrated {len(clips)} clips, {len(report)} bindings, two upper-body masks.')
