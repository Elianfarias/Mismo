**Plan de implementación: El despertar del dragón**

Preparado el 25/09/2026 a partir del documento de diseño y una revisión del código actual de Mismo. Es un plan de trabajo: no certifica que el recorrido esté integrado ni probado en Unity.

El objetivo es entregar una demo completa: terminar el tutorial, presenciar el sobrevuelo del dragón, escuchar la explicación del NPC, recibir del rey la misión y la leyenda de los tres faros, preparar la invocación y vencer al dragón. Matar al primer dragón es el final de la demo. La recompensa queda registrada y se presenta en el cierre; las regiones interiores quedan como misterio narrativo. La prioridad de producción es obtener pronto un recorrido completo con geometría provisional y después mejorar encuentros, orientación y presentación.

Actualización acordada: el sobrevuelo bloquea temporalmente movimiento y acciones del personaje, dirige la cámara al dragón que pasa al fondo y acompaña la explicación del NPC. La misión de los faros se recibe al hablar con el rey. El resto del plan conserva su alcance.

**1. Fijar las reglas del primer prototipo**

Decisiones propuestas para convertir el diseño en tareas verificables:

- Un NPC de llegada explica que el dragón causa el aumento de monstruos, nunca se había comportado así y ya no responde a las ofrendas. Sospecha que algo está pasando en las regiones interiores del continente y envía al jugador a hablar con el rey.
- El rey propone la misión principal, «El despertar de la montaña», cuenta la leyenda de los tres faros y explica cómo permiten invocar al dragón. Son dos funciones narrativas y dos personajes, aunque se reutilice la infraestructura de diálogo existente.
- Después de hablar con el rey, la investigación sigue siendo un encuentro breve; confirma una pista de su relato y conduce a tres faros distintos, completables en cualquier orden.
- Los faros sirven para localizar y atraer al dragón. La leyenda debe explicar por qué ese llamado antiguo puede funcionar cuando las ofrendas ya no sirven. El texto preciso de la leyenda queda como propuesta narrativa por desarrollar.
- El foco se obtiene de un guardián concreto y es permanente. No se gasta al invocar, vender, morir o abandonar el intento.
- Si el jugador encuentra un faro o el guardián antes de la explicación, el avance debe reconocerse. Para el prototipo se puede impedir esa interacción con una condición explícita; si la geografía permite alcanzarlos, guardar el hecho y reconciliarlo después.
- La primera victoria es única por mundo y dispara el final de la demo. No requiere volver al pueblo, entregar la misión o recoger un objeto para terminar. Repetir el boss y sus recompensas sería una funcionalidad posterior.
- El primer boss necesita combate terrestre y un patrón aéreo legible. La destrucción de arena, múltiples variantes y una tercera fase elaborada quedan para otra iteración.
- Las partidas antiguas conservan su terreno. El primer alcance puede habilitar el arco sólo en mundos nuevos; incorporar el arco a mundos existentes requiere una migración específica.

**Entrega:** reglas aprobadas como parte del diseño y una lista de contenidos mínimos. No introducir un herrero nuevo o una economía nueva como dependencia del arco.

**2. Aprovechar la base existente y reconocer sus límites**

| Sistema | Evidencia en el proyecto | Trabajo necesario |
| --- | --- | --- |
| Tutorial | `WorldIntroduction` conserva etapas y expone `Complete`. | Añadir una transición semántica hacia la aventura y recuperación al cargar. |
| Misiones | `QuestDefinition`, `QuestRules` y `PlayerInventoryQuests` permiten señales, requisitos e inicio por evento. | Configurar el arco y adaptar cierre por evento y presentación por etapa. |
| Dragón | `DragonBossController`, `DragonBossSettings` y prefabs Firyx tienen ataques, vuelo, aterrizaje y furia. | Controlar la invocación, validar animaciones y conectar derrota, guardado y UI. |
| Encuentro anterior | `BossEncounter` depende de `BossController`; su victoria es un booleano de escena. | Crear un coordinador específico del encuentro del dragón o adaptar un contrato compartido. |
| Progresión | `EnemyProgressionReward` reconoce al dragón; las reglas actuales asignan 200 EXP y 120 de maestría al boss. | Conservar una sola concesión de experiencia y vincularla a la victoria persistente. |
| Guardado | Las misiones y el inventario se guardan en un perfil por mundo; `WorldSession` guarda posición, reaparición e introducción. | Elegir una fuente de verdad para cada dato y recuperar operaciones incompletas. |
| Mapa | `WorldMapInteraction` dibuja pueblos y marcadores personales. | Añadir objetivos de misión con reglas propias de visibilidad e interacción. |
| Reintento | `RegionRespawn` recarga la escena después de morir. | Reconstruir la arena desde los datos guardados y situar un checkpoint seguro. |

