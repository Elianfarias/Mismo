# Goblin base

Abrir `Assets/Scenes/GoblinArena.unity` y entrar en Play. La arena incluye el jugador con su espada, un goblin, dos obstáculos y navegación construida al iniciar. Para repetir el encuentro, salir y volver a entrar en Play.

Controles: WASD, mouse para cámara, click para combo, Q estocada, E parry, R giro, C dash y Shift sprint. La vida del jugador y el estado del goblin aparecen arriba a la izquierda.

## Comportamiento

- Detecta al jugador a 12 metros con línea de visión. Lo persigue por NavMesh y recuerda durante 3 segundos la última posición vista.
- A corta distancia mantiene rango y se mueve lateralmente durante las pausas.
- Golpe: aviso amarillo de 0,65 segundos, ventana de impacto de 0,18 segundos, 12 de daño y recuperación de 0,85 segundos.
- Carga: aviso naranja de 1 segundo, avance de hasta 3,4 metros en 0,4 segundos, 20 de daño y recuperación de 1,2 segundos. Se selecciona entre 2,5 y 5 metros y tiene 5 segundos de cooldown.
- Ambos ataques fijan la dirección al comenzar el aviso. Cada ejecución sólo intenta golpear una vez a cada receptor, incluso si el contacto se rechaza por parry o invulnerabilidad.
- La carga respeta paredes y límites del NavMesh; consulta impactos en subpasos para evitar atravesar objetivos entre frames.
- Los impactos de al menos 15 de daño provocan 0,45 segundos de stagger. Después hay 0,75 segundos de resistencia para evitar interrupciones permanentes. El parry siempre interrumpe el ataque y produce 1,25 segundos de stagger.
- Al morir desactiva navegación y colliders y cancela el ataque. Al perder al jugador, o si éste muere, vuelve a su punto inicial. No regenera vida al volver.

## Ajustes y reutilización

`Assets/Data/Enemies/BaseGoblin.asset` contiene vida, percepción, velocidad, stagger y parámetros de ambos ataques. `Assets/Art/Prefabs/Enemies/Goblin.prefab` contiene el enemigo reutilizable. Los tiempos y cifras son valores iniciales de prototipado, no valores fijados por el GDD.

El menú `Mismo > Prototype > Goblin > Build Goblin Arena` crea los assets faltantes y conserva los existentes. `Add Goblin To Current Scene` agrega una instancia con Undo; `Remove Selected Goblin` permite quitarla con Undo. Fuera de la arena, el prefab necesita un NavMesh existente. `GoblinNavigation` permite construir uno al iniciar a partir del árbol de colliders estáticos asignado a `geometry`; no incluir jugadores ni enemigos en ese árbol. La geometría de la arena es estática y no se reconstruye automáticamente al mover obstáculos durante Play.

El modelo y sus animaciones son provisionales, construidos con bloques. `GoblinPresentation` gestiona el aviso de ataque, los movimientos del arma, el flash de daño, la barra de vida y la caída al morir. Puede sustituirse por presentación animada sin cambiar las reglas de combate.

`Mismo.Gameplay.Enemies` referencia el assembly existente del jugador para reutilizar los contratos de combate y reconocer al jugador. La reacción al parry usa `IParryResponder`, de modo que la espada no depende del tipo Goblin.

## Integración del jugador

La instancia de la arena incorpora `Health`, `DamageReceiver`, `Invulnerability` y `PlayerCombatLife`. Este último cancela habilidades y detiene el controlador al morir. El cinturón básico abre una ventana configurable de invulnerabilidad al iniciar el dash; el valor inicial es 0,12 segundos y está limitado a la duración del dash. No se implementa aún el respawn en pueblo.

## Validación reproducible

`Mismo.Gameplay.Player.Editor.GoblinPlayModeChecks.RunBatch` ejecuta pruebas reales de Play Mode usando la arena y el prefab en una instancia batch de Unity. Comprueba detección, persecución, bloqueo visual por paredes, rutas alrededor de obstáculos, telegraphs, daño único, carga, esquiva lateral, parry, invulnerabilidad del dash, stagger, muerte y pérdida del objetivo. Debe ejecutarse sobre una copia del proyecto si el editor principal lo tiene abierto.
