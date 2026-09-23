# Desplazamiento de animaciones por habilidad

En **Mismo > Armas > Familias y animaciones > Actions**, cada habilidad permite activar **Usar desplazamiento de la animación** y ajustar **Multiplicador del desplazamiento**. Requiere Full Body o ausencia de máscara. El ataque básico de espada quedó activado con multiplicador 1. Las demás acciones conservan su configuración.

La opción aplica el avance horizontal real del clip mediante PlayerMotor y CharacterController, respetando paredes y restricciones del mundo. Mientras se reproduce, reemplaza el movimiento de las teclas y conserva la gravedad. El cambio de golpe o la repetición del mismo clip reinicia el muestreo sin devolver al personaje a la posición inicial. Los movimientos especiales de habilidades/cinturón tienen prioridad. Los clips In Place no producen avance.

## Verificación

Se ejecutaron pruebas en Play Mode, en un proyecto aislado de Unity 6000.6.0f1, con el aventurero, su controlador, los tres clips DoubleL actualmente asignados y los componentes reales PlayerMotor, PlayerAnimationMotion, WeaponActionPlayback y AbilityAnimationBinding. Los servicios de audio/catálogo ajenos a la prueba se sustituyeron por servicios vacíos. Las asambleas completas de runtime y editor del proyecto compilaron con las referencias originales; solo aparecieron avisos de archivos existentes.

- Primer golpe: avance de 2,02 m; multiplicador 0,5: 1,01 m.
- Con la opción desactivada: ningún avance de la animación.
- Mantener la tecla de movimiento no suma un segundo desplazamiento.
- El controlador se detiene ante una pared y respeta restricciones de posición.
- Repetir el clip y encadenar los tres golpes no provoca teletransportes ni regresos al inicio.
- Cancelación, pausa y movimientos especiales detienen o sustituyen el avance de la animación.
- Al terminar, vuelve el movimiento normal y la malla permanece unida al collider.

Resultados completos: `checks.txt`. Compilación: `runtime-compile.txt` y `editor-compile.txt`.

El editor principal debe terminar de importar los cambios. Si no lo hace al enfocarlo, usar Assets > Refresh antes de probar Play.