La documentación de progresión describe versiones anteriores: el `InventoryProfile` revisado está en versión 6. Cualquier extensión del guardado debe partir del código actual, incluyendo copias, validación y migraciones.

**3. Diseñar la persistencia antes de colocar los faros**

Usar el progreso de misión guardado como fuente de verdad para los faros, el foco y la victoria. El perfil ya es independiente por mundo, por lo que no hace falta un segundo sistema de guardado para este arco.

Asignar IDs estables, por ejemplo:

| Dato | ID propuesto | Persistencia |
| --- | --- | --- |
| Sobrevuelo y explicación completados | `dragon.arrival.seen` | Indicador de llegada guardado |
| Encargo de hablar con el rey | `Q-DRAGON-AUDIENCE` | Objetivo de enlace previo a la misión principal |
| Misión | `Q-DRAGON-AWAKENING` | Progreso de misión |
| Investigación | `dragon.investigated` | Objetivo de señal |
| Faro del bosque | `dragon.beacon.forest` | Objetivo de señal independiente |
| Faro de la ruina | `dragon.beacon.ruins` | Objetivo de señal independiente |
| Faro de la garganta | `dragon.beacon.gorge` | Objetivo de señal independiente |
| Foco | `dragon.focus.acquired` | Objetivo de señal permanente |
| Victoria | `dragon.defeated` | Registrada junto con la recompensa |
| Introducción vista | Indicador del encuentro | Perfil del mundo, extensión mínima |
| Posición de reintento | Coordenadas y orientación válidas | Servicio de mundo |

Los nombres son propuestas, no APIs ya implementadas. Los identificadores de origen de las señales también deben ser estables: no usar `GetInstanceID()` ni un GUID nuevo cada vez que se instancia un faro.

La llegada deja disponible «Hablá con el rey»; la misión principal se acepta en la audiencia. Si el juego se cierra entre ambos registros, reconstruir el objetivo pendiente al cargar. La finalización de la demo se deriva de la victoria guardada; no mantener otro booleano independiente que pueda discrepar de ella.

La disponibilidad se calcula:

```text
Puede invocar = bosque activo
             Y ruina activa
             Y garganta activa
             Y foco obtenido
             Y dragón sin derrotar
```

No guardar además otro booleano independiente `summonAvailable`: podría quedar desactualizado. Tampoco representar los faros mediante una secuencia que presuponga su orden.

Separar ese progreso del estado temporal del combate:

```text
Disponible → Introducción → Combate terrestre ↔ Combate aéreo → Victoria guardada → Fin de demo
                               ↓
                         Derrota / abandono
                               ↓
                           Disponible
```

Al recargar durante un combate, reconstruir un intento limpio y devolver al jugador a un punto seguro. Conservar la preparación; no guardar al dragón en mitad de una animación o una llamarada.

**Criterio de aceptación:** activar un faro, cerrar y continuar conserva exactamente ese faro. Repetir la interacción no suma otro. Una partida nueva no hereda el progreso de otra.

**4. Construir primero un recorrido completo de prueba**

Crear una escena de validación separada o un modo de prueba aislado con la secuencia de llegada, el NPC, el rey, tres objetos interactuables, un guardián, un altar, el dragón y una pantalla de fin de demo. Añadir herramientas de editor para preparar estados de prueba: llegada pendiente, audiencia pendiente, ningún faro, dos faros, invocación disponible y victoria.

