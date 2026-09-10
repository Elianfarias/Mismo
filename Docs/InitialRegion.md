# Región inicial de Scope 0

Abrir `Assets/Scenes/InitialRegion.unity` y entrar en Play. Es un blockout manual: pueblo al sur, sendero hacia el norte, desvío a la ruina al este y boss al final. Los materiales y edificios son provisionales.

## Contenido

- Un jugador y cámara existentes, sin cambios en controles o habilidades.
- Cuatro goblins base y un Elite visual, colocados manualmente.
- Ruina abierta con plataformas, rampa de retorno y restauración del 40 % de vida máxima.
- Boss existente con puerta de salida e indicador de victoria.
- Muerte: tras dos segundos se recarga la región y comienza un intento nuevo desde el pueblo. Enemigos, recompensa y boss se restablecen; no hay guardado.

La restauración no se consume con salud completa. Al tocarla con vida faltante cura una sola vez y desaparece. No agrega inventario ni equipamiento.

## Edición

`Mismo > Prototype > Region > Build Initial Region` construye la escena solamente si no existe. Nunca sobrescribe una región editada. El constructor conserva las escenas de combate originales. La geometría creada queda dentro de `Initial Region`; puede editarse normalmente en Unity. La creación del grupo se registra en Undo.

`RegionGeometry` contiene los colliders estáticos usados por `GoblinNavigation`. Mantener jugadores, enemigos, recompensa y puerta fuera de esa jerarquía. La navegación se genera al iniciar y no sigue cambios de geometría hechos durante Play.

Asignar explícitamente boss, puerta y recompensa en `BossEncounter`. La puerta alta y los riscos posteriores bloquean la salida antes de ganar, incluso con doble salto. La entrada permanece abierta y permite retirarse.

## Comprobaciones

`Mismo.Gameplay.Player.Editor.InitialRegionChecks.RunBatch` ejecuta comprobaciones de Play Mode en una copia del proyecto: referencias, cinco goblins, navegación hasta encuentros y boss, recompensa con salud completa y dañada, victoria y recarga desde el pueblo. Ejecutar sin `-quit`; el método termina Unity con el resultado.

La validación automatizada no sustituye recorrer el mapa para ajustar cámara, saltos, legibilidad y densidad. La duración de 8–12 minutos del plan es una aspiración de diseño, no una medición de esta primera versión.

Resultado del 7 de septiembre de 2026: compilación y comprobaciones de Play Mode aprobadas en Unity 6000.3.11f1, en una copia temporal. Evidencia en `Docs/Validation/InitialRegion-checks.txt` y vista general en `Docs/Validation/InitialRegion.png`. El editor emitió una excepción de su indexador `UnityEditor.Search` al iniciar; no impidió ejecutar ni aprobar las pruebas de gameplay.

No se agregó generación procedural, cambios de estadísticas, nuevas armas ni sistemas de progresión.
