# Misiones, pedidos y diálogos

## Probar en la partida

- **B → Misiones**, o **J**, abre el diario. Conserva el estilo de Recetas.
- Acercarse a un habitante con **!**, pulsar **T** y elegir **Aceptar pedido**. Hablar, entrar al pueblo o abrir el diario no acepta misiones.
- **Seguir misión** muestra un objetivo en el HUD. Las misiones de materiales muestran el sprite real del objeto y `Tienes / Necesitas` debajo.
- Para cobrar, volver al NPC con **?** y elegir **Entregar pedido**. La conversación requiere cercanía y línea de visión.
- El diario solo muestra misiones aceptadas y completadas. `QuestCatalog.journalContacts` queda desactivado; puede reactivarse exclusivamente para prototipar conversaciones desde el diario.

## Configuración

`Assets/Data/Quests/QuestCatalog.asset` es el punto de entrada: catálogo, contacto desde el diario, teclas, opacidad, posición/tamaño del seguimiento e icono del diario. Las imágenes de sectores del radial se conservan por compatibilidad; el menú actual reutiliza los marcos cuadrados del HUD. Sus referencias se registran bajo `QuestCatalog` en `Assets/Data/System/RuntimeAssetCatalog.asset`.

Crear contenido con **Create → Mismo → Quests**:

- **Quest**: ID permanente, título, categoría, descripción, ubicación orientativa, icono, NPC, requisitos, objetivos, pista y recompensas. `offerInJournal` oculta encargos que deben iniciarse mediante un descubrimiento o evento. No equivale a habilitar el evento en el mundo.
- **NPC dialogue**: ID, nombre, oficio, retrato opcional y textos de saludo, oferta, aceptación, progreso, entrega, cierre y problemas de inventario. Cada misión permite sobrescribir esos textos para el mismo NPC.
- **Catalog and settings**: referencias a las misiones disponibles en esa partida.

Los textos están escritos en español y pasan por `GameLanguage.Text`; para otros idiomas se agregan a las tablas existentes. Editar un texto no cambia la identidad del objetivo. No renombrar IDs de misiones, objetivos, recetas ni fuentes de señal después de publicar partidas que los usen.

### Objetivos

| Tipo | Configurar | Comportamiento |
| --- | --- | --- |
| Material | MaterialDefinition, cantidad, consumir al entregar | Cuenta lo que hay ahora en la mochila, incluido lo obtenido antes de aceptar. El cofre no cuenta. Gastarlo puede volver a dejar el pedido incompleto. |
| DefeatSpecies | CreatureSpecies y cantidad | Cuenta derrotas de esa especie posteriores a aceptar. Usa las victorias persistidas del sistema de combate, sin exigir recoger el botín. |
| CraftRecipe | CraftingRecipe y cantidad de fabricaciones | Cuenta transacciones de fabricación posteriores a aceptar. Una tanda equivale a una fabricación, independientemente de cuántos objetos produzca. |
| Signal | Clave de señal, cantidad, requerir anteriores | Puente para descubrimientos, puzzles y eventos. Cada fuente estable aporta como máximo una vez a cada objetivo. |

Los objetivos son paralelos salvo las señales con `requirePrevious`. No hay límite de tiempo ni repetición automática. Una misión completada va a **Completadas** y no se puede cobrar otra vez. La ubicación es una indicación textual; no agrega un marcador de mapa.

### Recompensas y guardado

- Monedas: se guardan y aparecen en el diario. No hay una tienda nueva ni precios de venta definidos por este sistema.
- Materiales/consumibles: referencia al material y cantidad.
- Recetas: referencia a CraftingRecipe. Marcar `requiresLearning` para bloquear su fabricación hasta aprenderla. Las recetas previas conservan su comportamiento; se añadieron dos recetas aprendibles separadas.
- Al agregar recetas nuevas como premios, ejecutar **Mismo → Quests → Crear ejemplos y registrar catálogo** para incorporarlas al libro y a la build. Las recetas de mano requieren `craftInWorld`; las demás también deben estar asignadas a las estaciones que las fabrican.
- Coste, premios, monedas, recetas y estado completado se escriben juntos en el perfil protegido **v6**. Un fallo de guardado no cobra materiales ni entrega premios. Si no entra la recompensa, el pedido queda pendiente; se contempla el espacio que liberan los materiales entregados.
- Las partidas v1–v5 migran sin perder inventario. No borrar assets de misiones que tengan partidas activas sin planear una migración de contenido.