En esta primera entrega, los encuentros pueden ser mínimos. La prueba debe recorrer todas las transiciones, conceder una recompensa real y permitir guardar, morir y continuar. Después se conecta el mismo código al mundo principal; no mantener dos implementaciones del arco.

Los componentes nuevos propuestos tienen responsabilidades pequeñas:

| Componente propuesto | Responsabilidad |
| --- | --- |
| `DragonArcDefinition` | Referencias a misión, prefabs e IDs de objetivos. Sólo datos de diseño. |
| `DragonArcCoordinator` | Leer progreso y decidir qué objetivos y elementos del mundo están disponibles. |
| `DragonArrivalSequence` | Coordinar bloqueo temporal de acciones, cámara, vuelo de fondo y diálogo; restaurar el control al terminar. |
| `DragonBeacon` | Interacción y representación de un faro. |
| `DragonSummoningAltar` | Mostrar requisitos y solicitar la invocación. |
| `DragonRegionalEncounter` | Instanciar/controlar el intento y coordinar derrota, recuperación y victoria. |
| `DemoCompletionPresenter` | Mostrar la victoria y recompensa guardadas y ofrecer volver al menú. |

Ubicar la coordinación de misión en el área de `Player/World` o `Player/Quests`, y la integración concreta del dragón en `Enemies/Boss`. Hoy el assembly Enemies referencia Player; evitar que Player referencie a su vez Enemies. Usar un contrato pequeño en un assembly accesible o colocar la coordinación concreta del combate en Enemies.

**Criterio de aceptación:** se puede completar el arco provisional y ver el fin de demo; recargar vuelve al cierre sin repetir el combate ni duplicar el premio.

**5. Integrar el terreno procedural y la llegada al pueblo**

El mundo se genera por semilla y carga sectores alrededor del jugador. Los lugares de la historia deben tener emplazamientos garantizados: no depender de que una tabla aleatoria genere casualmente tres faros y una arena.

Planificar los puntos respecto al primer pueblo, reservar su espacio en la generación, comprobar pendientes y caminos, y excluir apariciones aleatorias incompatibles. Conservar una versión de esa distribución para no desplazar objetivos al continuar un mundo tras una actualización.

La arena necesita navegación suficiente para el tamaño del dragón, espacio para girar la cámara, entrada segura, lugar para invocar y punto de reintento fuera del combate. Fijar sus dimensiones después de medir el prefab y sus ataques.

Los actores cercanos pueden pertenecer a los sectores cargados. La coordinación persistente no debe depender de ellos. Mantener una representación ligera para el humo y los haces de los faros que deban verse a distancia. Evitar que descargar y cargar un sector cree otro boss o borre un faro activado.

En el tutorial, emitir un evento con significado, como «encuentro de entrada completado», después de guardar el resultado. No contar cualquier par de goblins del mundo. Disparar la secuencia una vez que el jugador esté en el punto seguro de llegada al pueblo:

1. Bloquear temporalmente movimiento, ataques y control manual de la cámara. El mundo y las animaciones siguen avanzando; no congelar el sobrevuelo con una pausa global.
2. Llevar suavemente la cámara al encuadre del dragón, que cruza volando al fondo sin atacar ni aterrizar.
3. El NPC identifica al dragón como responsable del aumento de monstruos, explica que su conducta es nueva y que dejó de responder a las ofrendas.
4. El NPC plantea que algo sucede en las regiones interiores y pide hablar con el rey. Usar diálogo/subtítulos del sistema existente; una voz grabada no es requisito del prototipo.
5. Guardar la secuencia completada y habilitar el objetivo «Hablá con el rey».
6. Devolver cámara y control al jugador. El marcador conduce hasta el rey, con quien se habla para escuchar la leyenda y aceptar la misión principal.

Texto provisional del NPC, basado en la dirección acordada: «Ese dragón es el culpable de que haya cada vez más monstruos. Nunca se había comportado así… y ya no responde a nuestras ofrendas. Algo está pasando en las regiones interiores del continente. Tenés que hablar con el rey».

