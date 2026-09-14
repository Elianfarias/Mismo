# Recolección y crafting de superficie

Actualización de recetas: ver `Assets/Resources/Recipes/README.md`. La expansión incorpora hierro, cristal arcano, poción de combate, piedra de afilar, mejoras T2–T4 y fabricación de espada/arco. Los apartados históricos de este archivo que describen todas las menas como piedra corresponden a la importación inicial; las tablas vigentes distinguen los nuevos materiales.

## Probar

Entrar a una partida de exploración. Cerca del punto de llegada se coloca un banco y una fuente inicial de hierbas, madera y piedra cuando el terreno tiene soporte y espacio libre. Acercarse y presionar **G** para recolectar o abrir el banco. **ESC** cierra el banco. **F** conserva las interacciones de cofre y botín.

- REC-01: 2 hierbas → 1 ungüento. En el inventario, seleccionarlo y pulsar **Usar**. Recupera 40 de vida durante 5 segundos, fuera de combate; recibir daño interrumpe la recuperación. No se gasta con vida completa ni mientras otro ungüento está activo.
- REC-02: 2 madera + 3 piedra + 1 componente de monstruo → mejora T1 a T2 del arma seleccionada. El botón superior alterna las ranuras equipadas. Conserva identidad, variante y maestría; no se puede repetir sobre T2.
- Moverse, atacar, esquivar, saltar o recibir daño cancela la recolección. El material y el agotamiento del nodo se guardan juntos al terminar.
- La mochila sigue usando capacidad por grilla. Una recompensa que no entra no consume el nodo. La fabricación considera el espacio que liberan sus ingredientes.

## Assets configurables

- `Assets/Resources/GatheringSettings.asset`: fuentes por tipo, recetas disponibles, prefab del banco, botín predeterminado de goblins, densidad, separación y radio inicial. `groupsPerChunk` representa intentos de colocación, actualmente de un nodo cada uno.
- `Assets/Resources/Gathering/*Node.asset`: duración, alcance, tiempo de regeneración, distancia para restaurar, prefabs disponible/agotado/herramienta, sonidos y tabla de recompensas.
- `Assets/Resources/Materials`: objetos independientes de las fuentes; IDs únicos, nombre, descripción, icono, tamaño en grilla, apilado y propiedades de curación. MAT-05 es el ungüento.
- `Assets/Resources/Recipes`: ingredientes, cantidades, producto o transición de tier. Crear recetas con **Create → Mismo → Crafting → Recipe**, luego agregarlas al array de recetas de GatheringSettings o de una estación manual.
- `EnemyProgressionReward.materialLoot`: permite asignar una tabla a cualquier monstruo. Los goblins sin tabla explícita usan la de GatheringSettings; se respetan sus tablas propias.
- `Assets/Art/Gathering`: modelos y sonidos básicos reemplazables. Los materiales están preparados para URP. La caída del árbol es solamente visual.
- Para nodos manuales, agregar GatheringNode y asignar definición e ID persistente único dentro de la partida. Para bancos manuales, usar CraftingStation con sus recetas y alcance.

**Mismo → Crafting → Create surface defaults** vuelve a establecer los valores de las recetas y del ungüento de ejemplo; no ejecutarlo para conservar modificaciones de esos ejemplos. No se ejecuta automáticamente al abrir el proyecto.

## Persistencia e idiomas

El perfil v5 migra partidas anteriores y guarda reloj de juego y fechas de regeneración. No usa tiempo offline ni avanza con Time.timeScale = 0. El reloj se guarda cada 15 segundos y al pausar/salir; las transacciones incorporan el reloj actual. Un cierre abrupto puede perder hasta ese último intervalo de avance, sin separar el premio de su estado agotado.

Los nodos procedurales usan versión de generación, semilla, chunk y posición lógica como identidad. Los agotados se restauran al vencer su plazo cuando el jugador está fuera de la distancia configurada. Esta condición usa proximidad, no visibilidad de cámara.

Los textos nuevos están en las tablas español/inglés de `Assets/Resources/Localization` (147 entradas totales). Agregar nuevas frases a Translations.json y actualizar las tablas con el importador existente. El cambio de idioma selecciona traducciones preparadas; no llama a un traductor en línea.

## Estado respecto al instructivo

Implementados: interacción con los tres tipos de recurso, interrupciones, recompensa transaccional, regeneración persistente, colocación por bioma, recursos iniciales, botín de goblins, banco, recetas REC-01/02 y curación gradual.

Pendiente de aceptación en mundo real: recorrido completo en varias semillas, disponibilidad/accesibilidad de los puntos iniciales y ajuste de densidad/rendimiento. La colocación busca soporte y evita caminos/obstáculos, pero todavía no se verificó la garantía para todas las semillas. No se incluyen cuevas, minerales avanzados, domesticación ni monturas.

## Verificación realizada

