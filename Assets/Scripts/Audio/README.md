# Configurar sonidos

## Pausa durante la partida

Escape abre el menú de pausa con los controles de Música, Efectos e Interfaz
compartidos con el menú principal. Escape o Continuar reanuda la partida.
Los cambios se aplican inmediatamente y se guardan en las mismas preferencias.
La música sigue sonando para poder ajustar su volumen.

Mientras está abierto se detiene el tiempo del juego y se bloquean movimiento,
cámara e interacciones. Si había inventario, mapa o crafteo abierto, queda oculto
durante la pausa y reaparece al continuar. La cámara lenta de combate también
se suspende y continúa desde donde estaba al reanudar.

Comprobar en Play: abrir con Escape mientras se camina o pelea; mover los tres
volúmenes; continuar con el botón y con Escape; abrir sobre inventario y mapa;
pausar durante una defensa perfecta y verificar que la pausa no termina sola.

## Herramienta del editor

Abrir **Mismo > Audio > Configurar sonidos** en Unity.
Arrastrar un AudioClip a cada evento, ajustar volumen e intervalo mínimo y pulsar
**Guardar configuración**. Las asignaciones se guardan en
`Assets/Data/Audio/GameSounds.asset` y se incluyen en las builds.
El catálogo inicial está sin sonidos: un evento sin clip permanece en silencio.

## Música por zona

En la sección Música de la misma ventana se asignan menú principal, exploración,
combate, pradera, bosque, tierras altas y cercanía al pueblo. La prioridad es
**pelea con jefe > pueblo > bioma > exploración**. Los biomas/pueblos sin pista usan
exploración; menú o combate sin pista permanecen en silencio.

La selección inicial usa Ambient 1/2/3/4 del pack de AlkaKrab para pradera,
bosque, tierras altas y pueblo respectivamente. Son asignaciones iniciales editables.
La distancia de pueblo se mide desde su borde y empieza en 25 metros.
El cambio de zona se confirma tras dos segundos, ajustables en la ventana;
la pelea con un jefe tiene prioridad inmediata. Los enemigos comunes mantienen la música de zona. Al derrotar al jefe o perder su atención vuelve la música de la zona, sin esperar el temporizador de combate general.
Las transiciones se funden en 1,5 segundos y respetan el volumen Música.
Se consulta el terreno real de la partida, incluida su semilla y pueblos procedurales.
Las escenas de prueba sin mundo usan la pista general de exploración.
Las pistas de zona pueden cambiarse durante Play; para el menú principal, volver
a entrar a esa escena después de modificar su pista.

**Probar clip** escucha el archivo original en modo edición. **Disparar evento**,
disponible en Play, usa el volumen configurado, el intervalo y el mezclador UI.
El ajuste Interfaz del menú controla todos estos eventos. No modifica música ni
efectos del mundo. El intervalo usa tiempo real y funciona durante una pausa.

## Eventos conectados

- Abrir/cerrar: inventario, personaje, habilidades, mapa, estación de crafteo y opciones.
- Hover/clic: botones compartidos FantasyUI/QuietFantasyUI, inventario, crafteo,
  bestiario y controles del menú principal. Los controles deshabilitados no suenan.
- Cambiar pestaña: páginas del inventario.
- Crafteo exitoso/fallido, usar consumible y recolectar recursos.
- Experiencia y nivel: recompensas de victoria; subir de nivel reemplaza el aviso
  de EXP para evitar superposición. Un salto de varios niveles produce un aviso.
- Drop de arma: victoria o recompensa única, incluso si queda pendiente en el suelo.
- Recoger botín: recoger el botín pendiente; no vuelve a disparar el drop.
- Equipar/cambiar arma, descubrir región/especie y error al guardar un cambio.

Las recompensas reproducen éxito solamente después de confirmar el guardado.
Una recompensa ya reclamada no vuelve a sonar.

## Sonidos del goblin

Los siete efectos están en `Assets/Art/Audio/Enemies/Goblin`:
preparación y ejecución de Golpe/Carga, y tres gruñidos cortos de hit. Son WAV
mono PCM de 16 bits a 48 kHz. La voz parte de grabaciones CC0 de artisticdude
(Goblins Sound Pack), editadas con un pequeño descenso de tono y capas suaves
de movimiento. Los originales se conservan en la subcarpeta `Source`;
procedencia, licencia y correspondencias en `Assets/Documentation/Audio/GoblinVoices.txt`.
El gruñido de hit acompaña al impacto del arma del perfil de feedback existente.
Golpe y Carga usan el esfuerzo B aprobado, la voz `goblin-2` aislada de 0,262 s,
sin capas de movimiento. Ambos WAV conservan exactamente los bytes de la
muestra `output/goblin-audio/effort-options/Goblin_Esfuerzo_B.wav`.
Las preparaciones, voces de daño y volúmenes de cada ataque se conservan.

