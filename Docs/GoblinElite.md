# Goblin Elite

`Assets/Prefabs/Enemies/GoblinElite.prefab` es una variante del prefab base. Mantiene la misma IA, ataques, daño, stagger y muerte del Goblin; la tarea de diferenciación provisional vive en `GoblinEliteVisual` y `GoblinVisualStyle`.

Durante Play Mode el Elite se distingue por:

- Escala global 1,28x, conservando la silueta y el modelo del Goblin base.
- Tinte púrpura aplicado mediante `MaterialPropertyBlock`, sin duplicar ni modificar los materiales compartidos del base.
- Etiqueta `ELITE GOBLIN`, que cambia a `ELITE GOLPE`, `ELITE CARGA`, `ELITE ATURDIDO` y `ELITE DERROTADO` según el estado.
- Aura circular púrpura pulsante alrededor de los pies y partículas ascendentes.
- Aura y partículas intensificadas durante 0,3 segundos al recibir daño, además del flash de impacto común.

La arena `Assets/Scenes/GoblinEliteArena.unity` contiene al jugador y una instancia del prefab Elite para probar la lectura visual sin modificar la arena base. `Mismo > Prototype > Goblin > Build Elite Variant` crea sólo el prefab si falta; `Build Goblin Arena` crea ambos prefabs y las dos arenas si faltan.

La diferenciación es deliberadamente provisional: no cambia las estadísticas ni el comportamiento hasta que se defina el tier de gameplay. Los datos de estilo están separados de la IA para poder reemplazar este feedback por materiales, VFX o un modelo final sin tocar `GoblinController`.
