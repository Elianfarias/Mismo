# Buffs sobre el personaje

Presentación compartida para cualquier actor, con una órbita de hasta cuatro símbolos pequeños, aparición/desaparición de 0,2 s y una sola traza tenue a la altura de los pies. Un tipo ocupa los cuatro espacios; dos tipos se distribuyen 2 + 2; tres tipos usan tres símbolos (uno cada uno); cuatro tipos usan uno cada uno. Las fuentes duplicadas del mismo tipo no suman símbolos. Cuando conviven varios tipos, los símbolos bajan al 75 % de su opacidad habitual. No modifica las mallas ni los materiales del personaje, no agrega luces, colisiones, hit stop ni sacudidas.

| Tipo | Color | Símbolo | Conexiones actuales |
| --- | --- | --- | --- |
| Escudo | Celeste claro | Paneles translúcidos | Barrera al recuperar el broquel |
| Daño | Rojo | Espadas cruzadas | Tercer impacto, Paso lateral, segundo golpe de Dos tiempos, afilado |
| Defensa | Azul | Escudos | Cobertura frontal durante el combo |
| Velocidad | Violeta | Chevrones | Acumulaciones de Ritmo, velocidad de ataque |

La HUD muestra tarjetas de los buffs activos arriba, a la derecha de la vida y la resistencia, sin la franja de color en el borde izquierdo. Conserva color e icono, tiempo restante y barra de duración cuando corresponde. Las habilidades con progreso muestran marcas dentro de su botón. Tercer impacto queda listo tras dos aciertos y potencia el tercero; tanto el progreso parcial como el golpe listo se vacían tras cinco segundos sin acertar. Fallar no renueva ese plazo y la pausa lo detiene. El plazo se configura con `thirdArrowResetSeconds` en `Assets/Data/Weapons/Skills/ThirdArrow.asset`. Dos tiempos no anuncia daño extra durante su primer golpe de lentitud. Rematador muestra «MISMO ENEMIGO» en la HUD y no enciende una mejora global sobre el cuerpo. Cobertura indica «SOLO FRONTAL». La barrera desaparece visualmente por agotamiento o expiración, sin esperar otro daño.

Configuración: `Assets/Data/Combat/Buffs/BuffPresentation.asset`. Allí se ajustan colores, tamaño de símbolos, opacidad y velocidad de órbita. La distribución de hasta cuatro símbolos es común a todos los actores. El material usa `Assets/Art/Shaders/BuffSymbol.shader`. Mallas: `Assets/Art/Meshes/Combat/Buffs`. Iconos: `Assets/Art/UI/Combat/Buffs`. El catálogo precargado referencia el perfil con la clave `Combat/Buffs`.

Los ticks de estados como veneno siguen causando daño y muerte, pero no disparan animaciones de impacto, destellos de golpe ni interrupciones en los enemigos. Los impactos directos conservan esas reacciones.

## Futuro bardo / efectos de party

`ActorBuffFeedback` es una capa **visual**: no aplica estadísticas ni cambia las reglas de acumulación de daño, defensa o velocidad. El sistema de gameplay que implemente el bardo debe administrar esas reglas y publicar la presentación en cada destinatario:

```csharp
var feedback = ActorBuffFeedback.For(aliado);
feedback.SetBuff(bardo, "cancion.ataque", BuffKind.Damage,
    remaining: segundosRestantes, duration: duracionTotal, label: "CANTO DE GUERRA");
// Al disiparse/cancelarse el buff:
feedback.RemoveBuff(bardo, "cancion.ataque");
```

Actualizar el mismo par fuente/id refresca la entrada, sin duplicar VFX. Distintas fuentes conservan sus tiempos separados y comparten el efecto de su tipo. `remaining: -1` conserva una carga visual hasta `RemoveBuff`; no concede un golpe potenciado. Los efectos caducan con tiempo escalado, se limpian al morir/desactivar al destinatario y desaparecen si se destruye su fuente. El cambio de arma limpia sus mejoras propias y conserva las externas. No se guarda un buff externo en partidas: esa persistencia corresponderá al futuro sistema de gameplay.

## Verificación

Revisión del 6/10/2026 (HUD junto a la vida, caducidad del tercer impacto, órbita compartida y ticks sin hit): compilación C# y pruebas de Play Mode completadas con código de salida 0. `play-checks.txt` y los PNG están actualizados: caducidad de cinco segundos, distribución 4/2+2/3/4 y ticks reales sin reiniciar hit sobre Imp, goblin y primer jefe. La captura de Game View confirma los buffs junto a la vida, sin franja izquierda. `checks.txt` y los informes de build de esta carpeta conservan la verificación anterior; los scripts Windows actuales también se compilan al verificar el apuntado en `output/ground-aim`.

Herramientas de Unity: **Mismo → Combate → Buffs**. El instalador conserva los ajustes de assets que ya existen. Las pruebas de Play Mode usan una escena temporal y un equipo aislado, sin cargar ni escribir perfiles del jugador. Restauran las escenas anteriores.

Los PNG de esta carpeta se capturan del modelo y los efectos reales en Unity, en una escena de prueba. No son nuevos concept arts ni capturas de una partida completa.

- `play-checks.txt`: comprobaciones de daño real del tercer impacto, caducidad sin aciertos, fallos/esquivas, distribución 4/2+2/3/4, Rematador por objetivo, barrera, defensa frontal, velocidad, pausa, fuentes simultáneas, cambio de arma, muerte y limpieza. También prueba ticks reales de veneno sobre Imp, goblin y primer jefe.
- `checks.txt`: comprobaciones de assets, perfil precargado y posición de la HUD junto a la vida dentro de los límites de la pantalla.
- `hud-game-view.png`: captura de la Game View real con PlayerHUD, Tercer impacto listo y cuatro entradas externas de ejemplo. Los buffs externos se fuerzan exclusivamente en la escena temporal de prueba; todavía no existe la habilidad del bardo.
- `Damage-rear.png`, `Shield-rear.png`, `Defense-rear.png`, `Speed-rear.png` y `combined-rear.png`: efectos individuales y combinados sobre el modelo del mago, normalizado a la altura del actor de prueba.
- `content-build.txt`: compilación de scripts Windows y carga del bundle con todas las referencias de presentación.
- `game-build.txt`: build Windows generada en `.validation/BuffGame/Mismo.exe`, resultado `Succeeded`, con un error de auditoría por ubicaciones de assets anteriores (ver `organization.txt`). No es una auditoría global limpia; no se reorganizó arte ajeno.

La build generó `Assets/Data/Addressables/link.xml` y su meta, ausentes al comenzar. Se conservaron en `.validation/BuffGeneratedArtifacts` y se retiraron de Assets al finalizar. La escena temporal también se retiró.
