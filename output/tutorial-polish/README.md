# Correcciones de conversación y presentación del tutorial

Al iniciar una conversación válida con Liria, el objetivo de hablar con ella avanza y se guarda inmediatamente. Omitir con el botón o cerrar con Esc ya no lo deja pendiente. Repetir la conversación no adelanta el combate siguiente. En una partida anterior que quedó en ese paso, basta con volver a hablar con Liria.

El aviso de objetivo usa los límites reales del minimapa: se coloca debajo o, si el mapa está ampliado y queda poco espacio vertical, a su izquierda. El panel central y el fondo oscurecido aparecen con un fundido de 0,28 segundos; la tarjeta se desplaza suavemente 14 unidades. El cambio de página tiene un fundido de 0,16 segundos sin volver a oscurecer el fondo. Las transiciones usan tiempo independiente de la pausa.

`checks.txt`: 150 comprobaciones aprobadas en Play Mode, sin errores de ejecución. Incluyen el recorrido completo, guardado y recarga, conversación omitida durante la entrada, repetición del diálogo, avance del fundido con el tiempo de juego pausado y 20 combinaciones de resolución y tamaño de minimapa. Se usaron perfiles de prueba aislados.

`objective-before-talk.png` y `objective-after-talk.png` muestran el cambio de objetivo y la posición respecto del minimapa. `dialogue-transition.png` muestra una fase intermedia del fundido; `liria.png`, la tarjeta completamente visible.

Build actualizada: `Builds/2026-09-25-tutorial-fix/Mismo.exe`, Windows x64, Unity 6000.6.0f1. Compilación correcta con 0 errores y 499 advertencias; `build.txt` y `build-messages.txt` registran el resultado. Organización del proyecto: PASS en `organization.txt`.

El ejecutable pasó la comprobación independiente de arranque, menú, partida nueva, cueva, Liria, pueblo, cinco habitantes y materiales compatibles. `smoke-checks.txt` registra ese resultado. Esta comprobación visita destinos mediante teletransporte; el recorrido completo del tutorial se verificó en Play Mode. Se usó almacenamiento aislado de las partidas del usuario.

ZIP distribuible: `Builds/Mismo-Windows-2026-09-25-tutorial-fix.zip` (193.340.667 bytes, 225 archivos). Contiene instrucciones de ejecución y excluye la carpeta de respaldo interna de Unity.

SHA-256: `68FF8CB846A51CA3D4C0B369AE8B1AC22D7BE7BB24A50B03E72E21402E9ED1B3`.
