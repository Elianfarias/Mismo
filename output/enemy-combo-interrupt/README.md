# Interrupción de enemigos por combos

Los impactos confirmados provenientes de AttackHitbox cancelan preparación, ataque activo o recuperación de enemigos gobernados por GoblinController, salvo excepciones configuradas.
Cada golpe renueva un hitstun de 0.55 segundos. La resistencia temporal del antiguo stagger no impide continuar el combo. No reduce un aturdimiento o recuperación más largos ni sustituye una rotura de postura activa.
Stagger descarta CurrentAttack y bloquea la emisión pendiente del ataque: no se reanuda después del aturdimiento. Los proyectiles ya lanzados siguen existiendo.

Configuración: GoblinSettings > Interrupción por combos > interruptibleByCombos y comboHitStun. BaseGoblin queda activado explícitamente.
GoblinAttack.resistComboInterrupt permite proteger la preparación y ejecución de un ataque concreto; su recuperación sigue expuesta. El parry y la rotura de postura conservan sus interrupciones. Los jefes quedan excluidos de esta nueva regla.
Solo un impacto con daño de salud confirmado interrumpe: bloqueo, invulnerabilidad, fallos y proyectiles no lo hacen. Tampoco da invulnerabilidad al jugador frente a otros enemigos ni deshace daño que ya recibió antes de conectar el golpe.

Validación: compilación completa Mismo.Gameplay.Enemies con referencias del proyecto. Pruebas aisladas Unity del controlador real con servicios periféricos sustituidos; no es una partida de la escena principal. Ver checks.txt y compile.txt.
