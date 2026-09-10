# Criaturas del bosque

Integración de los paquetes Jabali, Arana y Golem V3 sobre la IA y el combate existentes del goblin. Los originales de Blender y la documentación están en `ArtSource/Creatures`; los FBX y las paletas de runtime, en `Assets/Art/Creatures`.

## Comportamientos

- Jabalí: ataque cercano y embestida con anticipación, dirección fijada y recuperación.
- Araña: mordida y salto horizontal navegable; el clip aporta la elevación visual y el daño ocurre al aterrizar. Spit_Web está importado pero desactivado: todavía no existe un sistema de telarañas o estados de ralentización/veneno.
- Gólem: golpe de área, barrido y recogida/lanzamiento de roca desde Rock_Carry, seguidos de recuperación. Morir o interrumpir el ataque elimina la roca sostenida y evita lanzamientos tardíos.
- Comparten navegación, percepción, vida, postura, daño, recompensas y muerte voxel del sistema existente. Los paquetes no incluyen clips propios de daño o muerte.

## Edición en Unity

`Assets/Data/Enemies/ForestCreatures` contiene Boar, Spider y Golem (CreatureSettings). Editar vida, postura, velocidad, percepción y el arreglo attacks: habilitación, peso, distancia mínima/máxima, daño, tiempos, desplazamiento, hitbox, cooldown y proyectil. Las variantes de una especie comparten configuración; duplicar el asset y asignarlo en el prefab para diferenciarlas. Los valores iniciales requieren balance de juego.

Los ocho prefabs están en `Assets/Prefabs/Enemies/ForestCreatures`. CreatureAnimationDriver sincroniza los clips con las fases de GoblinController; los goblins que no tienen una lista genérica conservan su selección anterior. Cada variante conserva su propio avatar Generic y paleta.

En `Assets/Resources/WorldContentCatalog.asset`, las entradas `forest.*` controlan bioma, tipo de sitio, peso, cantidad, nivel, altura y distancia mínima al jugador. Los jabalíes aparecen en grupos de 1–2, las arañas de 1–3; el gólem ocupa un único puesto por encuentro BossArena del bosque. No es un enemigo de encuentros ordinarios. La integración usa el streaming y la identidad persistente de enemigos del mundo existente.

El menú `Mismo/Enemies/Integrate forest creatures` reconstruye la integración con las APIs de Unity. Conserva las configuraciones existentes; regenera la jerarquía Visual de los prefabs. No modificar esa jerarquía a mano si se va a repetir el importador.

## Verificación

Unity 6000.3.11f1, copia aislada del proyecto para no cerrar el editor principal ni tocar partidas. Importación, referencias, avatares, clips, materiales y ocho prefabs validados. 69 comprobaciones en Play Mode: navegación, selección por bioma, anticipación sin daño, impactos, recuperación sin repetición, muerte, roca y cancelación, dirección fijada y colisión con obstáculos bajo una raíz común.

Escena reproducible: `Assets/Scenes/ForestCreaturesTest.unity`, con jugador, cámara y ocho variantes. Informes y ocho renders inspeccionados en `Docs/Validation`. Los renders son vistas de modelos; la suite de combate se ejecutó sin ventana. Queda la evaluación manual de cámara, dificultad y distribución a lo largo de una partida.

## Corrección de apariciones

La densidad del encuentro y la selección de especie usan semillas derivadas diferentes. Reutilizar la misma tirada hacía que el filtro de densidad descartara las tiradas necesarias para seleccionar criaturas situadas después del goblin en el catálogo. Las identidades persistentes no cambian: los enemigos derrotados siguen derrotados.

La navegación se actualiza por intervalos mientras se cargan sectores; los encuentros utilizan la última navegación completada y reintentan si su suelo todavía no está listo. Ya no esperan a que deje de cambiar toda la ventana de streaming. También se corrigió una línea concatenada en ExplorationWorldSettings.asset.

`WorldSpawnChecks.RunBatch` comprueba la carga del catálogo y la aparición efectiva de criaturas en terreno voxel procedural con su NavMesh, usando un inventario en memoria que no toca partidas. Incluye reintento tras descubrir región, ausencia de duplicados y persistencia de derrotados al recargar.

## Niebla y densidad

El catálogo contiene Atmosphere: fogEnabled, fogColor, fogStart (28 m) y fogEnd (90 m, limitado al horizonte cargado). Se aplica durante el juego, también al continuar. El shader de terreno y los materiales Standard admiten esta niebla.

Clearing Chance sube a 0.85. Roaming Chance (0.75) y Roaming Spacing (64 m) añaden grupos fuera de aldeas, caminos y puntos reservados, sin cambiar el terreno guardado. Sus identidades `world-roaming-v1` son independientes de los encuentros existentes. También funcionan en el centro original al entrar directamente a Play y en mundos legacy.

Goblin patrol baja de peso 50 a 8: en claros del bosque las siete variantes de fauna suman peso 56 frente a 9 de goblins, aproximadamente 86 % de fauna. Los grupos de jabalíes y arañas pasan a 2–3 integrantes. El gólem continúa reservado para arenas. Todos estos valores se editan en WorldContentCatalog y se aplican a partidas continuadas.
