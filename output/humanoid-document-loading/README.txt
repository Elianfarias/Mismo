Corrección de carga del proyecto Humanoid

El cambio de PreviewSceneStage puede descargar los assets nativos referenciados sólo por variables C#. LoadRecipe conservaba esa referencia al ejecutar CopySerialized, provocando MissingReferenceException.

LoadRecipe ahora conserva el GUID antes de cerrar la vista y resuelve nuevamente el asset después. Recupera el borrador si se perdió, conserva el vínculo del proyecto guardado por GUID y nunca destruye un asset persistente al cerrar la ventana.

Validación en Unity aislado: reproducción del error anterior; carga desde una vista abierta con poses, duración y ambas armas; guardar cambios y reabrir la misma configuración con el mismo GUID; rechazo de referencias inválidas conservando el proyecto actual; recuperación del borrador desde el asset guardado. También pasaron las comprobaciones anteriores de animación, IK y armas.

Compilación del assembly de editor completo: PASS. No se modificaron los proyectos de ataque ni los perfiles de armas del usuario.
