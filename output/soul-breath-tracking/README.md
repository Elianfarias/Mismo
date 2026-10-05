# Seguimiento del aliento de Soul Eater

Se mantienen los nombres serializados y los valores del asset del usuario. En el Inspector, los dos controles aparecen juntos dentro de Aliento:

- **Giro del cuerpo (°/s)**: antes `Breath Turn Speed`. Controla solo la rotación corporal.
- **Giro del fuego F2 (°/s)**: antes `Phase Two Breath Tracking Speed`. Controla la dirección del fuego y del daño en fase 2. 0 fija la dirección inicial de cada aliento, 30 gira lentamente y 120 gira rápido.

Ambos admiten 0 real. Se quitó el mínimo de 1 grado/segundo que impedía detener por completo el seguimiento del fuego. La fase 1 mantiene el seguimiento durante la preparación y fija la dirección al primer tick de fuego.

Validación en `.validation/DragonRuntime`: 99 comprobaciones de fase 1, 48 de fase 2 y 51 del arco del dragón aprobadas. Se verificaron explícitamente fuego fijo con cuerpo girando, cuerpo quieto con fuego a 30 grados/segundo, fuego a 180 grados/segundo y alineación entre VFX y dirección del daño. No cambió la inclusión de contenido y no se repitió la build.

La primera ejecución detectó una referencia destruida al limpiar el polvo durante la descarga de escena. Se añadió una limpieza que tolera ese orden de destrucción y una prueba de regresión; la segunda ejecución terminó sin esa excepción.

Informes adjuntos: `phase-one.txt`, `phase-two.txt` y `world.txt`.
