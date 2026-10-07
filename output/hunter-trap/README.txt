TRAMPA DE CAZADOR — MODELO VOXEL E INTEGRACIÓN

Prefab listo para usar:
Assets/Art/Prefabs/Combat/HunterTrap/HunterTrap.prefab

La habilidad existente Assets/Data/Weapons/Bow/HunterTrap.asset ahora
referencia este prefab mediante TrapAction.prefab. Reemplaza el cilindro.
No requiere registros nuevos en el catálogo global ni paquetes externos.

Modelo original de acero, con dos mandíbulas semicirculares completas,
dientes escalonados, bisagras comunes, plato central y cadena corta.
Cuatro mallas rígidas comparten un material URP y una paleta de 64 x 1.
Paso voxel: 0,0125 m. Total: 16.594 triángulos; caras internas eliminadas.
Las pruebas de autoría verifican conectividad de cada mandíbula y todos
sus dientes, para evitar la separación presente en el concept.

Las mallas están en Assets/Art/Meshes/Combat/HunterTrap.
Material en Assets/Art/Materials/Combat/HunterTrap.
Paleta en Assets/Art/Textures/Combat/HunterTrap.
HunterTrapVisual controla los dos pivotes y el descenso del plato.
Closing Angle en el prefab permite ajustar el cierre (78 grados).

Comportamiento:
- Mantener la tecla de la habilidad muestra parábola, huella y silueta ámbar.
- Soltar confirma el destino; la trampa recorre exactamente la curva prevista.
- Un destino inválido se marca rojo y cancelar no consume recursos ni recarga.
- El vuelo se detiene ante obstáculos nuevos; no activa enemigos en el aire.
- Se orienta sobre la superficie de apoyo e ignora el collider del dueño.
- Se arma a los 0,4 s de aterrizar y permanece hasta 30 s desde la llegada.
- Máximo dos trampas en espera por dueño; una tercera retira la más antigua.
- Conserva 12 de daño base, multiplicadores de habilidad y 1,5 s inmovilizado.
- El cierre tarda 0,12 s; el modelo cerrado se conserva durante la retención.
- Una activación consume la trampa incluso si el objetivo esquiva, como antes.
- Conserva arma, habilidad y uso para la progresión; no repite daño por cada
  collider del enemigo. No añade sacudidas ni hit stop adicionales.
- Desaparición al terminar o perder al dueño; el reloj usa tiempo de juego.

Herramientas Unity: Mismo > Armas > Trampa de cazador.
HunterTrapModelBuilder crea sólo los assets ausentes y respeta los existentes.

Lluvia de flechas usa el mismo mantener/soltar con un círculo sin parábola;
el radio se toma de GroundAreaAction.radius (actualmente 2,5 m).
groundIndicatorMaterial, en cada AbilityDefinition, referencia el material
del indicador. TrapAction.arcHeight y throwSpeed ajustan arco y velocidad.
Comprobaciones de este apuntado: Mismo > Armas > Apuntado de suelo.
Capturas e informes actuales: output/ground-aim. Los informes debajo
corresponden a la integración original del modelo y su activación.

Capturas reales del modelo: trap-open.png y trap-closed.png.
Captura de prueba con goblin en Play Mode: trap-goblin-play.png.
checks.txt contiene referencias, geometría y presupuesto del prefab.
play-checks.txt contiene la prueba real de TrapAction contra el goblin,
daño único, atribución, inmovilización, cierre, pausa, retirada, dos dueños,
expiración, esquiva y orientación sobre pendiente.
Los reportes runtime-compilation.txt, content-build.txt y game-build.txt
registran los resultados de compilación y contenido Windows.
organization.txt conserva las incidencias previas de otros assets del proyecto.

Resultado de esta ejecución: pruebas de modelo y Play Mode aprobadas;
compilación runtime y recarga del contenido aprobadas. Build completa Windows
Succeeded, con 1 error registrado por la organización previa de assets de
otros paquetes (detalle en game-build.txt). No es una build sin incidencias.
Ejecutable de validación: .validation/HunterTrapGame/Mismo.exe.
