# Introducción integrada al continente

Probar en Unity: `Assets/Scenes/MainMenu.unity` → Play → Nueva partida. El acceso de editor es `Mismo > Demo > Abrir inicio real (Nueva partida)`.

Seis salas de cristal y más de 90 metros entre salas; explicación de Shift; salida a EnchantedGrove; conversación con Liria; encuentros guiados; arco mantenido y Focus; llegada al pueblo procedural con sus NPCs y misiones. El avance se conserva al continuar la partida.

- `checks.txt`: 119 comprobaciones aprobadas en Play Mode; incluye seis semillas, navegación interior completa, menú real, encuentros y recargas a mitad y al final del tutorial. Viaje y bajas automatizados.
- `arrival.png`, `cave.png`, `liria.png`, `grove.png`, `village.png`: capturas de Unity. `grove.png` se actualiza después de quitar la laguna, el puente y los nenúfares solicitados.
- `build.txt`: Unity produjo el ejecutable Windows, con un error de auditoría; no es una build sin errores. La build corresponde a la integración anterior a la última corrección estética del claro.
- `organization.txt`: imágenes y un prefab preexistentes fuera de las carpetas esperadas. No se reorganizaron assets ajenos.
- `player-smoke.log`: arranque del ejecutable y carga del menú, sin errores de ejecución. No se probó el recorrido completo en el ejecutable.

Los perfiles de las verificaciones están aislados de la partida del usuario. La cueva original se conserva; los nuevos prefabs, mallas y datos están bajo las carpetas Art y Data correspondientes.
