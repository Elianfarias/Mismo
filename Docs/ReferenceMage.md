# Personaje basado en el mago de referencia — 10 de septiembre de 2026

El jugador de `VoxelRegion_7319` usa `Assets/Prefabs/Player/Player.prefab`, que referencia las mallas de `Assets/Art/FBX/Characters/Voxel_Adventurer_Animated.fbx`. Se actualizó ese FBX y los materiales de los prefabs Player y VoxelAdventurer. No se reconstruyeron sus componentes de movimiento, combate o inventario.

La fuente es el aventurero animado existente de `ArtSource/VoxelAdventurer/AnimationV2`, derivado de Voxel_Base_Faceless_Detailed. La adaptación añade sombrero voxel alto con punta doblada, ala ancha, cinta marrón, emblemas triangulares, ojos en cruz violetas, hombreras escalonadas y faldones vinculados a las piernas. Es una adaptación del diseño de la imagen sobre las proporciones del personaje existente.

Editable: `ArtSource/VoxelAdventurer/ReferenceMage/Voxel_Reference_Mage.blend`. Respaldo del FBX anterior: `ReferenceMage/Before_Mage.fbx`. Los scripts `build_reference_mage.py` y `apply_mage_materials.py` reproducen la geometría y la actualización de materiales y prefabs. El exportador conserva nombres de objetos, mallas, huesos y acciones.

Validación en un proyecto aislado con Unity 6000.3.11f1: los 37 identificadores de malla y sus arreglos de huesos coinciden con el FBX anterior; las mallas del prefab Player se resuelven y sus huesos coinciden con el modelo; se importan los 13 clips. Vista del prefab realmente importado: `ArtSource/VoxelAdventurer/ReferenceMage/Unity_Player.png`. Poses adicionales de Blender: Idle, Run y Attack1 en esa carpeta. Se conservaron los controladores y clips retargeteados del proyecto.

La validación es de importación, referencias y presentación del personaje. No sustituye una sesión completa de gameplay. No se generaron nuevos ejecutables ni builds WebGL; los builds anteriores conservan los assets con que se compilaron.
