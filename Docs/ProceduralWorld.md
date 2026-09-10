# Mundo procedural incremental

Implementación del prompt de mundo procedural sobre `ExplorationChunks`, `VoxelRegionHeightfield`, el inventario y el repositorio de perfiles existentes. El pedido actual de implementar reemplaza la instrucción histórica del documento de entregar solamente un plan. El GDD se utiliza como referencia de diseño; esta entrega no implementa sus propuestas ajenas al mundo procedural.

## Probar e iterar

Abrir `Assets/Scenes/MainMenu.unity` y entrar en Play. **Nueva partida** guarda una semilla nueva, genera también el centro del mundo y empieza en un pueblo procedural seguro. Usa un inventario y una progresión nuevos. **Continuar** vuelve a la última partida, conservando semilla, parámetros geográficos, posición, orientación, inventario, regiones descubiertas y enemigos derrotados. Se habilita únicamente cuando hay un guardado recuperable.

La posición se registra cada cinco segundos cuando el personaje está vivo y apoyado en el suelo, al perder foco, al pausar y al cerrar normalmente. Si se cierra en el aire se retoma el último punto seguro. Al morir se vuelve al pueblo inicial del mismo mundo. El archivo `Profiles/active-world.mismo` conserva la partida activa y cada partida nueva tiene su propio perfil `Profiles/Worlds/<id>.mismo` dentro de `Application.persistentDataPath`. Crear otra partida cambia cuál continúa el menú; no elimina los perfiles anteriores. Esta entrega no incorpora un selector de partidas antiguas.

Si solo existe el perfil anterior `single-player.mismo`, Continuar lo conserva y lo vincula al mundo original. Como ese perfil no almacenaba posiciones, su primera continuación empieza en el pueblo original. Abrir directamente `Assets/Scenes/VoxelRegion_7319.unity` en el editor también conserva el modo original: centro guardado y transición exterior de 160 metros, con los nuevos POIs desde aproximadamente |X| o |Z| > 288. El modo completamente procedural se activa al iniciar una partida desde el menú.

`Assets/Resources/ExplorationWorldSettings.asset` contiene la semilla, radios de streaming, escalas de macroformas y detalle, propiedades de pradera/bosque/highlands, montañas, densidad de encuentros y escalado. El catálogo inicial ya está enlazado en `Assets/Resources/WorldContentCatalog.asset`. El menú `Mismo > World > Configure procedural content` permite reconstruirlo si falta; no sobrescribe un catálogo existente.

El relieve anterior añadía `mountain * (12 + 24 * noise)` y saturaba la máscara radial antes de salir del centro. Por eso todo el exterior recibía relieve montañoso. El centro conserva esa geometría para no romper su escena; el exterior mezcla alturas regionales suaves, ruido local débil y montañas escasas en celdas globales. No se promete un porcentaje de transitabilidad medido hasta ejecutar las comprobaciones en Unity.

## Capas y contenido

- `ExplorationTerrain` decide regiones, biomas, alturas, montañas, emplazamientos y caminos con coordenadas globales. Mantiene exactamente los primeros samples vecinos del centro. La caché de sitios es acotada y no cambia los resultados.
- El gameplay pass aplana pueblos y arenas y adapta corredores entre pueblos y sitios vecinos. Reserva las explanadas y los caminos frente a árboles. Las casas se colocan fuera del corredor central. Los emplazamientos atraviesan chunks; su chunk propietario es el que contiene su centro.
- `WorldContentCatalog` selecciona presentación por categoría, bioma y peso. Los assets se importan como prefabs con origen al pie, colisiones y escala en metros. Casas y herrerías usan solares de hasta 10 × 10 m; se comprueban footprint y pendiente, y se elige entre las rotaciones configuradas. Árboles usan los prefabs registrados o las mallas del bosque existente.
- `ExplorationContent` compone pueblos con cuatro solares y una herrería, ruinas/arenas con una señal visible y secretos con un manantial y curación. Casas y ruinas tienen geometría voxel provisional de respaldo. El catálogo de presentación comienza vacío: registrar arte definitivo reemplaza esos respaldos. No se han creado nuevas criaturas ni arte definitivo.
- Los encuentros registrados reutilizan goblins, un campamento con élite garantizado, un élite poco frecuente y el guardián. Los encuentros comunes dejan emplazamientos vacíos y pueden tirar un élite. Los bosses solo se seleccionan para arenas. Grupos de hasta cinco se materializan únicamente cuando sus posiciones están en el NavMesh. El tamaño de la arena se decide antes del prefab. El guardián procedural entrega la recompensa de victoria existente; la recompensa exclusiva del boss central conserva su recorrido original.
- La selección admite biomas, pesos relativos, altura y nivel mínimo regional. Añadir una criatura implica registrar su prefab con IA y combate configurados. Los controladores existentes usan `WorldEnemyIdentity` para nivel, vida y daño; una IA nueva debe consumir ese mismo contrato y usar `EnemyProgressionReward` si necesita experiencia/maestría/drops.
- `WorldSurfaceFeedback` emite como máximo 64 partículas voxel por jugador, usando el color del manantial al cruzar su superficie y el color del suelo al aterrizar. No hay simulación de fluidos ni natación. `WorldWaterSurface` permite registrar superficies de agua adicionales.