## NPCs y contenido de ejemplo

| NPC | Misiones |
| --- | --- |
| Mara, herborista | Hierbas para el botiquín; Antes de volver a salir (requiere completar la primera). |
| Bruno, herrero | El primer encargo: hierro a cambio de monedas y receta de afilado de campaña. |
| Elena, molinera | Jabalíes en el camino: caza posterior a aceptar. |
| Iria, guardiana del archivo | El altar de los cuatro vientos; Un eco entre las piedras. |
| Tomás, vigía | El pueblo bajo asedio. |

Los premios son valores iniciales editables, no un balance final. Los cuatro pedidos iniciales usan mecánicas existentes. El altar, el asalto y la anomalía no aparecen como encargos normales para evitar ofrecer contenido que todavía no está colocado o conectado al mundo.

Los cinco habitantes aparecen en el pueblo inicial generado, también en partidas que conservan el centro antiguo. El grupo se descarga con el pueblo; recrearlo no reinicia el progreso ni acepta pedidos. Los prefabs base miden 1,75 unidades; `VillageNpcSettings.residentScale = 1.3` los lleva a 2,275 unidades, independientemente del multiplicador del pueblo. La colisión y la altura de los marcadores acompañan esa escala.

El pueblo usa `WorldContentCatalog.villageSizeMultiplier = 2.6`: casas, puertas, calles y murallas son un 30% mayores que con el valor anterior (2). La revisión de distribución 2 actualiza el punto de aparición de partidas previas. Si el personaje estaba dentro de un pueblo, lo coloca una sola vez en la entrada para evitar que una casa ampliada lo encierre; conserva las posiciones de exploración lejanas y el progreso.

`QuestCatalog.interactionPromptOffset` ajusta el aviso **[T] Hablar** desde el centro inferior de la pantalla. Su posición predeterminada deja lugar al aviso del cofre/botín y a la barra de habilidades.

`Assets/Data/Quests/VillageNpcSettings.asset` permite configurar prefabs, posiciones locales del pueblo, orientación, distancia de conversación, línea de visión, alcance/tamaño/altura/colores de marcadores y nombres. `startingVillageOnly` evita repetir los mismos personajes en cada asentamiento. El catálogo de misiones referencia esta configuración directamente y sus dependencias entran en la build.

Los prefabs de habitantes están en `Assets/Art/Prefabs/Quests/Village`. Cada `QuestGiver` tiene NPC, misiones, distancia y texto de interacción. `eventQuests` enumera encargos que solo se atienden cuando su controlador ya los inició: Iria y Tomás tienen saludo, pero no anuncian contenido pendiente de conectar. Prioridad de conversación: entrega lista, pedido nuevo y encargo activo. El diálogo se cierra si el personaje desaparece, queda fuera de alcance o se interpone una pared.

Marcadores: **!** dorado para un pedido disponible, **?** verde para entregar y **…** blanco para un encargo en curso. Desaparecen al completar todos los pedidos y se ocultan detrás de geometría o mientras un menú está abierto. El siguiente encargo de Mara requiere volver a hablar y aceptarlo explícitamente.

Los 14 modelos únicos de `fbx/people_unity` se procesaron con **Voxelizar modelo**, resolución máxima 64, conservando rigs y paleta. Se guardaron en `Art/Prefabs/Voxelized/NPC/Craftpix`, `Art/Meshes/Voxelized/NPC/Craftpix` y `Art/Animations/Voxelized/NPC/Craftpix`. La textura y materiales están en sus categorías `Art/Textures/Characters/NPC/Craftpix` y `Art/Materials/Characters/NPC/Craftpix`. El `.tx` original está en `Art/Source/Characters/NPC/Craftpix` y la licencia/documentación en `Assets/Documentation/NPC/Craftpix`. Los traslados usan `AssetDatabase.MoveAsset` y verifican GUIDs; no se borraron modelos originales ni exportaciones alternativas.

