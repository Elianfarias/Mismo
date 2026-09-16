from pathlib import Path
import re, uuid
root=Path(__file__).resolve().parents[2]
base=root/'Assets/Scripts/Gameplay/Player'
targets={
    'PlayerController.cs':['Update'],
    'Camera/ThirdPersonCamera.cs':['Update','LateUpdate'],
    'Equipment/Inventory/InventoryPanel.cs':['Update','OnGUI'],
    'Presentation/WorldMapPanel.cs':['Update','OnGUI'],
    'World/GatheringPlayer.cs':['Update','OnGUI'],
    'Equipment/Inventory/InventoryWorldAccess.cs':['Update','OnGUI'],
    'World/CompanionPlayer.cs':['Update','OnGUI'],
}
for name,methods in targets.items():
    path=base/name
    text=path.read_text(encoding='utf-8-sig')
    for method in methods:
        pattern=r'(\bvoid '+method+r'\(\)\s*\{)'
        text,count=re.subn(pattern,r'\1\n            if (Mismo.Gameplay.Player.Presentation.GameplayPause.BlocksInput) return;',text)
        print(name,method,count)
    path.write_text(text,encoding='utf-8')
for name in ['Assets/Scripts/Menu/PauseMenu.cs','Assets/Scripts/Gameplay/Player/Presentation/GameplayPause.cs']:
    meta=Path(str(root/name)+'.meta')
    if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n',encoding='utf-8')
