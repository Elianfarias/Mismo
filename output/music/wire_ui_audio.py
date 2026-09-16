from pathlib import Path
import uuid
root=Path(__file__).resolve().parents[2]
def edit(name, pairs):
    path=root/name
    text=path.read_text(encoding='utf-8-sig')
    for old,new in pairs:
        assert old in text, (name,old)
        text=text.replace(old,new)
    path.write_text(text,encoding='utf-8')

base='Assets/Scripts/Gameplay/Player/'
for name in ['Presentation/PlayerHUD.cs','Presentation/QuietFantasyUI.cs','Equipment/Inventory/InventoryPanel.cs','World/GatheringPlayer.cs']:
    edit(base+name,[('GUI.Button(rect,','GameAudio.Button(rect,')] if name!='World/GatheringPlayer.cs' else [('GUI.Button(new Rect(','GameAudio.Button(new Rect(')])
edit(base+'Presentation/WorldMapPanel.cs',[
    ('active=this;IsOpen=true;Recenter();','active=this;IsOpen=true;GameAudio.Play(GameSound.MenuOpen);Recenter();'),
    ('if(!IsOpen)return;IsOpen=false;','if(!IsOpen)return;GameAudio.Play(GameSound.MenuClose);IsOpen=false;')])
edit(base+'World/GatheringPlayer.cs',[
    ('if(station==null)return;station=null;','if(station==null)return;GameAudio.Play(GameSound.MenuClose);station=null;'),
    ('{station=nearby;oldLock=', '{station=nearby;GameAudio.Play(GameSound.MenuOpen);oldLock=')])
edit(base+'Equipment/Inventory/PlayerInventory.cs',[
    ('materialDefinitions[id].displayName, false);','materialDefinitions[id].displayName, false, GameSound.ResourceCollected);'),
    ('minimumLevel,false);','minimumLevel,false,GameSound.RegionDiscovered);'),
    ('Commit(next,"Equipamiento guardado.")','Commit(next,"Equipamiento guardado.",true,GameSound.ItemEquipped)'),
    ('Commit(next, "Equipamiento guardado.")','Commit(next, "Equipamiento guardado.",true,GameSound.ItemEquipped)'),
    ('como botín en el suelo.",false);','como botín en el suelo.",false,GameSound.WeaponDrop);'),
    ('T2 Guardián. [I] Inventario", false);','T2 Guardián. [I] Inventario", false,GameSound.WeaponDrop);')])
edit(base+'Equipment/Inventory/PlayerInventoryCrafting.cs',[
    ('string.Join(" · ",items),false);','string.Join(" · ",items),false,GameSound.ResourceCollected);'),
    ('L.Text(material.displayName)),false))','L.Text(material.displayName)),false,GameSound.ConsumableUsed))')])
edit(base+'Equipment/Inventory/PlayerInventoryStorage.cs',[
    ('Commit(next,"Botín recogido.",false);','Commit(next,"Botín recogido.",false,GameSound.LootCollected);')])
edit(base+'Equipment/Inventory/PlayerInventoryHands.cs',[
    ('"Combinación y habilidades actualizadas.");','"Combinación y habilidades actualizadas.",true,GameSound.ItemEquipped);')])
edit(base+'Equipment/Inventory/PlayerInventoryBestiary.cs',[
    ('Text("Nueva especie descubierta."),false);','Text("Nueva especie descubierta."),false,GameSound.SpeciesDiscovered);')])

# Stable Unity asset IDs; do not replace an existing meta created by the editor.
newfiles=['Assets/Scripts/Audio/GameSoundCatalog.cs','Assets/Scripts/Audio/GameAudio.cs','Assets/Scripts/Audio/UISoundFeedback.cs','Assets/Scripts/Audio/Editor','Assets/Scripts/Audio/Editor/GameSoundWindow.cs','Assets/Scripts/Audio/Editor/Mismo.Audio.Editor.asmdef']
for name in newfiles:
    path=root/name
    meta=Path(str(path)+'.meta')
    if not meta.exists():
        meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n'+('folderAsset: yes\n' if path.is_dir() else ''),encoding='utf-8')
guid=(root/'Assets/Scripts/Audio/GameSoundCatalog.cs.meta').read_text().split('guid: ')[1].splitlines()[0]
asset=root/'Assets/Resources/Audio/GameSounds.asset'
asset.write_text('''%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: '''+guid+''', type: 3}
  m_Name: GameSounds
  m_EditorClassIdentifier: Mismo.Audio::GameSoundCatalog
  entries:
'''+''.join(f'  - sound: {i}\n    clip: {{fileID: 0}}\n    volume: {0.35 if i==2 else 0.7}\n    cooldown: {0.1 if i<5 else 0.3}\n' for i in range(17)),encoding='utf-8')
Path(str(asset)+'.meta').write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n',encoding='utf-8')
