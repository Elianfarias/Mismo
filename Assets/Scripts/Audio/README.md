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

## Ampliar

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