`Assets/Data/Enemies/BaseGoblin.asset` referencia los clips directamente; el
goblin normal y el élite comparten esta configuración. En **Mismo > Enemigos >
Taller de armas y animaciones**, seleccionar el goblin, abrir cada ataque y
ajustar `Preparation Sfx`, `Execution Sfx` y sus volúmenes. También se pueden
editar directamente en el Inspector de BaseGoblin, dentro de `Slash` (Golpe)
y `Charge` (Carga).
En el Inspector de BaseGoblin, `Hit Sfx` y `Hit Sfx Volume` controlan la reacción
al daño. Todos usan el canal Efectos existente.

Las tomas de hit alternan sin modificar la aleatoriedad de la IA, con un mínimo
de 80 ms entre voces. Sólo suenan al recibir daño directo, incluido el golpe
letal; no por bloqueo, parry, esquiva ni ticks de estados. Los enemigos sin
clips de hit asignados mantienen su comportamiento anterior.

Para regenerar las siete tomas y la muestra de escucha, ejecutar
`python Assets/Scripts/Audio/Editor/GenerateGoblinSounds.py` (requiere NumPy).
Conserva los GUID existentes. También genera `output/goblin-audio/escuchar.html`.
La muestra queda en
`output/goblin-audio/Goblin_Audio_Preview.wav`: cuatro sonidos de habilidades,
tres hits y dos secuencias con la preparación de 0,5 s del juego.

**Mismo > Audio > Verificar sonidos del goblin** comprueba la importación,
los eventos de daño, las regresiones existentes de combate y la organización,
y construye/recarga un bundle Windows desde BaseGoblin para verificar sus
dependencias de audio. El informe queda en `output/goblin-audio/checks.txt`.
Es una build de contenido; no reemplaza una build completa del jugador.

## Imp: voz B y bola de fuego

`Assets/Data/Enemies/ForestCreatures/Imp.asset` usa las tres voces B aprobadas
en `Assets/Art/Audio/Enemies/Imp/Concept`: preparación y ejecución del golpe
cuerpo a cuerpo, y reacción al recibir daño directo. Los archivos aprobados
conservan sus bytes y GUID; las voces alternativas no están asignadas.

La bola de fuego tiene tres clips en `Assets/Art/Audio/Enemies/Imp/Fire`:
preparación de 1,18 s (dentro del windup existente de 1,2 s), lanzamiento de
0,64 s e impacto de llamas de 1,25 s, con inicio suave y crepitar.
Preparación y lanzamiento mezclan la voz B con fuego procedural.
El impacto se reproduce al colisionar el proyectil,
en el punto de contacto y a través del grupo SFX; no al agotar su alcance.

Las referencias pertenecen a cada ataque y no añaden otro reproductor ni
otro hit stop. Se mantienen tiempos, daño, cooldowns, VFX y charco de fuego.

Generar con `python Assets/Scripts/Audio/Editor/GenerateImpFireSounds.py`
(NumPy). Si se regeneran primero las voces con `GenerateImpVoiceConcepts.py`,
ejecutar después el generador de fuego para actualizar la mezcla y la página
`output/imp-audio/escuchar.html`.

**Mismo > Audio > Integrar voz B y fuego del Imp** asigna únicamente audio.
`ImpAudioIntegration.RunBatch` verifica los clips importados, construye y
recarga un bundle Windows del prefab real y prueba las fases de ataque,
hit, cancelación y sonido espacial en Play Mode, en una escena temporal.
Los informes quedan en `output/imp-audio`; la build es de contenido, no del
jugador completo. Créditos y licencia CC BY 3.0 de las voces en
`Assets/Documentation/Audio/ImpVoiceConcepts.txt`.

## Araña: perfil B (Hissing)

`Assets/Data/Enemies/ForestCreatures/Spider.asset` referencia los cinco clips
B aprobados de `Art/Audio/Enemies/Spider/Concept`: preparación y ejecución de
mordida, preparación y ejecución de salto, y hit recibido. Las cuatro variantes
comparten esa configuración. Las preparaciones de 0,34 y 0,50 s entran dentro
de los windups existentes; se conservan daño, animaciones y telaraña desactivada.

