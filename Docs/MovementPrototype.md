# Prototipo de movimiento

Abrir `Assets/Scenes/MovementPrototype.unity` y entrar en Play Mode.

WASD mueve al personaje relativo a cámara; mouse controla la órbita; Shift mantiene sprint; Space salta y permite un segundo salto en el aire; C ejecuta el dash; Q ejecuta el Lunge; E o botón derecho abre la ventana de parry de la espada; R ejecuta el Spin Attack. Soltar Space reduce la altura de cada salto. Escape libera el cursor y click vuelve a capturarlo. Backspace recupera la posición inicial; también se recuperan automáticamente las caídas por debajo del playground.

## Ajustes

Los assets en `Assets/Data/Player` controlan movimiento, stamina y dash. El cinturón `BasicBeltDash` referencia `BasicDashSettings`. Distancia y duración determinan su velocidad; el cooldown comienza al activar el dash. El cinturón inicial requiere suelo, conserva dirección durante el dash y no consume stamina. Las paredes interrumpen su desplazamiento. Salto tiene buffer y tolerancia al abandonar bordes; aterrizar durante un dash permite conservar brevemente una pulsación de salto.

La cámara tiene distancia, sensibilidad, límites verticales y suavizado en su componente. El jugador usa la capa integrada Ignore Raycast para excluirse de las consultas de cámara; su CharacterController conserva colisiones físicas. La representación visual es hija del motor. Las futuras habilidades deben entregar desplazamientos al motor mediante `PlayerMotor.RequestControlledDisplacement` en vez de modificar el Transform por su cuenta. El motor consume una solicitud por frame, resuelve prioridades y mantiene bajo su autoridad la rotación y las colisiones.

`CombatFeedback` se puede añadir a cualquier actor con `Health` para mostrar impactos automáticamente. Las resoluciones de bloqueo y parry invocan `NotifyBlocked` y `NotifyParried`; los cooldowns del `BeltDash` se notifican automáticamente y otras habilidades pueden llamar `NotifyCooldown`.

`BasicSwordCombo` coordina hasta tres etapas configurables y debe compartir índices con las ventanas de `AttackHitbox`. La entrada `Attack` (click izquierdo) se traduce desde `PlayerInputReader`; cada pulsación dentro de la ventana de entrada se encola y se ejecuta al terminar la etapa actual, respetando transición y recuperación. El prefab inicial trae `slash`, `slash` y `heavy_slash` con un hitbox hijo separado, para que la cadena no interfiera con Lunge o Spin y pueda sustituirse junto con cada arma.

`SwordAnimationFeedback` aplica poses procedurales temporales al visual de la espada para slash, heavy slash, parry, spin y lunge. Los hitboxes de combo y Spin consultan overlaps físicos mientras están activos, por lo que funcionan con dummies estáticos aunque no exista un Rigidbody. Cuando se reemplace el placeholder por un Animator o clips finales, el componente puede retirarse sin cambiar contratos de gameplay.

`SwordParry` y `SwordSpinAttack` son habilidades exclusivas de la espada y opcionales en el personaje. Cada arma futura puede reemplazarlas por sus propios componentes y reglas, sin convertir sus habilidades en contratos globales del jugador. E abre el parry; R inicia el giro circular. El parry se configura en su componente; `DamageReceiver` lo consulta antes de aplicar daño, y un impacto válido durante la ventana se anula y emite `Parried`, que `CombatFeedback` representa con señal dorada y tono agudo. El giro usa un área temporal, aplica daño una vez por objetivo, consume stamina y dispara feedback celeste al comenzar.

La herramienta de editor `SwordAssetTool` reutiliza `Assets/Data/Player/BasicSword.asset` y puede adjuntar el prefab visual temporal `Assets/Art/Prefabs/Player/BasicSword.prefab` al personaje seleccionado. `Mismo > Prototype > Build Sword Combat Training Setup` agrega `SwordAnimationFeedback` al prefab del jugador, crea `Assets/Art/Prefabs/Combat/TrainingDummy.prefab` y coloca una instancia frente al jugador en `MovementPrototype` sin duplicarla si ya existe.

