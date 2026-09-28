# Informe técnico — Warhammer Rework

Fecha: 28/09/2026. Proyecto: `C:/Users/Franc/Projects/mismo`. Unity 6000.6.0f1.

Implementación y validación realizadas antes de cerrar este informe. La revisión del 28/09 interpreta primera, segunda y tercera como Q/E/R: carrera con remate, impacto sísmico y giro completo, respectivamente. El básico conserva la petición anterior: un clic izquierdo ejecuta un golpe; una segunda pulsación durante la ventana de encadenado añade un golpe de vuelta. Sigue siendo una sola habilidad dentro de las seis del Warhammer.

Rama conservada: `branch-ipp`. HEAD: `9eb63a5b`. No se ejecutaron add, commit, push, pull, merge, rebase ni cambio de rama.

## 1. Arquitectura actual encontrada

El flujo activo es `PlayerInputReader → PlayerController → EquipmentLoadout → PlayerInventory/WeaponFamilyDefinition → AbilityRunner`. La animación lee el estado del combate mediante `PlayerAnimationDriver → WeaponActionPlayback → WeaponAnimationSet`. El combate determina el reloj y los impactos; los clips no ejecutan daño mediante eventos independientes.

`WeaponDefinition` identifica el objeto equipable. `WeaponFamilyDefinition` contiene las habilidades predeterminadas, el repertorio seleccionable y las animaciones. El perfil de pose pertenece al modelo; las animaciones de ataque pertenecen a la familia. Se revisó el código actual y `Docs/WeaponAnimationArchitecture.md`, en lugar de asumir que el informe histórico seguía describiendo el proyecto.

## 2. Diferencias respecto del Hammer anterior

Se conservó el arma existente y sus identificadores. Se normalizaron su organización, escala, poses y vínculos; se completaron barrido y avance; el especial ejecuta ahora un torbellino de dos vueltas y la primera técnica es una carrera con remate. Los ataques tienen preparación, aceleración, contacto, continuación y recuperación. El básico ahora reutiliza el sistema de combo de la espada con dos clips propios.

## 3. Estructura de carpetas

| Contenido | Carpeta actual |
| --- | --- |
| Definiciones y pose | `Assets/Data/Weapons/Hammer` |
| Familia y bindings | `Assets/Data/WeaponFamilies` |
| Receta | `Assets/Data/Crafting/Recipes` |
| Perfil de feedback | `Assets/Data/Combat/Feedback` |
| Proyectos editables de animación | `Assets/Data/AnimationAuthoring/HumanoidAttacks/Hammer` |
| FBX original | `Assets/Art/FBX/Weapons/Hammer` |
| Prefab visual | `Assets/Art/Prefabs/Weapons/Hammer` |
| Materiales | `Assets/Art/Materials/Weapons/Hammer` |
| Clips y override | `Assets/Art/Animations/Weapons/Hammer` |
| Iconos de habilidades | `Assets/Art/UI/Weapons/Hammer` |
| Icono de inventario | `Assets/Art/UI/Inventory` |
| Audio existente | `Assets/Art/Audio/Hammer` |
| Código/editor | `Assets/Scripts/Gameplay/Player/Equipment` y `Assets/Scripts/Gameplay/Player/Editor` |

Se siguió `Docs/Buenas-practicas-de-assets.md`. No se introdujeron cargas mediante Resources.

## 4. Referencias modernas

La espada actual aportó el encadenado básico, ventanas de entrada y reproducción de combos. El authoring Humanoid de doble espada aportó el flujo de recetas, rig y bake. La locomoción se apoya en los clips Quaternius existentes del personaje real.

