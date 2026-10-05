# Soul Eater: primera fase

La prueba abarca del 100 % al 50 % de vida. La segunda fase se entrega mediante `PhaseTwoRequested`; todavía no tiene ataques definidos. Esta arena no registra al jefe en el mundo ni concede la recompensa de una victoria completa.

## Abrir y probar

- Escena: `Assets/Scenes/SoulEaterPhaseOneArena.unity`.
- Menú: **Mismo → Bosses → Soul Eater → Abrir arena de fase 1**.
- Iniciar Play. WASD mueve, click ataca, E usa parry y C esquiva. Se conserva el equipamiento inicial del proyecto.
- **F5** reinicia; **F6** baja la vida al 75 %; **F7** la baja al 50 % para probar los umbrales. El panel lateral muestra la anticipación, el ataque y las aperturas. Esc libera el cursor.
- La arena elimina el componente de reaparición del mundo de su copia del jugador para no iniciar el inventario persistente de la partida.

## Ataques y oportunidades

| Acción | Anticipación | Ataque | Recuperación | Respuesta del jugador |
|---|---:|---:|---:|---|
| Mordida | 0,95 s | 0,42 s | 1,25 s | Parry o esquiva lateral. Un impacto por intento. |
| Coletazo | 1,10 s | 0,65 s | 1,35 s | Salir del recorrido de la cola; se activa al permanecer detrás. |
| Aliento verde | 1,50 s | 2,20 s | 1,70 s | Salir del cono y aprovechar el flanco. No admite parry. |

La mordida y el aliento dejan de corregir la dirección durante el último 28 % de su anticipación. El aliento mantiene esa dirección durante toda la exhalación; no barre la arena ni deja fuego persistente. Su daño se aplica cada 0,4 s dentro del cono y comprueba obstáculos. La longitud visual crece junto con el alcance activo.

Valores iniciales ajustables: 1400 de vida, 220 de postura, mordida 22 de daño, coletazo 19, aliento 6 por pulso y carga 30. Los modificadores generales de combate del proyecto se aplican después. Son valores de partida para ajustar jugando.

## Secuencia del 75 %

Se ejecuta una vez por intento, después del ataque y la recuperación actuales. El jefe ruge, busca un aterrizaje seguro alejándose del jugador y salta hacia él. Comprueba el suelo, los límites de la arena, los obstáculos del arco y la visibilidad del jugador. Si no encuentra un destino válido, prepara la carga desde su ubicación actual.

Después de aterrizar, prepara la carga durante 1 s. Corrige su orientación al principio y la fija antes de salir. Corre en línea recta a 14 unidades/s hacia la posición fijada del jugador, con 4 unidades adicionales de recorrido y un máximo de 34; se detiene antes ante obstáculos o un borde sin suelo. No gira para perseguir al jugador durante la carrera. Cada objetivo recibe como máximo un impacto. Frena y ofrece 1,3 s de recuperación antes de volver a sus ataques habituales.

## Umbral del 50 %

Termina la acción y recuperación actuales, ejecuta un rugido de 2,6 s y entrega la segunda fase una sola vez. La arena muestra que la fase 1 está completada y protege al jefe hasta reiniciar. Si un único golpe cruza ambos umbrales, tiene prioridad el del 50 %.

Morir, perder al jugador o reiniciar cancelan los ataques. Una pérdida de objetivo durante el salto primero coloca al jefe sobre suelo seguro. La pausa detiene el reloj del combate y los sonidos del jefe.

## Assets y animaciones

- Prefab completo: `Assets/Art/Prefabs/DragonBosses/SoulEater_PhaseOne.prefab`.
- Configuración: `Assets/Data/Enemies/DragonBosses/SoulEater_PhaseOne.asset`.
- Conserva el visual verde con boca articulada y ojos integrados.
- Usa `Basic Attack`, `Tail Attack`, `Fireball Shoot`, `Scream`, `Take Off`, `Land`, `Run`, `Walk`, `Idle`, `Get Hit` y `Die` del rig Generic existente.
- El aliento prolonga la sección abierta de `Fireball Shoot`. El controlador sincroniza las secciones de cada clip con anticipación, daño y recuperación.
- `Charge_Anticipation` y `Charge_Brake` son adaptaciones independientes de `Defend` y `Land`. Los originales no se modifican.
- `Tail_RearSweep` adapta `Tail Attack`: reduce el giro y desplazamiento de la pelvis para mantener el barrido detrás del cuerpo.
- Llamarada nueva: `SoulEaterFlameVfx` y shader `Mismo/SoulEater/Flowing Flame`, con cintas de fuego, núcleo claro, bordes verdes, brasas y luz de boca. No utiliza el emisor de cubos de Firyx.
- Sonidos originales sintetizados en `Assets/Art/Audio/Enemies/SoulEater`: señales funcionales provisionales. Conviene reemplazarlos por audio final de criatura después de validar el ritmo de combate.

## Herramientas de mantenimiento

En el mismo menú se puede generar el contenido propio, ejecutar las comprobaciones en Play y compilar una arena Windows. La generación actualiza únicamente los assets de esta fase; no debe usarse para regenerar otros dragones. La escena se compila explícitamente sin modificar la lista de escenas del juego.

Resultados y capturas: `output/soul-eater-phase-one`. Las pruebas automáticas verifican colisiones, parry, bloqueo por paredes, umbrales, salto, carga, muerte, reinicio y pérdida de objetivo. La dificultad final y la sensación del control requieren una ronda de juego con el usuario.

## Validación realizada

- Unity 6000.6.0f1: comprobaciones en Play completadas (`checks.txt`).
- Capturas del aliento, salto/carga y coletazo: `vista-previa.html`. Se hornea cada pose para evitar la reutilización del mismo resultado de skinning al capturar varios cuadros en un frame del editor.
- Build Windows de desarrollo: `.validation/SoulEaterPhaseOneBuild/SoulEaterArena.exe`. La ejecución automática verifica aliento, salto, carga y final de fase; informe en `standalone.txt`.
- `ProjectOrganizationChecks.Run` ejecutado: el informe global conserva 117 incidencias en assets ajenos a Soul Eater, principalmente contenido importado fuera de sus carpetas. Ninguna entrada del informe corresponde a los nuevos assets de este jefe. Detalle en `organization.txt`.
- La build termina con `Succeeded`. Su contador de un error registra la excepción agregada del chequeo global de organización anterior; `build.txt` incluye ese diagnóstico y las advertencias heredadas. La prueba standalone finaliza con `PASS`.
