"""Update the two unpacked character prefabs without rebuilding gameplay components."""
from pathlib import Path
import re, math
root=Path(__file__).resolve().parents[2]
folder=root/'Assets/Art/Animations/VoxelMaterials'
colors={'Cloth___slate_blue':(.065,.075,.17),'Leather___warm_brown':(.25,.12,.042),'Trim___muted_brass':(.8,.43,.075),'Armor___charcoal_steel':(.26,.29,.32),'Scarf___sand':(.36,.22,.105),'Eyes___pale_violet':(.75,.25,1)}
def srgb(x):return 12.92*x if x<=.0031308 else 1.055*x**(1/2.4)-.055
for name,c in colors.items():
 p=folder/(name+'.mat');text=p.read_text();r,g,b=map(srgb,c)
 text=re.sub(r'- _Color: \{[^}]+\}',f'- _Color: {{r: {r:.7f}, g: {g:.7f}, b: {b:.7f}, a: 1}}',text)
 if name.startswith('Eyes'):
  text=text.replace('m_ValidKeywords: []','m_ValidKeywords:\n  - _EMISSION')
  text=re.sub(r'- _EmissionColor: \{[^}]+\}','- _EmissionColor: {r: 1.3, g: 0.4, b: 2, a: 1}',text)
 p.write_text(text)
guids={n:re.search(r'guid: (\w+)',(folder/(n+'.mat.meta')).read_text())[1] for n in colors}
for rel in ['Assets/Prefabs/Player/Player.prefab','Assets/Prefabs/Voxel/VoxelAdventurer.prefab']:
 p=root/rel;text=p.read_text();blocks=re.split(r'(?=--- !u!)',text)
 names={}
 for block in blocks:
  if block.startswith('--- !u!1 &'):
   names[re.search(r'&(-?\d+)',block)[1]]=re.search(r'  m_Name: (.*)',block)[1]
 for i,block in enumerate(blocks):
  if not block.startswith('--- !u!137 &'):continue
  name=names.get(re.search(r'm_GameObject: \{fileID: (-?\d+)',block)[1])
  material={'Hood back':'Leather___warm_brown','Hood upper step':'Trim___muted_brass','Shoulder edge L':'Armor___charcoal_steel','Shoulder edge R':'Armor___charcoal_steel'}.get(name)
  if material:
   block=re.sub(r'(m_Materials:\n  - \{fileID: 2100000, guid: )\w+',r'\g<1>'+guids[material],block)
  if name in ['Hood crown','Voxel_Base_GameReady']:
   block=re.sub(r'm_AABB:\n    m_Center: \{[^}]+\}\n    m_Extent: \{[^}]+\}', 'm_AABB:\n    m_Center: {x: 0, y: 0.013, z: 0}\n    m_Extent: {x: 0.016, y: 0.016, z: 0.016}',block)
  blocks[i]=block
 p.write_text(''.join(blocks))
print('MAGE_PREFABS_MATERIALS_OK')
