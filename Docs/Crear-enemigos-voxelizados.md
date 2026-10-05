# Crear un enemigo voxelizado con comportamiento de goblin

Guía para el usuario y para futuras sesiones de IA. Revisada contra el código del proyecto el 23/09/2026. Describe el flujo disponible y sus limitaciones; no implica que se haya integrado o probado un Imp enemigo.

## 1. Entender las tres piezas

- **Modelo visual:** malla voxelizada, materiales, huesos y Animator. Voxelizar no añade inteligencia ni ataques.
- **Prefab enemigo:** navegación, vida, recepción de daño, controlador de comportamiento y presentación.
- **Settings propios:** vida, detección, movimiento y ataques. Compartir este asset comparte los cambios de comportamiento.

La ejecución terrestre común está en `EnemyController`. Goblin e Imp tienen `GoblinController` e `ImpController`, derivados de `HumanoidEnemyController`; jabalíes, arañas y el gólem usan `CreatureController`. Para un nuevo humanoide, especializar `HumanoidEnemyController`; para una criatura terrestre con el patrón actual, usar `CreatureController`. La familia de IA es independiente del tipo de rig. No hace falta convertir un modelo a Humanoid para darle IA. Humanoid sirve para compatibilidad y retargeting de animaciones; Generic puede animar un enemigo con su propio esqueleto.

Antes de importar o generar assets, leer [Buenas prácticas de assets](Buenas-practicas-de-assets.md).

## 2. Preparar y comprobar el original

1. Trabajar fuera de Play Mode. Usar la raíz completa del modelo, incluyendo su esqueleto, no solo el objeto que contiene la malla.
2. Comprobar materiales y reproducir al menos Idle, Walk/Run y un ataque sobre el original.
3. Mirar el tipo de rig del FBX y de los clips. Identificar qué Animator, Avatar y Controller hacen funcionar el original.
4. Guardar FBX en `Assets/Art/FBX`, animaciones y controllers en `Assets/Art/Animations`, materiales y texturas en sus categorías de `Assets/Art`.

### Humanoid y Generic

`VoxelRigExporter` conserva la jerarquía, los huesos y los pesos. Para Humanoid conserva el Avatar válido y coloca el Animator en la misma raíz de animación, incluso si está dentro de un contenedor. Copia los clips Humanoid completos, incluidas las curvas musculares. Si el original tiene un Controller Humanoid compatible, conserva su referencia y sus transiciones; de lo contrario crea un Controller de muestra con los clips encontrados. Ese Controller de muestra no es un controlador de combate completo.

**No cambiar a Generic los originales que ya funcionan como Humanoid para sortear este problema.** Tampoco asumir que arrastrar cualquier Avatar al resultado arreglará el rig.

Hay dos caminos:

- **Original Generic con clips de ese mismo esqueleto:** usar el flujo de exportación descrito abajo.
- **Original Humanoid, como el Imp examinado:** usar el original con Avatar válido y clips Humanoid. La exportación conserva su configuración Humanoid y permite asignar después clips compatibles en el taller. Un Avatar inválido, varios Animators o una mezcla de clips Generic/Humanoid se rechazan antes de generar mallas y materiales. La herramienta no convierte automáticamente un modelo Generic en Humanoid.

El antiguo `Assets/Art/Prefabs/Voxelized/Imp.prefab` tenía Avatar vacío; la corrección no modifica retroactivamente los prefabs existentes. Volver a exportar desde el original. Se generó `Assets/Art/Prefabs/Voxelized/Imp_Humanoid.prefab` como ejemplo con clips Humanoid Quaternius y se comprobó su deformación usando el mismo método de previsualización del taller. Sigue siendo un visual, sin IA. Pruebas: **Mismo → Modelos → Verificar voxelizador Humanoid con Imp**, informe en `output/voxel-humanoid/checks.txt`.

## 3. Voxelizar el modelo compatible

1. Abrir **Mismo → Modelos → Voxelizar modelo**.
2. En **Modelo fuente**, arrastrar la raíz completa del original.
3. Poner un nombre claro, por ejemplo `Imp_VisualVoxel`.
4. Empezar con **Resolución máxima: 64**; usar 128 si alas, dedos o cuernos pierden demasiado detalle.
5. Activar **Conservar materiales** y **Conservar rig y animaciones**.
6. En **Carpeta de animaciones**, elegir exclusivamente los clips compatibles de ese modelo. No elegir una carpeta que mezcle esqueletos.
7. Para un visual que irá dentro de un enemigo con collider propio, desactivar **Agregar collider de cuerpo**.
8. Pulsar **Voxelizar modelo y generar prefab**.
9. Comprobar Console y el prefab: SkinnedMeshRenderer con huesos y malla, Animator interno y clips exportados. Probar una animación antes de seguir. Si falló la exportación, comprobar los archivos parciales antes de volver a generar.