Las categorías disponibles para futuras composiciones no implican que ya exista un layout que solicite cada una: Rock, Landmark, Well y Fence quedan disponibles en el catálogo, mientras el layout actual consume House, Blacksmith, Ruin y Tree. No se implementan splines editables, dungeons interiores ni un editor visual de pueblos.

## Identidad y persistencia

Las regiones tienen identidad `world-v1:seed:x:z`. La precarga no descubre regiones: el jugador debe entrar en su exterior. En ese momento se guarda una única vez `nivel del personaje + regionalLevelIncrement` (15 por defecto). Los monstruos usan el máximo entre ese mínimo y el nivel actual del personaje. La selección del encuentro usa el mínimo guardado, por lo que subir de nivel no cambia la criatura al recargar.

Los slots de enemigos dependen de semilla, celda y ordinal, independientemente del prefab. La derrota y la experiencia/maestría/drop se confirman en la misma transacción de `PlayerInventory`, utilizando `ProtectedProfileRepository`. Una escritura fallida no concede la recompensa; se reintenta y se conserva el chunk con una recompensa pendiente. Una muerte sin contribución del jugador se registra sin premio. Los secretos procedurales también se consumen persistentemente; el secreto central conserva su regla previa.

Los perfiles anteriores siguen siendo legibles sin las listas nuevas. Se valida identidad única y se conservan las copias defensivas de las transacciones. Límites actuales del perfil: 4096 regiones y 16384 slots consumidos. Cambiar semilla produce otro mundo. Cambiar espaciado/regiones o pesos del catálogo en un mundo existente puede cambiar su distribución: mantener esos parámetros estables durante una partida. Sustituir solamente el prefab de una entrada no altera terreno, posiciones ni identidades de derrotas.

## Validación

El 9 de septiembre de 2026 se compilaron Audio, Player, Enemies y Player.Editor con Roslyn contra las referencias instaladas de Unity 6000.3.11f1, sin errores. Esto verifica C# y referencias, no importación de assets ni comportamiento en Play.

Pasaron 40 pruebas de `Tools/InventoryCoreChecks`, incluidas corrupción y recuperación del guardado, compatibilidad previa, aislamiento de copias y rechazo de regiones/derrotas duplicadas.

La ampliación de Nueva partida/Continuar pasó 50 pruebas de núcleo en total. Las nuevas verifican semillas distintas, identidad válida del perfil, rechazo de posiciones no finitas, recuperación desde un repositorio recién creado y conservación de semilla/configuración/posición/orientación y respaldo anterior. Se compilaron también Menu y Menu.Editor sin errores. Las pruebas de núcleo usan System.Text.Json para el round trip de datos; la comprobación de JsonUtility y la transición real del menú a Play quedan preparadas para Unity y pendientes por la licencia.

`Mismo.Gameplay.Player.Editor.ProceduralWorldChecks.RunBatch` deja preparadas comprobaciones de continuidad con la escena guardada, regeneración determinista, independencia del catálogo, referencias de enemigos y muestreo de desniveles. No se pudo ejecutar: Unity informa `No valid Unity Editor license found` (salida 198). Quedan pendientes la importación real, esas métricas, el NavMesh, el streaming con combate y la inspección visual.

Después de activar la licencia, ejecutar las comprobaciones de mundo y `ExplorationChecks.RunBatch`. En Play comprobar además: acercarse a un sitio y ver aparecer el mismo encuentro; derrotar un integrante, alejarse más del radio de carga y regresar sin que reviva; reiniciar la aplicación y comprobar su ausencia; cruzar a otra región, subir de nivel y verificar que el mínimo anterior no cambia; saltar al manantial sin splash continuo; combatir contra varios enemigos en un mismo chunk. El hitbox ahora distingue receptores individuales aunque compartan la raíz del mundo.

Para evaluar la experiencia, recorrer durante cinco minutos sin combate y registrar interrupciones por desniveles, lugares que invitan a desviarse, tiempo entre encuentros y picos de frame durante generación/NavMesh. Comparar la misma seed con la versión anterior. Que el código compile no valida diversión, legibilidad del paisaje ni balance.
