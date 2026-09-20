# Animaciones de Quaternius aplicadas

Fuente: Universal Animation Library Standard, publicación de Quaternius en https://opengameart.org/content/universal-animation-library . Licencia original en Assets/Documentation/Art/Animations/Quaternius/License.txt. Copia descargada desde OpenGameArt; no se afirma que sea la versión más reciente de itch.io.

Reemplazos en VoxelLocomotion:
- Idle → Idle_Loop
- Walk → Jog_Fwd_Loop (trote, acorde a la velocidad normal del juego)
- Run → Sprint_Loop
- Jump → Jump_Start
- Fall → Jump_Loop
- Land → Jump_Land, ajustado a la ventana de aterrizaje existente

Los archivos retargeted se hornearon sobre los huesos actuales a partir de una pose de referencia alineada, corrigiendo la diferencia de eje frontal entre FBX. Requieren un Avatar Generic: son curvas de Transform y no curvas de músculos Humanoid. Las rutas de huesos y los siete estados de combate/dash se conservan. Los override controllers de espada y arco referencian el mismo controlador base y no tienen reemplazos de locomoción.

Validación: se renderizaron 18 poses reales con mallas actualizadas; todas las curvas resuelven sus huesos; el árbol usa Quaternius; se conservaron los clips originales de combate. Vista previa: Docs/Validation/Quaternius-preview.png, filas Idle/Walk/Run/Jump/Fall/Land. La prueba aislada no sustituye una sesión de juego en la escena final.

Herramienta reproducible: Mismo > Character > Apply Quaternius Locomotion. Respaldo del controlador anterior: Docs/Validation/QuaterniusBackup/VoxelLocomotion.controller.txt. El generador del personaje también reutiliza los clips nuevos cuando están disponibles.

Corrección del 2026-09-10: el FBX del proyecto estaba importado como Humanoid, mientras que la copia de validación anterior estaba en Generic. Se reprodujo el fallo en una copia completa de VoxelRegion_7319: suelo Y=4, raíz del jugador Y=4.04, cadera Y=3.84 y pie izquierdo Y=3.39, sin variación de pose. Al cambiar únicamente animationType a Generic (2), la cadera vuelve a Y=4.80–4.81, el hueso del pie a Y=4.21 y la pose de reposo avanza. El generador ahora asegura Generic y VoxelAnimationChecks rechaza la importación incompatible. Estas mediciones son de huesos, no de la suela de la malla. Las pruebas usan una identidad de aplicación separada para no escribir partidas del usuario.

La prueba de movimiento en la misma escena confirmó Idle → Walk → Run → Idle con variación de los huesos. Evidencia: Docs/Validation/Quaternius-scene-before.txt y Quaternius-scene-after.txt. Compilación de Mismo.Gameplay.Player.Editor: 0 errores (16 advertencias preexistentes). La ventana interactiva del usuario no se pudo activar mediante el control de escritorio; se requiere recargar los assets/reiniciar Play allí.
