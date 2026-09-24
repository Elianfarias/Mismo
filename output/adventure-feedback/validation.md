# Validación de avisos e interacción

- PASS: compilación Unity 6000.6.0f1 del código de editor y runtime en proyecto aislado.
- PASS: EXP a través de varios niveles y ausencia de avisos ante progreso sin cambios.
- PASS: selección de la acción más cercana y una sola ejecución por frame.
- PASS: drops de enemigos fuera de la mochila, persistencia, victorias idempotentes, mochila llena, fallo de guardado y recogida sin duplicación.
- PASS: reproducción real de Feel en Play Mode, ocultación y suspensión durante pausa, reanudación y final de los avisos.
- PASS: construcción y carga del bundle Windows de la fuente y sus recursos.
- PASS: revisión visual de preview.png y caracteres españoles incluidos.
- PASS: código runtime probado idéntico al entregado y referencias del catálogo principal válidas para los recursos nuevos.

ProjectOrganizationChecks.Run se ejecutó en la copia de validación parcial. No pasó la comprobación global por referencias a arte y audio que no se copiaron al proyecto aislado; organization-isolated.txt contiene el detalle. No se repararon ni eliminaron assets ajenos. Esto no certifica la organización completa del proyecto principal. No se hizo una build completa del juego.
