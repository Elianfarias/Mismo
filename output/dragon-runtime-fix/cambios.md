# Soul Eater: corrección en el mundo

- El controlador de muestra ya no sobrescribe la animación del combate ni de los vuelos.
- Se conserva el torso voxelizado original al reparar la boca; los clips, ojos y materiales permanecen.
- Cinemáticas: HUD, habilidades, minimapa, marcadores y Canvas ocultos; restauración al finalizar, omitir o interrumpir.
- Escape no abre el menú de pausa mientras una cinemática posee los controles.
- Llamarada y tutorial conservados.

Verificaciones realizadas: `play.txt`, `rules.txt` y `mouth-checks.txt`. Las animaciones de las capturas proceden del SkinnedMeshRenderer del encuentro en la escena del mundo. Las capturas de cámara no incluyen IMGUI; su ocultación/restauración se comprueba como estado de ejecución, además de comprobar Canvas habilitados y previamente deshabilitados.

Para cargar los cambios en una sesión de Unity que ya estaba en ejecución, salir de Play, esperar a que termine la importación/compilación y volver a entrar.