`SpiderAudioIntegration.RunBatch` valida asignaciones, dependencias de los cuatro
prefabs, build de contenido Windows y eventos en Play Mode. Los informes quedan
en `output/spider-audio`. Los WAV aprobados conservan bytes y GUID. Créditos de
spookymodem y WakianTech (CC BY 3.0) en `Assets/Documentation/Audio/SpiderSoundConcepts.txt`.

## Jabalí: perfil B (Bristled)

`Assets/Data/Enemies/ForestCreatures/Boar.asset` referencia los cinco clips B
aprobados, incluido el hit revisado de cerdo real de 0,29 s. Las tres variantes
comparten preparación y ejecución del ataque, preparación y arranque de la
carga, y reacción al daño directo. Los volúmenes son 0,8 para habilidades y
0,75 para hit. Las preparaciones de 0,40 y 0,62 s respetan los tiempos actuales.

`BoarAudioIntegration.RunBatch` valida las asignaciones existentes, las
dependencias de los tres prefabs, una build de contenido Windows y los eventos
de combate en Play Mode. Informes en `output/boar-audio`; créditos CC0 y CC BY
3.0 en `Assets/Documentation/Audio/BoarSoundConcepts.txt`. Los WAV aprobados
conservan sus bytes y GUID. El generador actualiza también la página de escucha.

## Dragón Soul Eater: Free Monster Sounds + fuego de Daniel Simon

El prefab `Art/Prefabs/DragonBosses/SoulEater_PhaseOne.prefab` referencia
`Data/Audio/SoulEaterAudio.asset`. Cada entrada del perfil permite cambiar el
clip y volumen por evento: aparición, rugido, mordida, coletazo, carga, salto,
aleteo, paso, impacto, daño y muerte. El pack de Creature Sounds / Coucassi
aporta las voces del dragón y su movimiento; el paso del Behemoth se usa para
pisadas e impactos. Los conceptos anteriores no son dependencias del prefab.

El audio aprobado de Daniel Simon se divide a 1,5 s entre preparación y
salida del fuego. Si un aliento supera la grabación inicial, continúa con un
loop tomado de su tramo sostenido. Al interrumpirlo termina con un fade corto;
la muerte y el reinicio detienen los canales inmediatamente. Las voces, fuego,
movimiento y daño tienen canales espaciales separados en el grupo SFX.
Los pasos y aleteos siguen las fases de las animaciones; no suenan pasos si
el cuerpo está quieto ni aleteos durante el planeo del picado. Los ataques
melee suenan al comenzar su fase activa. El daño periódico no dispara voces
de dolor, y los impactos simultáneos comparten un límite de repetición.

`PrepareDragonPackAudio.py` conserva los originales y prepara nueve WAV sin
cambiar su tono: recorte de silencio, ganancia y fades; también empalma el
loop de fuego. Produce `output/dragon-audio/integrados.html` para escucharlos.
Las licencias y modificaciones están en
`Assets/Documentation/Audio/DragonSoundSelection.txt` y su manifiesto JSON.

**Mismo > Audio > Integrar pack del dragón** asigna el perfil al prefab sin
regenerar escenas, geometría ni reglas de combate. Conserva los ajustes de un
perfil ya creado. `DragonAudioIntegration.RunBatch` importa y valida en una
copia aislada: clips, dependencias, bundle Windows y reproducción en Play Mode
(preparación, ejecución, fuego terrestre/aéreo, pausa, interrupción, vuelo,
pasos, impacto, daño, muerte y limpieza). Informes en `output/dragon-audio`.
La build de contenido no equivale a una build completa del jugador.

## Ampliar eventos de interfaz

Para botones nuevos de Unity UI, agregar **UISoundFeedback** al objeto con el
Selectable/Button. Para interfaces IMGUI, usar `GameAudio.Button(rect, text, style)`.
Para emitir un evento desde código: `GameAudio.Play(GameSound.CraftSuccess)`.
Para agregar otro tipo, añadirlo al final de `GameSound` y abrir la herramienta:
el catálogo agrega las entradas nuevas sin reemplazar las asignaciones existentes.

## Prueba en Unity

1. Asignar clips distintos a hover, clic, abrir/cerrar, EXP, nivel y drop.
2. Probar clips fuera de Play y disparar eventos en Play; mover Interfaz a cero.
3. Mantener el cursor quieto, salir y volver a entrar: hover solo al entrar.
4. Abrir/cerrar con teclado; cambiar páginas y usar un botón deshabilitado.
5. Completar un crafteo y recibir una victoria con y sin subida de nivel.
6. Recoger botín, cambiar arma y volver al menú; comprobar ausencia de duplicados.