Destinos automáticos: prefabs en `Assets/Art/Prefabs/Voxelized`, mallas en `Assets/Art/Meshes/Voxelized`, clips/controllers en `Assets/Art/Animations/Voxelized` y materiales en `Assets/Art/Materials`.

## 4. Crear el prefab de comportamiento

La separación de controladores conserva los campos serializados `settings`, `weapon` y `target`. `EnemySettings` contiene los datos comunes; `GoblinSettings` se conserva como tipo compatible de los assets existentes, incluido `Imp.asset`, y `CreatureSettings` hereda directamente de `EnemySettings`. `GoblinAttack` conserva su formato serializado. El taller consulta `EnemyController`. La migración de prefabs está en **Mismo → Enemigos → Migrar controladores compartidos** y mantiene GUIDs e identificadores de componentes; no vuelve a generar arte ni ataques. Los tiempos, daño y selección actuales se conservan: la separación no añade todavía saltos ni reacciones a curaciones.

1. En Project, duplicar `Assets/Art/Prefabs/Enemies/Goblin.prefab` y nombrarlo `ImpEnemy.prefab` en `Assets/Art/Prefabs/Enemies`. Trabajar sobre esa copia.
2. Duplicar `Assets/Data/Enemies/BaseGoblin.asset`, nombrarlo `ImpSettings.asset` y asignarlo al campo **Settings** del controlador propio de la copia. No editar BaseGoblin para este enemigo.
3. Abrir la copia en Prefab Mode. Conservar la raíz y sus componentes de combate y navegación, incluyendo `NavMeshAgent`, `Health`, `DamageReceiver`, el controlador propio, el driver de animación correspondiente y `EnemyEquipment`.
4. Conservar **Attack Source**, su `DamageDealer` y la referencia **Weapon** del controlador enemigo. Es la fuente de daño de la IA; no equivale al modelo visible del arma y también se necesita para ataques sin espada.
5. Conservar el contenedor **Visual**. Sustituir dentro de él el modelo y esqueleto del goblin por el prefab voxelizado completo. Quitar solo el arte antiguo de esta copia, revisando las referencias antes. No dejar dos Animators activos del personaje.
6. Ajustar posición, orientación y escala en el hijo que contiene el modelo. Mantener la raíz del enemigo con escala `(1,1,1)` y el contenedor Visual sin usarlo como offset permanente: la presentación modifica Visual durante el juego. Alinear pies con el suelo y frente del modelo con el frente del enemigo.
7. Reasignar **Animator** de `GoblinAnimationDriver` y de `EnemyEquipment` al Animator del nuevo modelo. Revisar cualquier referencia que apuntara al modelo retirado.
8. En `GoblinPresentation`, conservar **Visual**, habilitar **Authored Animation** y revisar referencias visuales antiguas. Recalibrar **Ground Support / Sole Clearance** para el nuevo modelo; el valor del goblin no es universal. Revisar nombre visible y ocultar o adaptar el rótulo legado que escribe GOBLIN.
9. Ajustar altura, centro y radio del CapsuleCollider y dimensiones del NavMeshAgent al nuevo cuerpo. Evitar un segundo collider corporal procedente del visual.

Jerarquía orientativa:

```text
ImpEnemy                         ← IA, navegación, vida y drivers
├─ Visual                        ← contenedor de presentación
│  └─ Imp_VisualVoxel             ← malla voxelizada y jerarquía completa
│     └─ raíz original del Imp    ← Animator y huesos clonados
└─ Attack Source                 ← DamageDealer
```

Conservar además los objetos auxiliares de la copia que tengan referencias válidas, como el aviso de ataque. No borrar indiscriminadamente todos los hijos.

## 5. Conectar las animaciones al comportamiento

El Controller de muestra del voxelizador **no basta** para controlar un enemigo como el goblin. `EnemyActionPlayback` requiere un Controller asignado y utiliza estos parámetros:

| Parámetro | Tipo | Uso |
| --- | --- | --- |
| `Motion` | Int | 0 reposo, 1 caminar, 2 correr, 3 ataque, 4 reacción/muerte del sistema actual |
| `ActionTime` | Float | Progreso de acciones controladas por la IA |
| `PlaybackRate` | Float | Velocidad de reproducción |

Crear un Controller propio compatible tomando como referencia el del goblin: duplicarlo desde Project y reemplazar sus clips y máscaras por los del nuevo rig, manteniendo sus parámetros, transiciones y configuración de tiempos. Asignarlo al nuevo Animator. Revisar también `EnemyEquipment.controllerOverride`, porque puede reemplazarlo en ejecución. No reutilizar clips Generic del goblin sobre huesos del Imp sin adaptación.

1. Seleccionar `ImpEnemy.prefab` y abrir **Mismo → Enemigos → Taller de armas y animaciones**.
2. Elegir el prefab y pulsar **Abrir vista de ajuste**.
3. Asignar clips compatibles a **Reposo (Idle)**, **Caminar (Walk)**, **Correr (Run)** y **Recibir golpe (Hit)**.
4. Asignar opcionalmente **Ruptura de postura** y **Parry recibido**. Revisar máscaras: las del goblin pueden no servir para el Imp.
5. Pulsar **Reproducir** y comprobar cada clip. El aviso de tipo de rig incompatible debe resolverse, no ignorarse.
6. Usar **Guardar movimiento y hit en el prefab** para persistir estos cambios.

El taller asigna y previsualiza clips; no convierte rigs incompatibles. La adaptación automática de `EnemyAnimationRetargeter` está especializada en el aventurero y Goblin Concept, no es un retargeter universal para el Imp. Los campos internos `compatibleClip`/`compatibleSource` heredados también deben revisarse: pueden hacer que se reproduzca una copia adaptada al goblin en lugar del clip esperado.

La muerte del sistema actual reutiliza Motion 4 y la presentación tumba Visual. No existe aquí un campo independiente de Death en el taller. Una animación de muerte específica necesita adaptar esa integración y probarla.

## 6. Darle personalidad y ataques

Para conservar el patrón clásico del goblin, dejar **Attacks** vacío y ajustar **Slash** y **Charge** en `ImpSettings`. Una lista Attacks no vacía activa la selección de ataques genéricos; no mezclar ambos flujos sin revisar el controlador.

| Campo de Settings | Qué cambia |
| --- | --- |
| `health`, `posture` | Vida y resistencia de postura |
| `detectionRange`, `loseRange`, `leashRange` | Detección, pérdida del objetivo y distancia desde su origen |
| `speed`, `positioningSpeed` | Persecución y posicionamiento |
| `decisionPause`, `chargeCooldown` | Ritmo de decisiones y frecuencia de la carga clásica |
| `interruptibleByCombos`, `comboHitStun` | Interrupción por combos y tiempo de aturdimiento |
| `displayName` | Nombre configurado; comprobar su presentación en juego |

En el taller, seleccionar cada ataque y expandir **Animation**:

1. Asignar **Clip** compatible con el Imp.
2. Ajustar **Windup** (preparación), **Active** (ventana de ataque) y **Recovery** (recuperación), en segundos.
3. Ajustar **Active Starts At** y **Recovery Starts At**, entre 0 y 1: delimitan las fases dentro del clip. Ejemplo: impacto al 30 % y recuperación al 60 % → `0.30` y `0.60`.
4. Ajustar **Damage**, **Range**, **Forward Offset** y **Half Extents**. Half Extents es la mitad del tamaño del volumen de golpe. **Travel** controla avance en ataques que lo utilizan.
5. Activar **Mostrar volumen de impacto** y desplazar el progreso para alinear el golpe con la animación. La IA decide el daño; asignar una animación o un arma visual no crea por sí solo un ataque.
6. Pulsar **Guardar animaciones y ataques**. Estos cambios se guardan en Settings; confirmar que sea ImpSettings, no BaseGoblin.

Ejemplo inicial para experimentar, no valores oficiales: 60 de vida, velocidad 3.5, golpe de 8 de daño, Windup 0.5 s, Active 0.15 s y Recovery 0.7 s. Ajustar alcance e hitbox mirando el tamaño real del Imp.