Se consultaron muestras visuales de las referencias suministradas: [Monster Hunter Wilds](https://www.youtube.com/watch?v=1SVhAsgES1k), [Jormungandr](https://www.youtube.com/watch?v=r1QgZnCxBeo), [New World](https://www.youtube.com/watch?v=oI-8AghHLcA) y [Darktide](https://www.youtube.com/watch?v=uyMxBX6Lk0s). Se usaron como referencias de preparación, peso y recuperación. No se copiaron animaciones ni se afirma una revisión continua de cada video o una comparación auditiva completa.

## 5. Cómo funcionan las seis habilidades

El Warhammer tiene exactamente seis definiciones: básico y cinco técnicas. La entrada básica queda fija en clic izquierdo. Q/E/R permiten equipar tres técnicas del repertorio. El selector de habilidades ya recorre `SkillCount`; no necesita seis botones simultáneos.

Los predeterminados son Carrera demoledora/Q, Golpe sísmico/E y Torbellino de guerra/R. Barrido se desbloquea en nivel 3 y Avance en nivel 6. Las primeras tres técnicas siguen disponibles en nivel 1. El guardado usa IDs de habilidades, preservados durante la migración.

## 6. Arreglos de cuatro elementos encontrados

`AbilitySlot { Basic, Q, E, R }`, los cuatro defaults de `WeaponDefinition` y los cuatro defaults de `WeaponFamilyDefinition` siguen activos. Representan entradas equipadas, no el tamaño máximo del repertorio. No se ampliaron a seis.

## 7. Compatibilidad legacy

Se conservó la resolución de habilidades locales mediante `overrideFamilyAbilities`, las armas con arreglos cortos y el retorno nulo para slots fuera de rango. `LegacyCombatAnimationAdapter` sigue disponible para contenido sin clips modernos; el Warhammer tiene bindings Humanoid propios. Los nombres históricos `HammerDoubleSpin` y `hammer.double-spin` se conservaron por compatibilidad de referencias y partidas, ahora con un giro real de cuerpo completo.

## 8. Cambios necesarios para el estándar actual

Se amplió el repertorio del Hammer, no el enum compartido. Se asignaron seis iconos/descripciones y seis bindings; el primero contiene dos clips de combo. Se añadieron recetas editables, poses y locomoción, feedback específico y validación de obtención/selección/persistencia. No se creó otra arma o familia paralela.

## 9. Modelo final

Se reutiliza `Assets/Art/FBX/Weapons/Hammer/Hammer_Double.fbx`, con sus materiales de madera y piedra. No se importó otro proveedor ni se sustituyó el modelo por un arma distinta.

## 10. Prefab, escala y pivot

`Assets/Art/Prefabs/Weapons/Hammer/HammerVisual.prefab`: altura normalizada de 1,55 m. Se mantuvo el pivot y se ajustó la geometría del prefab existente. GUID conservado: `aca8c65beab45cc458d2e64b3b824efe`. El perfil aplica escala 1.

## 11. Pose equipada

`Assets/Data/Weapons/Hammer/HammerPose.asset`: anclaje `RightHand`, offset cero, rotación calibrada `(291.50342, 84.78457, 202.50339)`. El agarre izquierdo queda a `(0, 0.30, 0)` en coordenadas locales del arma. `maintainSupportGrip` conserva ese contacto después de la mezcla Humanoid, reutilizando el solver de brazo existente. La opción queda desactivada por defecto en otras armas.

## 12. Pose guardada

El mismo perfil usa anclaje `Character`, offset `(0.35, 0.65, -0.31)`, rotación `(0, 0, 32)` y escala 1. Se conserva el seguimiento del torso del sistema actual. La hoja de revisión guardada es `output/warhammer/Quaternius_Idle.png`; se comprobó además cambiar de conjunto y volver a equipar.

## 13. Las seis habilidades

Tiempos en segundos a velocidad de ataque 1. El multiplicador de velocidad y el de daño del inventario se aplican en ejecución.

| Habilidad / ID | Entrada / AbilitySlot | Daño base | Preparación / activa / recuperación | Cooldown | Área y contacto |
| --- | --- | --- | --- | --- | --- |
| Golpe de mazo / `hammer.strike` | Clic izquierdo / Basic | 22; segundo clic añade 26 | Etapa 1: .36/.20/.42; etapa 2: .40/.22/.46; transición .04 | .98 | Esfera de radio .68, centro frontal 1.38 y altura .95; contacto a .36 y .40 desde el inicio de cada etapa |
| Carrera demoledora / `hammer.heavy` | Q predeterminado; seleccionable Q/E/R | 40 | .28/.92/.55 | 4.5 | Carrera 3.2 m; radio .78, frente 1.40, altura .90, cono 90°; contacto 1.00 |
| Golpe sísmico / `hammer.slam` | E predeterminado; seleccionable Q/E/R | 32 | .86/.24/.70 | 5 | Radio 2.1, frente 1.03, altura .15, circular; contacto .86 |
| Barrido de guerra / `hammer.sweep` | Seleccionable Q/E/R, nivel 3 | 25 por enemigo | .54/.42/.55 | 3.6 | Radio 1.75, frente .40, altura .90, sectores de 75° orientados −60/0/+60; muestras .58/.66/.78, un daño por objetivo |
| Avance demoledor / `hammer.charge` | Seleccionable Q/E/R, nivel 6 | 30 | .48/.68/.58 | 6 | Desplaza 1.8 m por motor; radio .70, frente 1.38, altura .90, cono 80°; contacto .94 |
| Torbellino de guerra / `hammer.double-spin` | R predeterminado; seleccionable Q/E/R | 14 × 4 | .48/1.80/.58 | 8 | Dos vueltas en el lugar; radio 1.70 centrado en el personaje, altura .95, circular; contactos .66, 1.11, 1.56 y 2.01 |

La ventana para preparar el segundo básico es 22–82 % de la primera etapa, aproximadamente .216–.804 s a velocidad 1. Un clic no desencadena automáticamente el segundo golpe. El cooldown comienza según el contrato existente de AbilityRunner; no se puede iniciar otra cadena mientras sigue ocupada la actual. Las ventanas efectivas de daño del básico son 36.734694–47 % y 37.037037–49 % de sus respectivos clips. No hay una séptima habilidad.

Todas las habilidades tienen coste de estamina/Focus 0; ganancia de Focus 5 por impacto válido, salvo el especial, que usa 2. La movilidad durante la ejecución está comprometida; el avance se realiza mediante PlayerMotor. No se alteraron los costes de otras familias.

La tabla de recursos exactos por habilidad está en el mapa final (§44); las descripciones de interfaz están en §23.

## 14. Animaciones

- Básico: diagonal de izquierda a derecha; el segundo clip prepara el mazo al lado contrario y vuelve sobre el objetivo.
- Carrera demoledora: pasos de Run durante el avance y golpe diagonal de remate, seguido de recuperación.
- Sísmico: elevación del mazo y contacto con el suelo, sin continuar atravesándolo como un giro.
- Barrido: arco horizontal amplio, distribuido entre tres consultas de daño sin golpear tres veces al mismo enemigo.
- Avance: paso sobre locomoción existente y golpe al final del desplazamiento.
- Torbellino: el personaje gira dos vueltas completas con el mazo horizontal extendido; acelera y frena antes de regresar a guardia. Se tomó la referencia de giro solicitada por el usuario, similar al movimiento de la E de Garen, sin copiar assets.

Son siete clips de ataque, porque el básico tiene dos etapas, más Idle/Walk/Run. Hay diez recetas correspondientes y un override de locomoción.

## 15. Fluidez

Se hornearon poses Humanoid del rig real a 60 fps. Los bindings usan cuerpo completo y .10 s de mezcla. Las poses iniciales/finales comparten guardia. La mano de apoyo se corrige después del blending; antes de esa corrección, una transición a caminar podía separarla alrededor de .25 m. Las capturas finales verifican un error menor de .08 m; el segundo básico mostró aproximadamente .039 m en contacto, y las transiciones de locomoción quedaron prácticamente en cero.

## 16. Fases e inercia

Los keyframes de preparación incluyen una pose cargada a .63 y .84 del tiempo hasta contacto. Los golpes diagonales aceleran hacia el impacto y desaceleran en la continuación. Los impactos al suelo usan una frenada más marcada. La recuperación interpola a una guardia común, sin eliminar la anticipación para aparentar velocidad. El segundo básico invierte el sentido de carga y giro del torso. El torbellino hornea 720° de rotación corporal sobre la fase activa, con aceleración/frenado suaves; su raíz física permanece en el lugar. Los tiempos concretos están en §13 y las poses permanecen editables en las recetas.

## 17. Sincronización

`AbilityRunner` y `BasicSwordCombo` son la autoridad temporal. Los bindings usan la misma duración/fases; los clips de combo se seleccionan por índice de etapa. `HammerImpactAction` procesa intervalos cruzados, incluidos pasos de simulación grandes. Las pruebas confirman ausencia de daño antes del contacto y el total esperado después. La velocidad de ataque escala el reloj de animación y el de combate juntos.

## 18. Sistema de hit

El básico usa `BasicSwordCombo`, `AttackHitbox`, `DamageDealer` y `IDamageReceiver`, igual que el sistema actual de espada. Sus esferas se consultan sólo en las ventanas configuradas y deduplican por receptor. Las técnicas usan `HammerImpactAction`: esfera, filtro angular, comprobación de obstrucción desde el personaje hasta el contacto y deduplicación de colliders. Consultar la obstrucción desde el centro adelantado del golpe podía omitir una pared; se corrigió y se probó un objetivo detrás de ella. El barrido conserva objetivos alcanzados durante toda la ejecución. El avance usa `MoveCasterAction` y las colisiones de `PlayerMotor`.

## 19. Cantidad de impactos

Básico: uno o dos, según entrada. Carrera, Sísmico, Barrido y Avance: uno por objetivo. Torbellino: cuatro pulsos por objetivo, delante o detrás. Las tres muestras del barrido amplían cobertura, no multiplican daño. Las pruebas usan enemigos con colliders duplicados.

## 20. Audio

Se reutilizan `Hammer-Swing.wav`, `Hammer-HeavySwing.wav`, `Hammer-Impact.wav`, `Hammer-HeavyImpact.wav`, `Hammer-Ground.wav` y `Hammer-Spin.wav` en `Assets/Art/Audio/Hammer`. El básico emite un swing por etapa y sonido de impacto sólo al conectar. `ComboStep.impactSfx` es opcional; vacío conserva las otras familias. El sonido de impacto está limitado a una emisión por etapa, aunque alcance varios colliders. Las técnicas de suelo sincronizan sonido y polvo con su pulso.

El perfil Hammer evita duplicar los sonidos normales/pesados del perfil de feedback. Se verificaron eventos y asignaciones de audio, no una mezcla auditiva comparativa exhaustiva en distintos dispositivos.

## 21. VFX y Feel

Perfil: `Assets/Data/Combat/Feedback/HammerFeedback.asset`, basado en el contrato actual de Sword. Reutiliza `Assets/Art/Prefabs/Combat/WallCoeur/VFX_Classic_01.prefab` y los prefabs Feel `Assets/Art/Prefabs/Feedback/Feel/CombatImpact.prefab` y `CombatHeavy.prefab`. Conserva feedback compartido de bloqueo, parry y rotura de postura.

Impacto normal: escala .23 y hit stop .008 s; pesado: escala .42 y .035 s; umbral de postura pesada 18. No se instaló otro shaker ni otro sistema de hit stop. Se respeta el receptor orbital del canal 7401. La estela usa el perfil existente y se apaga durante la recuperación del combo. Los ataques al suelo usan `WorldSurfaceFeedback` y el emisor acotado de `CombatFeedback`; el anillo alcanza el radio configurado en .28 s.

`Docs/Feel-en-combate.md`, mencionado por AGENTS.md, no está presente en este checkout. Se inspeccionó la implementación actual para conservar el canal y los receptores existentes.

## 22. Iconos

Seis imágenes transparentes de 512 px en `Assets/Art/UI/Weapons/Hammer`: `hammer-strike.png`, `hammer-heavy.png`, `hammer-slam.png`, `hammer-sweep.png`, `hammer-charge.png`, `hammer-spin.png`. El último conserva el nombre/GUID histórico y representa el mazo rodeado por una flecha circular. El icono de carrera añade líneas de velocidad y una flecha frontal. Inventario: `Assets/Art/UI/Inventory/weapon-Hammer.png`. Vista conjunta: `output/warhammer/skill-icons.png`.

## 23. Descripciones de interfaz

| Habilidad | Descripción guardada |
| --- | --- |
| Golpe de mazo | Golpe diagonal de 22 de daño. Volvé a hacer clic para encadenar un golpe de vuelta de 26. |
| Carrera demoledora | Corré 3,2 m hacia delante y rematá con un golpe de 40 de daño. Los obstáculos frenan la carrera. |
| Golpe sísmico | Descargá el mazo contra el suelo: 32 de daño en 2,1 m y breve tambaleo. |
| Barrido de guerra | Barré un arco frontal amplio. Inflige 25 de daño a cada enemigo alcanzado. |
| Avance demoledor | Avanzá 1,8 m y descargá un golpe de 30 de daño. Los obstáculos detienen el avance. |
| Torbellino de guerra | Girá dos vueltas sobre vos mismo con el mazo extendido. Cuatro impactos de 14 de daño en un radio de 1,7 m. |

## 24. Obtención y crafting

`Assets/Data/Crafting/Recipes/StoneHammer.asset`, ID `REC-HAMMER-STONE`, produce el arma `hammer.stone`. Conserva la receta de una piedra y el banco de crafting; no se rebalanceó la economía. Está referenciada por `Assets/Data/Gathering/GatheringSettings.asset`, `Assets/Data/Inventory/ItemCatalog.asset` y `Assets/Data/System/RuntimeAssetCatalog.asset`. Se probó fabricar, equipar, serializar y restaurar desde un repositorio en memoria, sin escribir partidas personales.

## 25. Archivos creados o completados

Se completaron las seis definiciones, el repertorio, diez clips/recetas, seis iconos, el perfil de feedback y la escena aislada `Assets/Scenes/Validation/WarhammerValidation.unity`. El nuevo segundo básico es `Hammer_Attack_Return.anim` y su receta homónima. Barrido y Avance completan el repertorio. Las herramientas Hammer ya existían como archivos sin seguimiento y se reescribieron para la arquitectura actual; no deben confundirse con clases completamente ajenas añadidas a otro sistema.

Los archivos `.meta` acompañan los assets. El mapa de §44 enumera los recursos finales. `output/warhammer` contiene evidencia de pruebas y copias históricas de las tres herramientas de editor; no es una carpeta de assets runtime.

## 26. Archivos modificados

Runtime de este trabajo: `AbilityDefinition.cs`, `AbilityRunner.cs`, `WeaponPoseProfile.cs`, `WeaponPresentation.cs`, `PlayerInventoryHands.cs`, `BasicSwordCombo.cs`, `AttackHitbox.cs`, la parametrización del radio de suelo en `CombatFeedback.cs` y la ventana de estela de `SwordAnimationFeedback.cs`.

Editor/registro: herramientas Hammer, ruta del icono de inventario y registros del catálogo. Se preservaron modificaciones previas en código compartido, Goblin, settings, catálogo y organización. El diff global incluye esos cambios anteriores y no representa exclusivamente este rework.

## 27. Migraciones

Se realizaron mediante `AssetDatabase.MoveAsset`, verificando el GUID antes/después:

```text
Assets/Data/Weapons/Hammer/HammerVisual.prefab -> Assets/Art/Prefabs/Weapons/Hammer/HammerVisual.prefab | aca8c65beab45cc458d2e64b3b824efe
Assets/Resources/Recipes/StoneHammer.asset -> Assets/Data/Crafting/Recipes/StoneHammer.asset | dd82f4aa367c5c649a8918136c043dfd
Assets/Resources/UI/QuietFantasy/Icons/hammer-strike.png -> Assets/Art/UI/Weapons/Hammer/hammer-strike.png | 2b5b3169ce3e8fc44b356a6faa496313
Assets/Resources/UI/QuietFantasy/Icons/hammer-heavy.png -> Assets/Art/UI/Weapons/Hammer/hammer-heavy.png | 5a592ea34728b884ab6add0e15fee0b4
Assets/Resources/UI/QuietFantasy/Icons/hammer-slam.png -> Assets/Art/UI/Weapons/Hammer/hammer-slam.png | 93456d7d848001d4da9b15ad8137bec7
Assets/Resources/UI/QuietFantasy/Icons/hammer-spin.png -> Assets/Art/UI/Weapons/Hammer/hammer-spin.png | d4f576a1263f1bf46ab22d67b6493679
Assets/Art/FBX/Weapons/Hammer/HammerWood.mat -> Assets/Art/Materials/Weapons/Hammer/HammerWood.mat | f6224b1dda939ba4a9ac63916c0e3884
Assets/Art/FBX/Weapons/Hammer/HammerStone.mat -> Assets/Art/Materials/Weapons/Hammer/HammerStone.mat | c64996f5f19ed29458a0d6bb6092a17f
Assets/Art/Inventory/weapon-Hammer.png -> Assets/Art/UI/Inventory/weapon-Hammer.png | 736bcdd6644576a4886ee1b0cdfe4302
```

## 28. Eliminaciones

Sólo se retiraron carpetas vacías resultantes de las migraciones. No se borró el modelo fuente ni se eliminaron originales de proveedores. Las eliminaciones de ZIP `.meta` y Addressables visibles en Git ya estaban en el árbol de trabajo y se conservaron.

## 29. Recursos históricos conservados

Se conservaron el FBX, materiales, audio original, IDs de arma/familia/receta y GUIDs migrados. `Hammer-Spin.wav` se reutiliza para el nuevo torbellino; no se borró contenido previo del usuario. Los nombres de archivo del especial mantienen compatibilidad. No se creó una copia paralela del arma para evitar migrarla.

## 30. Herramientas y clases del Hammer

- `HammerAssets`: migra rutas con GUID estable, configura definiciones/familia/pose/receta y actualiza/verifica catálogo y organización.
- `HammerPolishAssets`: produce recetas Humanoid y clips, locomoción, iconos y enlaces a audio existente.
- `HammerChecks`: valida contratos estáticos, ejecución real, input, persistencia, capturas y build de contenido. La ejecución interactiva restaura la escena inicial de Play y rechaza escenas con cambios sin guardar.
- `HammerImpactAction`: procesa impactos temporizados, áreas, arcos, postura, empuje, tambaleo y eventos de suelo sobre el sistema existente.

## 31. Código existente modificado

`AbilityDefinition/AbilityRunner` añaden movilidad de recuperación configurable. `WeaponPoseProfile/WeaponPresentation` añaden agarre de apoyo opcional. `ComboStep/AttackHitbox` añaden un sonido opcional al conectar una etapa. `PlayerInventoryHands` acepta un arreglo vacío de manos secundarias al normalizar perfiles antiguos. `CombatFeedback` recibe el radio real de la onda de suelo. `SwordAnimationFeedback` excluye los combos de la condición genérica que mantenía la estela encendida.

## 32. Sistemas reutilizados

PlayerController/Input System, EquipmentLoadout, PlayerInventory, MasteryProgress, AbilityRunner, BasicSwordCombo, AttackHitbox, DamageDealer/IDamageReceiver, MoveCasterAction/PlayerMotor, CombatState, CombatFeedback, WorldSurfaceFeedback, receptor Feel orbital, WeaponPresentation, PlayerAnimationDriver, WeaponActionPlayback, WeaponAnimationSet, HumanoidAttackRig/Recipe/Authoring, Quaternius, CraftingStation y ProjectAssets/RuntimeAssetCatalog.

## 33. Justificación de cambios compartidos

La recuperación necesitaba restringir el desplazamiento sin cambiar el valor predeterminado 1 de otras habilidades. El blending Humanoid podía despegar la mano izquierda; el ajuste es optativo por perfil. El combo necesitaba audio de contacto distinto del swing; los campos nuevos son nulos por defecto. La corrección de guardado evita indexar `offhands[0]` cuando la deserialización devuelve `[]`. El radio de VFX evita mostrar una onda pequeña para un ataque de área grande. No se añadió un enum o un controlador de combate sólo para el martillo.

## 34. Regresiones

Sword y Bow se equiparon después del Hammer y volvieron a infligir daño mediante su básico; ambos conservaron sus seis técnicas. Todas las WeaponDefinition encontradas toleraron un slot fuera de rango. Un arma legacy con una única habilidad conservó su básico y devolvió null para R. Esto no equivale a una prueba exhaustiva de cada habilidad de cada arma en todos los mapas.

## 35. Pruebas actualizadas

`HammerChecks` ya comprueba seis definiciones y cinco técnicas seleccionables, en lugar de confundir cuatro entradas con cuatro habilidades totales. Comprueba dos clips del básico, IDs persistentes, sincronización, daño y sonido por etapa, dos pulsaciones físicas de ratón a través de PlayerController, apagado de estela, retorno desde caminar/correr, cambio de arma, crafting/save/load y dependencias dentro de una build.

## 36–37. Problemas encontrados y corregidos

| Problema | Corrección / comprobación |
| --- | --- |
| Rutas históricas y recursos mezclados | Migración por AssetDatabase y verificación de GUIDs/organización |
| Repertorio incompleto y especial sin el peso buscado | Seis definiciones, barrido/avance, carrera con remate y torbellino |
| Mano izquierda se separaba al mezclar locomoción | Restricción opcional del agarre después del blending; capturas en Play |
| Perfil con `offhands: []` fallaba al recargar | Guard clause antes de indexar; roundtrip real en memoria |
| Estela continua al pasar el básico a combo | Ventana de hitbox para combos; comprobación durante recuperación |
| Prueba inicial de input usaba una sobrecarga no pública | Uso de InputSystem.Update() público; compilación y dos pulsaciones verificadas |
| Dispositivos de prueba dejaban referencias de acciones antiguas | Reenlace de PlayerInputReader al cambiar el esquema de prueba |
| Medición de avance arrastraba velocidad de sprint del fixture | Frenado mediante PlayerController antes de medir; avance final cercano a 1.8 m |
| Simulación manual de locomoción daba inclinaciones artificiales | Prueba con el PlayerController real y W/Shift; margen explícito de 30° |

La primera versión de la prueba de carrera intentó seleccionar Q mientras el inventario aún bloqueaba cambios por combate. Se corrigió el fixture esperando el fin del estado de combate; la pasada final valida la carrera libre y el obstáculo.

## 38. Warnings restantes

La build avisa que la malla compartida `Plane`, usada por un Particle System Renderer, tiene Read/Write desactivado y que versiones futuras de Unity no lo corregirán automáticamente. No se modificó ese asset compartido ajeno al Hammer. Durante el trabajo también aparecieron avisos previos de APIs obsoletas, Feel/CodeCoverage y servicios Unity/licencias. La compilación final y la suite del Hammer terminaron sin errores nuevos de código; no se borró la consola para ocultar mensajes históricos.

## 39. Resultado funcional

Última suite completa: **PASS**, 28/09/2026 15:20:51, `output/warhammer/playmode.txt` (145 comprobaciones PASS, además de la cabecera). Verifica daño básico 22 con un clic y 48 con dos, audio por etapa, las cinco técnicas, input físico simulado de Basic/Q/E/R, guardar/re-equipar, avanzar, Sword/Bow y persistencia de selección. `output/warhammer/static.txt` registra las comprobaciones de assets y contratos.

Build de contenido Windows: **PASS**, 28/09/2026 15:21:15, `output/warhammer/build.txt`. Se cargó el bundle generado y se verificaron las seis habilidades, iconos, sonidos y ambos clips del básico. Es una build de AssetBundle para Windows, no una build completa del ejecutable del juego.

La organización se verificó durante la actualización final de assets con `ProjectOrganizationChecks.Run`; evidencia de terminación en `output/warhammer/assets.txt`. `git diff --check` pasó.

## 40. Resultado visual

La carrera recorrió 3.199996 m sin obstáculo y se frenó a 0.6100006 m con una pared delante; el objetivo detrás de ella no recibió daño. El torbellino acumuló unos 721.4° en playback real, incluyendo la mezcla de entrada/salida, y alcanzó objetivos delanteros y traseros.

Se renderizaron hojas de poses de los siete ataques y de Idle/Walk/Run, además de la pose guardada. Se capturaron fases de las seis habilidades en Play con el personaje real y cuatro momentos del golpe de vuelta. El básico vuelve a guardia; el segundo carga desde el lado opuesto. Las pruebas registraron el agarre de ambas manos y la vuelta a locomoción sin retener el clip de ataque.

Evidencia: `output/warhammer/Hammer_Attack_Return.png`, `runtime-Basic-return-0.png` a `runtime-Basic-return-3.png`, `runtime-Walk-return.png`, `runtime-Run-return.png`, `runtime-poses.txt`, `runtime-HammerDoubleSpin-2.png` a `runtime-HammerDoubleSpin-6.png` y las hojas `Hammer_*.png`. Las hojas de editor usan materiales de preview neutros para evitar una incompatibilidad de preview; las capturas runtime conservan los materiales reales.

## 41. Alcance y límites

La implementación, las referencias y las pruebas descritas están completas. La apreciación de peso/fluidez se apoya en tiempos, mezclas y revisión visual; no demuestra equivalencia estética con una producción AAA. Quedan fuera de esta validación una sesión humana prolongada, todos los mapas/enemigos, una evaluación auditiva en distintos equipos y un ejecutable standalone completo. No se presenta la regresión acotada de otras armas como cobertura exhaustiva.

## 42. Estado Git final

Incluye trabajo previo del usuario. Muchos assets y scripts Hammer ya estaban sin seguimiento antes del rework. Ninguno de los estados D de contenido ajeno se atribuye a una limpieza de este trabajo.

```text
M Assets/Art/Animations/GoblinConcept/Compatible/0908625edfe9b9d40b60bf3219d47a14_7400000.anim
 M Assets/Art/Animations/GoblinConcept/Compatible/0908625edfe9b9d40b60bf3219d47a14_7400000.anim.meta
 M Assets/Art/Animations/GoblinConcept/Compatible/1a0f9a282c1138d4fbf69d673cfd484b_7400000.anim
 M Assets/Art/Animations/GoblinConcept/Compatible/1a0f9a282c1138d4fbf69d673cfd484b_7400000.anim.meta
 M Assets/Art/Animations/GoblinConcept/Compatible/dd234b55ca31533468e4969cd3a7c8cf_7400000.anim
 M Assets/Art/Animations/GoblinConcept/Compatible/dd234b55ca31533468e4969cd3a7c8cf_7400000.anim.meta
 D Assets/Art/FBX/Nature/desert-stone-3d-low-poly-models.zip.meta
 D "Assets/Art/Source/WeaponCombat/Human Animations/Animations/HumanMeleeAnimationsFREE_BlenderFiles.zip.meta"
 D Assets/Data/Addressables/link.xml
 D Assets/Data/Addressables/link.xml.meta
 M Assets/Data/Gathering/GatheringSettings.asset
 M Assets/Data/Inventory/ItemCatalog.asset
 M Assets/Data/Rendering/Quality/High.asset
 M Assets/Data/Rendering/Quality/Low.asset
 M Assets/Data/Rendering/Quality/Medium.asset
 M Assets/Data/Rendering/Quality/Ultra.asset
 M "Assets/Data/Rendering/Quality/Very High.asset"
 M "Assets/Data/Rendering/Quality/Very Low.asset"
 M Assets/Data/System/RuntimeAssetCatalog.asset
 M Assets/Scripts/Gameplay/Player/Combat/AttackHitbox.cs
 M Assets/Scripts/Gameplay/Player/Combat/BasicSwordCombo.cs
 M Assets/Scripts/Gameplay/Player/Editor/InventoryPresentationAssets.cs
 M Assets/Scripts/Gameplay/Player/Editor/Mismo.Gameplay.Player.Editor.asmdef
 M Assets/Scripts/Gameplay/Player/Equipment/AbilityDefinition.cs
 M Assets/Scripts/Gameplay/Player/Equipment/AbilityExecution.cs
 M Assets/Scripts/Gameplay/Player/Equipment/AbilityRunner.cs
 M Assets/Scripts/Gameplay/Player/Equipment/ExpandedAbilityActions.cs
 M Assets/Scripts/Gameplay/Player/Equipment/Inventory/PlayerInventoryHands.cs
 M Assets/Scripts/Gameplay/Player/Equipment/WeaponPoseProfile.cs
 M Assets/Scripts/Gameplay/Player/Equipment/WeaponPresentation.cs
 M Assets/Scripts/Gameplay/Player/Presentation/CombatFeedback.cs
 M Assets/Scripts/Gameplay/Player/Presentation/QuietFantasyUI.cs
 M Assets/Scripts/Gameplay/Player/Presentation/SwordAnimationFeedback.cs
 M ProjectSettings/EditorBuildSettings.asset
 M ProjectSettings/Packages/com.unity.testtools.codecoverage/Settings.json
 M ProjectSettings/ProjectAuditorSettings.asset
 M ProjectSettings/ProjectSettings.asset
 M ProjectSettings/QualitySettings.asset
?? Assets/Art/Animations/Weapons.meta
?? Assets/Art/Animations/Weapons/
?? Assets/Art/Audio/Hammer.meta
?? Assets/Art/Audio/Hammer/
?? Assets/Art/FBX/Weapons/Hammer.meta
?? Assets/Art/FBX/Weapons/Hammer/
?? Assets/Art/Materials/Weapons/Hammer.meta
?? Assets/Art/Materials/Weapons/Hammer/
?? Assets/Art/Prefabs/Weapons/Hammer.meta
?? Assets/Art/Prefabs/Weapons/Hammer/
?? Assets/Art/UI/Inventory/weapon-Hammer.png
?? Assets/Art/UI/Inventory/weapon-Hammer.png.meta
?? Assets/Art/UI/Weapons/Hammer.meta
?? Assets/Art/UI/Weapons/Hammer/
?? Assets/Data/AnimationAuthoring/HumanoidAttacks/Hammer.meta
?? Assets/Data/AnimationAuthoring/HumanoidAttacks/Hammer/
?? Assets/Data/Combat/Feedback/HammerFeedback.asset
?? Assets/Data/Combat/Feedback/HammerFeedback.asset.meta
?? Assets/Data/Crafting/Recipes/StoneHammer.asset
?? Assets/Data/Crafting/Recipes/StoneHammer.asset.meta
?? Assets/Data/WeaponFamilies/Hammer.asset
?? Assets/Data/WeaponFamilies/Hammer.asset.meta
?? Assets/Data/WeaponFamilies/HammerAnimations.asset
?? Assets/Data/WeaponFamilies/HammerAnimations.asset.meta
?? Assets/Data/Weapons/Hammer.meta
?? Assets/Data/Weapons/Hammer/
?? Assets/Scenes/Validation.meta
?? Assets/Scenes/Validation/
?? Assets/Scripts/Gameplay/Player/Editor/HammerAssets.cs
?? Assets/Scripts/Gameplay/Player/Editor/HammerAssets.cs.meta
?? Assets/Scripts/Gameplay/Player/Editor/HammerChecks.cs
?? Assets/Scripts/Gameplay/Player/Editor/HammerChecks.cs.meta
?? Assets/Scripts/Gameplay/Player/Editor/HammerPolishAssets.cs
?? Assets/Scripts/Gameplay/Player/Editor/HammerPolishAssets.cs.meta
?? Assets/Scripts/Gameplay/Player/Equipment/HammerImpactAction.cs
?? Assets/Scripts/Gameplay/Player/Equipment/HammerImpactAction.cs.meta
?? Informe_tecnico_Warhammer_Rework.md
?? mismo.slnx
?? output/warhammer/
```

## 43. Diff global

`git diff --stat` sólo incluye archivos seguidos; los clips y otros archivos sin seguimiento se ven en el estado anterior. También incluye cambios anteriores ajenos al rework.

```text
...8625edfe9b9d40b60bf3219d47a14_7400000.anim.meta |  2 +-
 ...f9a282c1138d4fbf69d673cfd484b_7400000.anim.meta |  2 +-
 ...34b55ca31533468e4969cd3a7c8cf_7400000.anim.meta |  2 +-
 .../desert-stone-3d-low-poly-models.zip.meta       |  7 ----
 .../HumanMeleeAnimationsFREE_BlenderFiles.zip.meta | 14 -------
 Assets/Data/Addressables/link.xml                  | 24 -----------
 Assets/Data/Addressables/link.xml.meta             |  7 ----
 Assets/Data/Gathering/GatheringSettings.asset      |  1 +
 Assets/Data/Inventory/ItemCatalog.asset            |  1 +
 Assets/Data/System/RuntimeAssetCatalog.asset       |  3 ++
 .../Scripts/Gameplay/Player/Combat/AttackHitbox.cs |  9 +++-
 .../Gameplay/Player/Combat/BasicSwordCombo.cs      |  3 ++
 .../Player/Editor/InventoryPresentationAssets.cs   |  2 +-
 .../Editor/Mismo.Gameplay.Player.Editor.asmdef     | 49 +++++++++++-----------
 .../Gameplay/Player/Equipment/AbilityDefinition.cs |  2 +
 .../Gameplay/Player/Equipment/AbilityExecution.cs  |  2 +-
 .../Gameplay/Player/Equipment/AbilityRunner.cs     |  2 +-
 .../Player/Equipment/ExpandedAbilityActions.cs     |  9 +++-
 .../Equipment/Inventory/PlayerInventoryHands.cs    |  3 +-
 .../Gameplay/Player/Equipment/WeaponPoseProfile.cs |  7 ++++
 .../Player/Equipment/WeaponPresentation.cs         | 18 +++++++-
 .../Gameplay/Player/Presentation/CombatFeedback.cs | 45 ++++++++++++++++++++
 .../Gameplay/Player/Presentation/QuietFantasyUI.cs |  4 ++
 .../Player/Presentation/SwordAnimationFeedback.cs  |  2 +-
 24 files changed, 133 insertions(+), 87 deletions(-)
```

## 44. Mapa final del Warhammer

- Weapon Definition: `Assets/Data/Weapons/Hammer/Hammer.asset`.
- Weapon Family: `Assets/Data/WeaponFamilies/Hammer.asset`.
- Model: `Assets/Art/FBX/Weapons/Hammer/Hammer_Double.fbx`.
- Visual Prefab: `Assets/Art/Prefabs/Weapons/Hammer/HammerVisual.prefab`.
- Equipped / Holstered Pose: `Assets/Data/Weapons/Hammer/HammerPose.asset`.
- Animation Set: `Assets/Data/WeaponFamilies/HammerAnimations.asset`.
- Locomotion Override: `Assets/Art/Animations/Weapons/Hammer/HammerAnimations.overrideController`.

### Golpe de mazo

- Definition: `Assets/Data/Weapons/Hammer/HammerAttack.asset`.
- Animation: `Assets/Art/Animations/Weapons/Hammer/Hammer_Attack.anim`.
- Receta editable: `Assets/Data/AnimationAuthoring/HumanoidAttacks/Hammer/Hammer_Attack.asset`.
- Animation: `Assets/Art/Animations/Weapons/Hammer/Hammer_Attack_Return.anim`.
- Receta editable: `Assets/Data/AnimationAuthoring/HumanoidAttacks/Hammer/Hammer_Attack_Return.asset`.
- Icon: `Assets/Art/UI/Weapons/Hammer/hammer-strike.png`.
- Audio: `Assets/Art/Audio/Hammer/Hammer-Swing.wav`.
- Audio: `Assets/Art/Audio/Hammer/Hammer-Impact.wav`.
- VFX / Feel profile: `Assets/Data/Combat/Feedback/HammerFeedback.asset`.
- VFX de contacto: estela de `HammerPose` e impacto del perfil del Warhammer.

### Carrera demoledora — Q

- Definition: `Assets/Data/Weapons/Hammer/HammerHeavy.asset`.
- Animation: `Assets/Art/Animations/Weapons/Hammer/Hammer_Heavy.anim`.
- Receta editable: `Assets/Data/AnimationAuthoring/HumanoidAttacks/Hammer/Hammer_Heavy.asset`.
- Icon: `Assets/Art/UI/Weapons/Hammer/hammer-heavy.png`.
- Audio: `Assets/Art/Audio/Hammer/Hammer-HeavySwing.wav`.
- Audio: `Assets/Art/Audio/Hammer/Hammer-HeavyImpact.wav`.
- VFX / Feel profile: `Assets/Data/Combat/Feedback/HammerFeedback.asset`.
- VFX de contacto: estela de `HammerPose` e impacto del perfil del Warhammer.

### Golpe sísmico — E

- Definition: `Assets/Data/Weapons/Hammer/HammerGroundSlam.asset`.
- Animation: `Assets/Art/Animations/Weapons/Hammer/Hammer_GroundSlam.anim`.
- Receta editable: `Assets/Data/AnimationAuthoring/HumanoidAttacks/Hammer/Hammer_GroundSlam.asset`.
- Icon: `Assets/Art/UI/Weapons/Hammer/hammer-slam.png`.
- Audio: `Assets/Art/Audio/Hammer/Hammer-HeavySwing.wav`.
- Audio: `Assets/Art/Audio/Hammer/Hammer-Ground.wav`.
- VFX / Feel profile: `Assets/Data/Combat/Feedback/HammerFeedback.asset`.
- VFX de suelo: `CombatFeedback.NotifyGroundImpact` + `WorldSurfaceFeedback`; radio 2.1 m.

### Barrido de guerra

- Definition: `Assets/Data/Weapons/Hammer/HammerSweep.asset`.
- Animation: `Assets/Art/Animations/Weapons/Hammer/Hammer_Sweep.anim`.
- Receta editable: `Assets/Data/AnimationAuthoring/HumanoidAttacks/Hammer/Hammer_Sweep.asset`.
- Icon: `Assets/Art/UI/Weapons/Hammer/hammer-sweep.png`.
- Audio: `Assets/Art/Audio/Hammer/Hammer-Swing.wav`.
- Audio: `Assets/Art/Audio/Hammer/Hammer-Impact.wav`.
- VFX / Feel profile: `Assets/Data/Combat/Feedback/HammerFeedback.asset`.
- VFX de contacto: estela de `HammerPose` e impacto del perfil del Warhammer.

### Avance demoledor

- Definition: `Assets/Data/Weapons/Hammer/HammerCharge.asset`.
- Animation: `Assets/Art/Animations/Weapons/Hammer/Hammer_Charge.anim`.
- Receta editable: `Assets/Data/AnimationAuthoring/HumanoidAttacks/Hammer/Hammer_Charge.asset`.
- Icon: `Assets/Art/UI/Weapons/Hammer/hammer-charge.png`.
- Audio: `Assets/Art/Audio/Hammer/Hammer-HeavySwing.wav`.
- Audio: `Assets/Art/Audio/Hammer/Hammer-HeavyImpact.wav`.
- VFX / Feel profile: `Assets/Data/Combat/Feedback/HammerFeedback.asset`.
- VFX de contacto: estela de `HammerPose` e impacto del perfil del Warhammer.

### Torbellino de guerra — R

- Definition: `Assets/Data/Weapons/Hammer/HammerDoubleSpin.asset`.
- Animation: `Assets/Art/Animations/Weapons/Hammer/Hammer_DoubleSpin.anim`.
- Receta editable: `Assets/Data/AnimationAuthoring/HumanoidAttacks/Hammer/Hammer_DoubleSpin.asset`.
- Icon: `Assets/Art/UI/Weapons/Hammer/hammer-spin.png`.
- Audio: `Assets/Art/Audio/Hammer/Hammer-Spin.wav`.
- Audio: `Assets/Art/Audio/Hammer/Hammer-Impact.wav`.
- VFX / Feel profile: `Assets/Data/Combat/Feedback/HammerFeedback.asset`.
- VFX de contacto: estela de `HammerPose` e impacto del perfil del Warhammer.

### Obtención, registro y sistemas

- Crafting: `Assets/Data/Crafting/Recipes/StoneHammer.asset`.
- Item Catalog: `Assets/Data/Inventory/ItemCatalog.asset`.
- Recetas disponibles: `Assets/Data/Gathering/GatheringSettings.asset`.
- Runtime Catalog: `Assets/Data/System/RuntimeAssetCatalog.asset`.
- Hit de técnicas: `Assets/Scripts/Gameplay/Player/Equipment/HammerImpactAction.cs`.
- Hit del básico: `Assets/Scripts/Gameplay/Player/Combat/AttackHitbox.cs`.
- Combo: `Assets/Scripts/Gameplay/Player/Combat/BasicSwordCombo.cs`.
- Reloj de combate: `Assets/Scripts/Gameplay/Player/Equipment/AbilityRunner.cs`.
- Reproducción de animación: `Assets/Scripts/Gameplay/Player/Presentation/WeaponActionPlayback.cs`.

## Checklist de entrega

- [x] Modelo y prefab del arma existente, escala y pivot revisados.
- [x] Seis habilidades: básico de dos etapas y cinco técnicas.
- [x] Seis iconos y descripciones asignados.
- [x] Clips Humanoid y recetas editables, incluida la vuelta del básico.
- [x] Audio y VFX vinculados y eventos de impacto comprobados.
- [x] Poses equipada/guardada y agarre de dos manos.
- [x] Crafting, equipar, guardar/re-equipar y save/load verificados.
- [x] Selección mediante repertorio actual y controles Basic/Q/E/R.
- [x] Impactos, mezclas, recuperación y locomoción comprobados.
- [x] Regresión funcional de básicos Sword/Bow y compatibilidad de slots/legacy.
- [x] Compilación, pruebas del Hammer y build de contenido sin fallos finales.
- [x] Migraciones con GUID estable; sin una segunda arma duplicada.

Los límites de cobertura quedan expresamente documentados en §41; no se afirma una validación universal de todo el juego.