El dragón del sobrevuelo debe ser una representación visual sin IA de combate, daño o recompensas. La secuencia debe liberar su bloqueo de control y restaurar la cámara al terminar, omitirse o interrumpirse. No empezar con enemigos capaces de dañar al jugador inmovilizado. Al cargar durante una secuencia incompleta, reiniciarla desde el punto seguro; al cargar una completada, reconstruir el encargo de audiencia sin repetirla.

**Criterio de aceptación:** durante el sobrevuelo el jugador permanece inmóvil y ve al dragón; después recupera el control y sabe que debe hablar con el rey. Pausar, omitir o recargar no deja la cámara o las acciones bloqueadas. Varias semillas generan rutas transitables sin encuentros duplicados ni superpuestos al tutorial.

**6. Implementar investigación, faros y foco**

Crear el objetivo de audiencia y la misión principal en `Assets/Data/Quests/Missions` y registrarlos en el catálogo existente. `QuestDefinition` exige actualmente un NPC válido incluso para misiones iniciadas por evento: asignar el NPC correspondiente y al rey como emisor de la misión principal. El diálogo del rey presenta la leyenda y la finalidad de los faros antes de aceptar la misión. Para los faros usar objetivos `Signal` independientes, sin imponer `requirePrevious` entre ellos.

La interacción del faro debe consultar el progreso persistido, solicitar el cambio mediante el inventario y encender luz/sonido sólo cuando el guardado haya sido confirmado. Si ya estaba activo, restaurar su aspecto sin emitir una nueva recompensa. El método actual de señales devuelve `false` tanto si no hay un cambio como si no se confirma; distinguir esos casos consultando el estado antes de mostrar errores o reintentar.

Dar a cada lugar una prueba reconocible: lectura de caminos en bosque, altura en ruinas y combate más exigente en garganta. Los seis órdenes posibles de activación deben funcionar. La dificultad no puede asumir que el jugador deja siempre la garganta para el final.

El guardián del foco debe tener identidad persistente propia y entrega garantizada. Una baja de cualquier enemigo de la misma especie no debe completar ese objetivo. El foco puede mostrarse como objeto de misión, pero su posesión no debe depender de espacio libre en la mochila ni de un drop aleatorio.

Registrar la derrota del guardián y el foco en una operación coherente, o recuperar el foco a partir de la derrota ya guardada. Un cierre entre ambas acciones no puede dejar al guardián muerto y al jugador sin posibilidad de invocar.

**Criterio de aceptación:** funcionan todos los órdenes, muerte y descarga de sectores conservan el avance, y el foco nunca queda perdido.

**7. Hacer que la misión oriente al jugador**

El seguimiento actual elige el primer objetivo incompleto de la lista. Adaptarlo para mostrar «Encendé los faros: 2/3» y el destino elegido, en vez de sugerir un orden obligatorio. Mostrar primero la audiencia con el rey y después investigación, preparación, foco e invocación cuando corresponda, sin adelantar todas las tareas en pantalla.

Agregar una capa de marcadores de misión derivada del progreso y del plan del mundo. No convertirlos en marcadores personales que el jugador pueda borrar. Definir por separado las pistas sobre terreno no explorado y los objetivos descubiertos; revelar una pista no debe habilitar automáticamente el viaje rápido.

Combinar mapa, diario y paisaje. Desde un lugar relevante debe reconocerse la amenaza y desde cada ruta debe entenderse hacia dónde continuar. Verificar niebla, distancia de cámara y carga de sectores antes de depender de haces visibles a gran distancia.

**Criterio de aceptación:** un jugador sin ayuda puede explicar su objetivo actual y encontrar al menos un destino válido.

**8. Convertir el altar en el inicio explícito del combate**

El altar muestra faros activos, foco y una acción clara de confirmación. Entrar en la arena no empieza la pelea. Al confirmar, comprobar otra vez los requisitos, evitar solicitudes duplicadas, registrar el reintento seguro y preparar navegación antes de habilitar al boss.

`DragonBossController` busca al jugador automáticamente por defecto. Añadir un inicio explícito del encuentro o instanciar/habilitar el boss únicamente cuando termine la introducción. Un dragón dormido ya presente podría despertar por proximidad o daño si no se controla esa condición.