Las armas son opcionales. En **Principal/Secundaria** se puede elegir un arma existente o modelo propio, seleccionar hueso de anclaje y ajustar agarre con W/E. Guardar con **Guardar equipo y animaciones base en el prefab**. Vaciar armas heredadas si el Imp golpea con las manos. El daño sigue en Settings.

Este flujo reutiliza IA terrestre: perseguir, posicionarse, avisar, atacar, recuperarse, recibir interrupciones, volver y morir. Volar, teletransportarse o tener fases especiales requiere programación adicional; no se obtiene cambiando solo la malla.

## 7. Probar y colocar en el mundo

1. Instanciar el nuevo prefab en una escena de prueba con jugador, suelo físico y NavMesh válido. Usar una copia de la arena si hace falta; no regenerar las arenas existentes para incorporar este enemigo.
2. Verificar reposo, detección, persecución alrededor de obstáculos y regreso al alejarse.
3. Comprobar ataque, ventana de daño, alcance real, carga, recuperación, hit, combo, parry, postura rota y muerte. Comprobar que no sigue dañando después de morir.
4. Revisar pies, escala, orientación, clipping y armas durante las animaciones, no solo en reposo. Evitar root motion que compita con el NavMeshAgent: este flujo lo controla mediante IA.
5. Revisar Console: sin referencias Missing, parámetros ausentes, rig incompatible ni avisos de estar fuera del NavMesh.
6. Salir de Play y confirmar que los cambios están guardados en prefab y Settings.
7. Colocarlo manualmente en una escena o registrar su aparición por bioma como se explica a continuación. Crear el prefab no lo añade automáticamente al mundo procedural.

### Registrar aparición por bioma en WorldContentCatalog

1. Seleccionar `Assets/Data/World/WorldContentCatalog.asset`.
2. En **Encounters**, agregar una entrada nueva sin reemplazar las existentes. Usar un **Id** único y estable, por ejemplo `forest.Imp`.
3. En **Prefab**, asignar el prefab enemigo completo (`ImpEnemy` cuando exista), no el visual voxelizado.
4. En **Biomes**, agregar explícitamente **Forest** para bosque, o los biomas deseados. Una lista vacía permite **todos** los biomas. Valores disponibles: Meadow, Forest, Highlands, Desert, Ice, Mountains y Ocean.
5. En **Site**, elegir **Clearing** para encuentros de ese tipo, **Ruin** para ruinas o el tipo correspondiente. El bioma no reemplaza este filtro.
6. Ajustar **Appearance Chance (%)** como filtro de aparición de esta entrada: 0 nunca participa, 100 participa siempre que sea elegible. Después, **Weight** sigue siendo el peso relativo entre las entradas que pasaron ese filtro; no es un porcentaje exacto del total. Completar también **Minimum Count / Maximum Count**, **Minimum Level**, **Altitude** y **Minimum Player Distance**. Para probar un enemigo nuevo, usar nivel mínimo 1, Appearance Chance 100 y un peso visible; balancear después con el resto del catálogo.
7. Si no hay variante élite, dejar **Elite Prefab** vacío, **Elite Chance** en 0 y **Guaranteed Elite** desactivado.
8. Guardar el catálogo. No hace falta registrar individualmente el prefab en otro catálogo si ya está referenciado aquí; comprobar que el WorldContentCatalog activo sea el usado por el mundo.
9. Probar selección con `catalog.Encounter(site, biome, level, height, seed)`: el enemigo debe poder salir en su bioma y quedar excluido fuera de él. La elección depende de semilla, Appearance Chance y pesos; si ninguna entrada supera su porcentaje, el sitio queda sin encuentro. Comprobar generación real en un mundo de prueba y revisar la persistencia de encuentros en partidas existentes.
10. Ejecutar `ProjectOrganizationChecks.Run`, pruebas pertinentes de encuentros y una build: esta modificación añade contenido al juego mediante una referencia serializada.

**Biomes controla dónde puede aparecer; GoblinSettings controla cómo se comporta.** Los perfiles `Biome Contents` del catálogo corresponden a naturaleza, no sustituyen la lista de encuentros enemigos. En esta sesión aún no se registró un Imp enemigo: primero se está validando el visual Humanoid.

Si cambia organización/carga de contenido, ejecutar `ProjectOrganizationChecks.Run` mediante **Mismo → Proyecto → Verificar organización y referencias**. Si se añade búsqueda global, usar referencias serializadas o el catálogo de `Data/System`, nunca Resources. Verificar build al cambiar el contenido incluido en el juego.