El prefab `Assets/Art/Prefabs/Player/Player.prefab` contiene input, motor, stamina, cinturón y coordinación, además de las habilidades actuales de espada (`BasicSwordCombo`, `SwordLunge`, `SwordParry` y `SwordSpinAttack`). La referencia de cámara se conecta en cada escena. No hay estado de jugador mutable en ScriptableObjects ni singletons.

## Herramientas

`Mismo > Prototype > Build Complete Movement Prototype` genera la escena y assets faltantes. No sobrescribe una escena ni un prefab existentes. La generación utiliza una escena aditiva temporal que se guarda y cierra; conserva la escena de trabajo abierta. Los assets generados se eliminan desde Project si hace falta; esta operación no ofrece Undo de archivos.

`Mismo > Prototype > Validate Movement Prototype` ejecuta comprobaciones físicas sin guardar sus cambios. Ejecutar con la escena del prototipo cerrada y fuera de Play Mode. Para evitar colisiones con otra escena abierta, usar la comprobación batch en una instancia aislada de Unity.

`MovementDebugView` es presentación provisional: muestra stamina, velocidad y cooldown, y permite recuperar la posición de prueba. `MovementFeedback` añade partículas, sonidos sintetizados y squash/tilt provisionales para sprint, salto, aterrizaje y dash; se crea automáticamente en el jugador y no controla la física. Se puede eliminar el objeto Prototype HUD sin afectar los sistemas del jugador. Los scripts dentro de Editor son builders y comprobaciones que no entran en una build.

## Validación manual

- Recorrer rampas, escalones y slalom; comprobar que la diagonal no sea más rápida.
- Correr, invertir dirección, saltar y aterrizar manteniendo control. Probar salto corto y largo.
- Saltar apenas después de abandonar un borde y apenas antes de aterrizar.
- Agotar stamina: sprint se interrumpe y se recupera sin parpadeo rápido entre velocidades.
- Hacer dash con stamina agotada, en reposo, hacia una pared y durante cooldown.
- Pulsar click izquierdo una vez y luego dentro de cada ventana de entrada; verificar `slash` → `slash` → `heavy_slash`, recuperación al terminar y que el hitbox se active solo durante su ventana.
- Rotar cámara junto a paredes y bajo techo; verificar que el jugador no bloquee su propia cámara.
- Comparar la secuencia completa a 30, 60 y 144 FPS antes de considerar definitivo el ajuste.

El ajuste inicial es provisional. Las pruebas físicas automatizadas no determinan por sí solas si el movimiento es divertido ni reemplazan la revisión visual y jugable.

## Resultado de esta entrega

Validado con Unity 6000.3.11f1 en una copia temporal del proyecto:

- Compilación de los assemblies de runtime y editor completada.
- Escena y prefab generados mediante Unity; comprobación sin scripts perdidos.
- Suelo, salto, aterrizaje y dash comprobados a 30, 60 y 144 FPS. Distancia integrada del dash básico: 5.000 m en las tres tasas.
- Comprobaciones de cooldown, regeneración y colisión de un desplazamiento contra pared superadas.
- Prueba en Play Mode con teclado y mouse simulados superada: movimiento, sprint con consumo, cambio de dirección, doble salto con una altura combinada observada de aproximadamente 2.39 m, aterrizaje y dash. Velocidad máxima observada durante dash: 22.73 m/s.

La prueba en Play Mode usa `MovementPlayModeCheck.Run` mediante `-executeMethod`, únicamente en una instancia batch aislada. Ajusta el enrutamiento del input durante esa ejecución y termina la instancia al completar la prueba. No ejecutarla contra un editor de trabajo.

No se generó un ejecutable distribuible ni se verificó visualmente la cámara en una sesión gráfica. Quedan para la prueba jugable la sensación de control y el ajuste fino de cámara, rotación y tiempos. El editor batch emitió además un error de su indexador Search, ajeno a los scripts de gameplay; no impidió la comprobación jugable.