Persistir que la introducción ya se vio; permitir omitirla en reintentos. Definir abandono, muerte, cierre durante la introducción y muerte simultánea de jugador y boss. Si la victoria ya se guardó, prevalece al recargar. Antes de la victoria, abandonar reinicia al dragón y conserva todos los preparativos.

**Criterio de aceptación:** no hay daño antes de iniciar, confirmar varias veces crea un solo boss y cada reintento comienza en un estado limpio.

**9. Afinar el boss con ataques verificables**

Inventariar los clips referenciados por la variante elegida y reproducirlos sobre su rig real. La presencia de clips no demuestra por sí sola que transiciones y contactos se vean correctos.

| Primer repertorio | Aviso | Respuesta esperada | Ventana de daño |
| --- | --- | --- | --- |
| Garra o mordida | Preparación visible y dirección que deja de seguir al jugador antes del impacto | Esquiva lateral; parry sólo si el ataque lo admite | Recuperación terrestre |
| Aliento | Boca y postura de preparación; área coherente con el efecto | Salir del cono o usar cobertura válida | Fin del aliento |
| Patrón aéreo | Despegue y trayectoria/zona de peligro reconocibles | Reposicionarse hacia espacio seguro | Aterrizaje y recuperación |

El controlador actual usa comprobaciones de alcance y dirección y ventanas proporcionales a la duración de animación. Medir su coherencia visual; separar tiempos por ataque si una fracción común produce golpes adelantados o tardíos. Mantener una sola autoridad de daño: añadir VFX no debe generar un segundo impacto.

Probar cámara cerca de paredes, ataque desde los laterales y cola, interrupciones, pausar durante el vuelo, pérdida de objetivo y aterrizajes. La fase aérea debe tener una duración limitada y ofrecer oportunidades a personajes cuerpo a cuerpo. Ajustar vida y daño según la progresión que realmente consigue el jugador durante el recorrido.

Usar Feel mediante el receptor orbital y canal 7401, respetando el hit stop existente.

**Criterio de aceptación:** el jugador identifica el aviso, sabe qué respuesta intentar y reconoce cuándo puede castigar al boss.

**10. Cerrar victoria, premio y recuperación como una sola operación lógica**

`EnemyProgressionReward` ya concede experiencia al dragón. Extender esa ruta para coordinar la victoria regional; no otorgar otra vez la misma EXP desde la misión. Guardar la derrota, el progreso final y el derecho al premio único en una transacción del perfil, con una identidad de encuentro estable que evite duplicados.

Para la demo, entregar el premio automáticamente o registrar su derecho como botín pendiente persistente si falta espacio. Mostrarlo en el resumen final sin exigir recogerlo en la arena. La mochila llena, la muerte simultánea o cerrar durante la animación final no deben perderlo. Revisar el registro de IDs de recompensa: actualmente existe un premio de boss identificado por `ItemCatalog.BossRewardId`; un premio propio del dragón necesita su definición y registro, o una decisión explícita de reutilizar el existente.

La misión actual se completa mediante entrega. Agregar una política de cierre por evento para esta misión, conservando la entrega a NPC como valor por defecto de las anteriores. La muerte del primer dragón es la condición suficiente para terminar la demo: no agregar una entrega al rey ni un tramo jugable posterior.

En la derrota, detener nuevos daños e invocaciones mientras se confirma la victoria. Dejar terminar la animación de muerte y el feedback, guardar victoria y premio y mostrar «Fin de la demo» con un resumen de la recompensa y una acción para volver al menú. El humo puede apagarse como parte de esa misma presentación. La alusión al interior del continente funciona como cierre narrativo; no desbloquear ni cargar una segunda región jugable.

Al continuar una partida con el dragón derrotado, mostrar el cierre guardado. Un cierre del juego durante la presentación no debe recrear al boss o duplicar el premio. Si el guardado falla, mantener un estado recuperable y reintentar sin presentar el progreso como guardado.

