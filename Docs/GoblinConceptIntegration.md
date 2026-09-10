# Goblin del concept integrado

Modelo de juego: `Assets/Art/FBX/Goblins/Goblin_Concept_Animated.fbx`.
Fuente editable: `ArtSource/GoblinConcept/Goblin_Concept_Rigged.blend`.

Los prefabs `Goblin` y `GoblinElite` usan el modelo nuevo. Los cinco goblins existentes de `VoxelRegion_7319` heredan el cambio; la escena no se reconstruye. El élite conserva su escala y sus efectos de tier.

El FBX contiene una malla con pesos rígidos, el esqueleto y cinco clips a 60 fps. Los colores se conservan en materiales; no requiere texturas externas. La exportación de juego omite el estudio y los biseles de presentación.

`GoblinAnimationDriver` usa Idle, Walk y Run según el movimiento real. Attack se muestrea entre 0–0,32 durante Telegraph, 0,32–0,59 durante Attack y 0,59–1 durante Recovery, conservando los tiempos y el daño de la IA. Hit reacciona al daño/aturdimiento. Se conserva la caída visual al morir. El Animator no desplaza al enemigo; NavMeshAgent sigue controlando el movimiento.

Materiales y controlador: `Assets/Art/Animations/GoblinConcept`. Se excluyen los clips `__preview__` al enlazar el controlador. Las animaciones procedurales antiguas del cuerpo y del arma quedan desactivadas para este modelo; permanecen las señales del ataque, la barra de vida y el feedback de daño.

Validación: FBX reimportado en Blender; prefab común y variante élite; herencia de las instancias de la región; render real en Unity; pruebas de daño, parry, dash, carga, stagger, muerte, persecución y animaciones. Evidencia en `Docs/Validation/GoblinConceptIntegration.txt` y `GoblinConcept_Unity.png`.

Los prefabs anteriores se guardaron en `Docs/Validation/GoblinIntegrationBackup`.
