# Recolección y crafting de superficie

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
