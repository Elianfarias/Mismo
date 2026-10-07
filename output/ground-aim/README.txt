APUNTADO DE SUELO — TRAMPA Y LLUVIA DE FLECHAS

Mantener la tecla asignada muestra el indicador; mover la cámara cambia
el destino; soltar confirma. El gesto respeta las acciones y bindings
existentes (también el mando), sin teclas nuevas para lanzar.
Cancelar con Esc, otra acción o cambiar de arma no consume recursos.
Para la trampa, Parry/clic derecho también cancela el apuntado.
Pausa, interfaces, pérdida de foco, daño directo y muerte lo limpian.

Trampa: parábola fina, huella y silueta ámbar. El vuelo usa la misma
función y puntos que la previsualización. Se arma 0,4 s después de
aterrizar; sus 30 s comienzan en la llegada. Mantiene daño y límite de dos.
Los obstáculos nuevos detienen el vuelo. Destinos sin apoyo, muy
inclinados o fuera del alcance son inválidos, en rojo con una cruz.

Lluvia: sólo círculo proyectado al terreno, sin parábola ni silueta.
El radio sale de GroundAreaAction.radius (2,5 m actualmente). Mantener
no gasta recursos; soltar paga la estamina una vez y comienza la
preparación normal, con las flechas cayendo sobre el punto confirmado.

Configuración:
- AbilityDefinition.groundIndicatorMaterial: material con referencia
  serializada en HunterTrap.asset y BowArea.asset; no usa Resources.
- TrapAction.arcHeight y throwSpeed: altura adicional y velocidad.
- Mallas de la silueta: compartidas desde el prefab real, sólo renderers.
- Material: Assets/Art/Materials/Combat/HunterTrap/HunterTrapAim.mat.

Herramientas: Mismo > Armas > Apuntado de suelo.
play-checks.txt: pruebas en Play Mode de ambos gestos, coste real,
cancelación, pausa, muerte, consultas de IA, vuelo, paredes, pendientes,
bordes y limpieza. Capturas reales de Unity en una escena temporal:
trap-aim.png, trap-blocked.png y rain-aim.png.
content-build.txt: compilación de scripts Windows sin UNITY_EDITOR y
recarga de las dos habilidades desde un bundle con sus dependencias.
No es una nueva build completa del juego.
organization.txt: incidencias previas de otros assets; no se reorganizan.