El paquete no contiene clips de animación. Los habitantes usan una pose de brazos descansados y `VillageNpcIdle` con respiración y giro de cabeza ajustables. Para animaciones futuras se conservan los huesos y pesos de los voxelizados.

## Puzzle y eventos

`Assets/Art/Prefabs/Quests/FourWindsAltar.prefab` es un ejemplo funcional, sin colocación automática en escenas. Al entrar al trigger, se registra `altar.found` únicamente si ya se aceptó la misión. `QuestSignalTrigger.acceptAutomatically` está desactivado por defecto. Cada guardián gira 90° con T. Su flecha indica la orientación. El orden se define en `CompassAltar.stones` y la solución en `solution`; norte corresponde al eje +Z local con ajuste `northYaw` por piedra.

El prefab incluye un punto para leer las notas de Iria y resolver la entrega durante el prototipado. Sustituirlo por su NPC cuando exista. Asignar fuentes únicas si se duplican altares. Las posiciones intermedias de las piedras se reinician al cargar; la resolución guardada se restaura al interactuar. `onSolved` permite abrir una puerta o activar un efecto después de persistir la resolución.

Para conectar futuros eventos:

```csharp
// API opcional para un evento que deliberadamente acepte misiones sin diálogo.
// Para mantener la aceptación presencial, habilitar el pedido en QuestGiver al comenzar el evento.
inventory.TryStartQuestEvent(quest);
inventory.TryRecordQuestSignal("raid.defended", "raid:town:first");
inventory.TryRecordQuestSignal("anomaly.found", "anomaly:forest:first");
inventory.TryRecordQuestSignal("anomaly.closed", "anomaly:forest:first");
```

`QuestSignalTrigger.Emit(player)` ofrece el mismo puente desde un componente con clave, fuente y misión inicial opcional. Las señales anteriores a aceptar no se acumulan. Una señal duplicada o irrelevante devuelve false sin escribir. Si falla el guardado, repetir la misma fuente es seguro. Los controladores de asaltos/anomalías, sus oleadas y su generación no forman parte del sistema de misiones.

## Herramientas y verificación

El seguimiento de misiones aparece por defecto a la izquierda, debajo de las barras. `QuestCatalog.trackerOffset` define el margen desde la esquina superior izquierda y `trackerWidth` su ancho. En partida: **Esc → Opciones → Controles → Modificar UI**, arrastrar el panel y pulsar **Guardar**. También aparece una muestra si no hay una misión seguida. La posición personal persiste en `PlayerPrefs` (`Mismo.HUD.Quests.Position.x/y`), se limita a la pantalla y no modifica los datos de misiones. Clic derecho sobre el panel durante la edición elimina esa preferencia y restablece la posición del catálogo.

- **Mismo → Quests → Crear ejemplos y registrar catálogo** crea únicamente assets faltantes y preserva la configuración de assets existentes. No regenera escenas.
- **Mismo → Quests → Preparar NPCs voxelizados del pueblo** crea modelos y habitantes faltantes; conserva prefabs y posiciones ya editados.
- `VillageNpcChecks.Run` comprueba modelos, referencias, escala humana, navegación desde la entrada en tres semillas y en el pueblo antiguo, descarga y prevención de duplicados.
- `QuestFlowChecks.Run` se ejecuta en una copia `.validation`: comprueba transacciones, recetas, requisitos, caza, señales, puzzle, guardado y navegación; captura diario, diálogo, HUD y radial.
- `QuestIntegration.Build` verifica organización y construye Windows desde la copia aislada.
- Arte del sistema: `Assets/Art/UI/Quests`; prefab: `Assets/Art/Prefabs/Quests`; datos: `Assets/Data/Quests`; código: `Assets/Scripts/Gameplay/Player/Quests` y herramientas en `Assets/Scripts/Editor`.