- 88 comprobaciones del núcleo de inventario y guardado.
- 28 comprobaciones en Play Mode de Unity 6000.3.11, en copia aislada: interacción G, agotamiento único, persistencia, pausa, regeneración, cancelación por daño, mochila llena, recetas, curación y errores de guardado con reintento.
- Compilación de Player, Enemies y Player.Editor contra Unity 6000.6 sin errores.
- Captura del banco revisada en `output/gathering/crafting.png`. La prueba aislada usa una escena plana; no sustituye la aceptación del mundo procedural ni una revisión visual con URP en la partida principal.

## Minerales del FBX y rosa recolectable

- Se separaron los 30 modelos de `Assets/Art/FBX/Minerals/SimplePolygon_Minerals.fbx` en prefabs de `Assets/Art/Gathering/Minerals`. Conservan sus mallas y UV del FBX original, con material de paleta para URP, pivote al suelo y ancho máximo de 1,35 m.
- Cada modelo tiene su ResourceNodeDefinition y su propia tabla `*-Loot.asset` en `Assets/Resources/Gathering/Minerals`. Inicialmente todos entregan Piedra (MAT-03), compatible con REC-02. Los colores no asignan por sí mismos nuevos minerales o rarezas: cambiar la tabla para entregar otro asset de material cuando se definan esos recursos.
- `GatheringSettings.minerals` contiene las variantes usadas en la distribución procedural. Se eligen a partir de la semilla y el chunk, sin alterar la identidad persistente de los nodos existentes. La mena inicial también usa un modelo importado.
- `flower_rose` reemplaza la planta provisional de HerbNode y el modelo/icono de Hierba medicinal (MAT-01). Las rosas que ya distribuía WorldContentCatalog ahora son recolectables, sin duplicar la decoración en ese punto. Al recolectarlas desaparecen y vuelven después del tiempo configurado. MAT-01 sigue siendo el ingrediente del ungüento, conservando las partidas y recetas actuales.
- El campo opcional `gatheringNode` de WorldAssetEntry permite convertir otras decoraciones del catálogo en recursos. El prefab visual debe estar asignado en la definición del nodo.
- El importador **Mismo → Crafting → Import mineral models and rose** configura los modelos y la conexión con la rosa. Al repetirlo conserva las tablas de botín y parámetros de los nodos existentes; vuelve a asignar sus visuales, la lista de variantes y los iconos de hierba/piedra.

Verificado: 30 prefabs independientes apoyados en el suelo, tablas con recompensa, conexión de la rosa y captura visual; 30 comprobaciones de interacción/guardado/crafting en Play Mode aislado. Player, Enemies y Player.Editor compilan contra Unity 6000.6. La captura `output/gathering/minerals.png` muestra los modelos en una escena de prueba; la densidad por semilla sigue pendiente de balance en partida.

## Recursos en el paisaje existente

Los árboles del catálogo, tanto en los chunks como en el bosque central, ahora se generan como recursos conservando el prefab, la rotación y la escala del paisaje. El árbol inicial también usa Tree_0 del catálogo en lugar del modelo provisional. El camino alternativo de árboles generados a partir de mallas conserva su geometría al convertirlos en nodos.

La decoración distribuida tiene estas recompensas predeterminadas:

- Árboles y madera caída: tabla de WoodNode.
- Piedras: tabla de StoneNode.
- Flores y arbustos: tabla de HerbNode.
- Pasto: decoración.

El campo gatheringNode de cada entrada de WorldContentCatalog permite asignar una definición con otra recompensa, duración o regeneración. Para estas entradas se conserva el prefab del catálogo como aspecto disponible. Los minerales especiales siguen usando sus nodos y tablas independientes. El peso de cada entrada del catálogo permite reducir la frecuencia de un árbol raro.

Los recursos del paisaje tienen identidad persistente por semilla y ubicación lógica. Al agotarlos se retiran su visual y colisión; los árboles tienen la caída visual y después desaparecen. Al regenerar se restaura el mismo modelo. Se solicita actualizar la navegación cuando cambian los obstáculos. Los componentes del prefab (incluida la escalada) se conservan mientras está disponible.

Para ver la nueva generación en una partida que estaba ejecutándose, salir de Play y volver a entrar. No hace falta borrar el guardado. Pendiente: balancear cantidades/tiempos con la nueva abundancia de fuentes.

Validación de esta integración: 52 comprobaciones en Play Mode aislado, incluyendo modelos reales de árbol, piedra, madera caída, arbusto, flor y pasto; compilación de Player, Enemies y Player.Editor contra Unity 6000.6 sin errores.

### Decoracion por asset
En Assets/Resources/WorldContentCatalog.asset, desplegar Assets y la entrada deseada. Decorative Only activado evita la recoleccion y tiene prioridad sobre Gathering Node y la categoria. Desactivado conserva la asignacion automatica por categoria; Gathering Node permite elegir un recurso especifico. nature.bush_0 y nature.bush_1 son decorativos; flower_rose sigue siendo recolectable. Salir de Play y volver a entrar para regenerar las instancias con la configuracion nueva.
