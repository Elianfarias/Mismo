Vista de armas — Ataques Humanoid

Usar Armas en la vista > Arma principal. Elegir un asset WeaponDefinition del taller. Mostrar segunda arma permite otro objeto; la selección secundaria vacía repite la principal con su agarre secundario. Una definición A dos manos muestra una sola pieza.

Implementación: selección y visibilidad guardadas en el proyecto editable; prefabs, agarres y escala reutilizados del taller; actualización al editar poses, arrastrar IK, recorrer el tiempo y reproducir; limpieza al cerrar. No cambia los perfiles compartidos ni añade curvas del arma a los clips exportados.

Validación: compilación completa del assembly de editor y comprobaciones Unity de seguimiento de ambas manos, rotación/offset/escala, componentes de las copias, ocultación, arma a dos manos, anclajes ausentes, guardado/reimportación y ciclo de ventana. Las pruebas nuevas se ejecutaron en el proyecto aislado con el código de animación y los prefabs/perfiles actuales; los servicios de gameplay ajenos usan fixtures.

La verificación global ProjectOrganizationChecks.Run se ejecutó en el editor principal. Falla por assets existentes fuera de las categorías del proyecto (DoubleL, texturas de Imp e imágenes de conceptos en Art/Source). Detalle en organization.txt. No se reorganizaron esos assets.
