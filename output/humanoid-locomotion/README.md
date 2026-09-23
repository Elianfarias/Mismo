# Locomoción Humanoid del aventurero

Se conservaron las seis animaciones Quaternius existentes (Idle, Walk, Run, Jump, Fall y Land) y se convirtieron sus poses a curvas Humanoid. Los nuevos clips están en `Assets/Art/Animations/Quaternius/Humanoid`. `VoxelLocomotion.controller` ya los utiliza. Los originales de `Retargeted` siguen siendo la referencia Generic y no deben asignarse al personaje Humanoid.

El FBX y su Avatar Humanoid, las animaciones de DoubleL y los datos de espada existentes no se modificaron. El controlador incorpora una capa vacía de peso cero, `Humanoid Mask Support`: en Unity 6000.6, las pruebas demostraron que el controlador de una sola capa dejaba pasar movimiento de piernas del ataque a través de la máscara superior. Con esta capa, las pruebas de mezcla mantienen exactamente la posición de las piernas.

## Verificación realizada

- Los seis clips se compararon en Unity con las poses originales, en 24 momentos por clip. Error máximo de posición de los huesos comprobados: 6,1 mm en Walk y 8,3 mm en Run; menos de 2 cm en todos los clips. Véase `checks.txt`.
- Se reprodujo el controlador mediante el reproductor real `WeaponActionPlayback`, en cinco velocidades entre reposo y carrera, junto con un ataque Humanoid DoubleL y `PlayerUpperBody.mask`. Se comprobó movimiento continuo, altura del cuerpo, conservación de piernas durante el ataque y recuperación de la pose. Véase `integration-checks.txt`.
- Se construyó un AssetBundle para Windows y se cargó el controlador compilado. Las seis animaciones Humanoid están incluidas entre sus dependencias. Esto verifica la build de las animaciones; no es una build completa del juego.
- Compilaron las dos asambleas de editor afectadas con las referencias del proyecto. Los avisos están registrados en `player-editor-compile.txt` y `project-editor-compile.txt`; son avisos de archivos existentes.
- `installed-assets.txt` confirma que los clips instalados coinciden con los comprobados.

## Al regresar al editor principal

Dejar terminar la importación de Unity; si no se actualiza automáticamente, usar Assets > Refresh. Mantener el aventurero como Humanoid.

Se dejó solicitada una verificación de las referencias del Player y `ProjectOrganizationChecks.Run` para la siguiente recarga de scripts del editor principal, sin cerrar su sesión ni modificar la escena. Esos resultados aparecerán en `project-checks.txt` y `organization-checks.txt`. En el momento de la entrega, esta comprobación del editor principal está pendiente de su recarga. El paquete DoubleL existente en la raíz de Assets puede producir incidencias de organización; su reorganización no forma parte de este arreglo.

Para regenerar los clips: Mismo > Character > Convertir locomoción Quaternius a Humanoid. Para repetir las comprobaciones del proyecto: Mismo > Character > Verificar locomoción Humanoid.