`WorldSession` y el perfil son archivos distintos: no asumir una transacción atómica entre ambos. El estado de campaña debe poder reconstruir o reparar el checkpoint. El autoguardado de posición también debe evitar que Continuar coloque al jugador dentro de un combate que ya se reinició.

**Criterio de aceptación:** matar al dragón conduce al fin de demo sin tareas adicionales. Cerrar inmediatamente después de vencer, tener la mochila llena y morir simultáneamente conservan una sola victoria y un solo premio; Continuar recupera el cierre.

**11. Validar la entrega y ajustar el ritmo**

Ejecutar pruebas automáticas para las invariantes del progreso y pruebas en Play Mode para escena, navegación, combate y UI. Aprovechar `WorldIntroductionChecks`, `QuestFlowChecks`, `DragonVoxelChecks`, `DragonBossPlayChecks` y las comprobaciones de inventario/progresión; algunas regeneran contenido y requieren proyectos aislados, por lo que hay que respetar su modo de ejecución. Añadir una prueba del arco completo.

Casos mínimos: sobrevuelo con control bloqueado; restauración de cámara y acciones al terminar, omitir o interrumpir; recarga antes y después del diálogo del NPC; encargo de audiencia; aceptación de misión con el rey; seis órdenes de faros; señales duplicadas; recarga en cada etapa; foco obtenido anticipadamente si se permite; salida y regreso al sector; doble invocación; pausa e introducción omitida; derrota; guardado fallido; mochila llena; victoria simultánea; cierre de demo y Continuar después de ganar; nueva partida independiente; compatibilidad con guardados anteriores. Comparar varias semillas y verificar accesibilidad.

Cuando se añada contenido o cambie su carga, ejecutar `ProjectOrganizationChecks.Run` y validar una build de la plataforma de entrega. Respetar las carpetas del proyecto y leer la guía de enemigos voxelizados antes de modificar esos prefabs. No usar `Resources` ni corregir problemas regenerando escenas ajenas.

Medir en pruebas humanas: tiempo sin saber qué hacer, tiempo de viaje, orden de faros, nivel/equipo al invocar, daño recibido por ataque y tiempo desde morir hasta volver a pelear. El documento propone 40–73 minutos para el primer recorrido sin reintentos ni desvíos; sus faros de 3–6 minutos suman 9–18 minutos de encuentros. Si se busca que el bloque de faros dure 20–35, el resto corresponde a desplazamiento y exploración. Medir ambos por separado.

**Orden de entregas recomendado:** primero persistencia y recorrido completo provisional; después integración en el mundo y orientación; luego combate y reintentos; por último presentación, balance y build para testers. Cada entrega debe poder jugarse y tener un criterio de aceptación propio.

**Archivos de referencia revisados**

- [Diseño del arco](Gameloop-Despertar-del-Dragon.md).
- [Introducción](../Assets/Scripts/Gameplay/Player/World/WorldIntroduction.cs).
- [Definición de misiones](../Assets/Scripts/Gameplay/Player/Quests/QuestDefinition.cs), [reglas](../Assets/Scripts/Gameplay/Player/Quests/QuestProgress.cs) e [integración de inventario](../Assets/Scripts/Gameplay/Player/Equipment/Inventory/PlayerInventoryQuests.cs).
- [Controlador del dragón](../Assets/Scripts/Gameplay/Enemies/Boss/DragonBossController.cs) y [encuentro anterior](../Assets/Scripts/Gameplay/Enemies/Boss/BossEncounter.cs).
- [Perfil](../Assets/Scripts/Gameplay/Player/Equipment/Inventory/InventoryProfile.cs), [inventario](../Assets/Scripts/Gameplay/Player/Equipment/Inventory/PlayerInventory.cs) y [sesión del mundo](../Assets/Scripts/Gameplay/Player/World/WorldSession.cs).
- [Generación de encuentros](../Assets/Scripts/Gameplay/Player/World/ExplorationContent.cs), [marcadores](../Assets/Scripts/Gameplay/Player/Presentation/WorldMapInteraction.cs) y [diario](../Assets/Scripts/Gameplay/Player/Presentation/QuestJournalView.cs).
