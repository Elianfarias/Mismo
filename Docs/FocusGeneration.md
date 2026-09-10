# Generación de Focus

Cada AbilityDefinition expone `Focus Gain On Hit` en el Inspector. Se otorga al atacante al infligir daño real, por enemigo e impacto aceptado. Los impactos duplicados, fallidos, esquivados, bloqueados e inmunes no generan esta recompensa. Se suma a los bonos existentes por espalda/apertura y se limita a 100.

Configuración inicial: `Assets/Data/Weapons/Bow/BowShot.asset` genera 5; su daño sigue en 8. Cuatro impactos normales permiten pagar los 20 Focus de BowPower. Las demás habilidades conservan una generación de 0 hasta configurarla. `Focus Cost` sigue siendo el gasto al activar una habilidad.

Se transporta la generación con el proyectil y con el área, por lo que impactar después de cambiar de arma sigue usando el valor del disparo original. En áreas, el valor se aplica por enemigo y pulso de daño: usar cantidades bajas. Los básicos de espada y las acciones de melee también admiten el campo.

Validación Unity: FocusGenerationChecks.RunBatch comprueba proyectiles reales, cuatro impactos para financiar BowPower, cantidad configurable, fallos, parry, duplicados y límite de 100. Resultado: Docs/Validation/FocusGenerationChecks.txt. Compilación del runtime sin errores.