## Boca articulada de SoulEater verde

`SoulEater_Green_Animated.prefab` conserva el rig Generic, el controlador y los 17 clips del original. Usa `Art/Meshes/Voxelized/SoulEater_Articulated.asset` para evitar que los labios voxelizados con la boca cerrada formen una cortina al abrirla.

Para regenerarlo, usar **Mismo → Modelos → Corregir boca de SoulEater verde** (`SoulEaterMouthRepair`). La herramienta toma mandíbula y hocico abiertos del centro de `Scream`, conserva el resto del cuerpo en reposo y voxeliza a resolución 128. Mantiene separados los pesos de mandíbula y cráneo en las esquinas compartidas y devuelve la geometría al espacio de enlace del rig existente. El relleno conserva la cavidad conectada al exterior. Solo sustituye la malla de la variante verde; el prefab y la malla base siguen siendo la referencia original.

Los dos detalles de ojos se localizan mediante las UV de las marcas originales. Sus pesos interpolan la superficie original (cabeza y ojo), porque asignarlos únicamente al hueso del ojo los despega del rostro durante el rugido. Usan `Art/Materials/DragonBosses/SoulEater_Eyes.mat` y geometría incluida en la misma malla animada.

Ejecutar **Mismo → Modelos → Verificar boca de SoulEater verde** (`SoulEaterMouthChecks`): verifica conservación del rig y clips, 11 poses por animación, proximidad de los ojos al rostro, apertura de la cavidad y cierre de labios. También prueba que la separación opcional de articulaciones no cambie la continuidad del voxelizado habitual. Revisar visualmente `Scream`, `Basic Attack` y `Fireball Shoot` desde ambos lados; las comprobaciones geométricas no reemplazan esa revisión. Esta variante es un visual, aún sin integración como boss en el mundo.

## Instrucciones para la próxima IA

- Leer esta guía y las buenas prácticas antes de crear o integrar enemigos voxelizados.
- Inspeccionar el estado actual de los assets y `git status`; hay trabajo del usuario que debe conservarse. Las observaciones de esta guía no reemplazan comprobar el estado actual.
- Separar primero compatibilidad de rig, después comportamiento, después integración en el mundo. No afirmar que un Humanoid exportado funciona sin comprobarlo.
- Usar el controlador de la familia adecuada y crear prefab y Settings independientes. Para Imp, usar ImpController; para un nuevo humanoide, especializar HumanoidEnemyController; para criaturas terrestres, CreatureController. No sobrescribir Goblin.prefab, BaseGoblin, originales, escenas ni animaciones compartidas.
- Revisar referencias al sustituir el visual: Animator en drivers/equipment, Avatar, Controller/override, máscaras, clips compatibles cacheados, DamageDealer, colliders y soporte de suelo.
- No usar el retargeter del goblin como conversor universal. El voxelizador ya conserva Humanoid; no modificar importadores Humanoid existentes para forzar una exportación Generic.
- Al integrar la aparición en el mundo, añadir una entrada en `WorldContentCatalog.encounters` con `biomes` explícitos, prefab enemigo, sitio, peso y límites; no basta crear el prefab. Preservar entradas ajenas, evitar IDs duplicados y comprobar selección dentro/fuera del bioma y contenido en build.
- Mover assets solo desde Unity/AssetDatabase conservando GUID y .meta. No borrar originales sin comprobar dependencias.
- Validar al nuevo enemigo específicamente. Las pruebas existentes del goblin no demuestran por sí mismas que el Imp funcione. Ejecutar pruebas pertinentes de animación/combate y organización según el cambio; informar qué se comprobó realmente en Unity y qué sigue pendiente.

Código de referencia: `WeaponVoxelizerWindow.cs`, `VoxelRigExporter.cs`, `EnemyWorkshopWindow.cs` y `EnemyAnimationRetargeter.cs` en `Assets/Scripts/Gameplay/Player/Editor`; `GoblinController.cs`, `GoblinSettings.cs`, `GoblinAnimationDriver.cs` y `GoblinPresentation.cs` en `Assets/Scripts/Gameplay/Enemies/Goblin`; `EnemyActionPlayback.cs` y `EnemyEquipment.cs` en `Assets/Scripts/Gameplay/Enemies`.

Ver también [Animaciones de ataques enemigos](EnemyAnimations.md).
