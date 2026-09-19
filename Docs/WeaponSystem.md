# Dos armas y habilidades reutilizables

La espada y el arco se cargan desde `Assets/Data/Weapons/StartingWeapons.asset` al iniciar el jugador. Funciona con las escenas existentes sin regenerar el terreno. Los datos del kit y sus visuales se encuentran en `Assets/Data/Weapons`.

## Controles

- Tab (mando: cruceta arriba): alternar espada y arco. No se puede alternar durante preparación, actividad, recuperación ni dash.
- Click: combo de espada o flecha básica.
- Q: estocada o disparo potente.
- E: parry o retirada con disparo.
- R: giro o zona de daño periódico en el suelo.
- C: dash del cinturón, independiente del arma.

El arco apunta al centro de la cámara. Las flechas chocan con el escenario y tienen alcance finito. Su visual usa `arrow_B`; el arco usa `bow_B_withString`. El kit del arco es una propuesta inicial de prueba con valores editables, no un diseño final de balance.

El arma activa está en la mano y la secundaria en la espalda. El intercambio es instantáneo. La pose del arco es procedural y conserva la locomoción existente; no hay todavía un clip específico de tensado de cuerda.

## Estructura

`EquipmentLoadout` conserva dos referencias y el arma activa. `TrySwap` alterna; `TryEquip` reemplaza una ranura únicamente fuera de combate. Daño y enemigos en persecución mantienen el combate; el período de gracia inicial es seis segundos configurables. No se añade un inventario ni una interfaz de selección de objetos.

`WeaponDefinition` referencia cuatro `AbilityDefinition`. Las definiciones contienen configuración compartida. El estado por uso está en `AbilityExecution`; `AbilityRunner` resuelve exclusión entre acciones, fases, gasto de stamina, cancelación y cooldowns. El cooldown usa tiempo de juego y se conserva al alternar o reequipar; empieza al aceptar la habilidad. Definiciones idénticas comparten cooldown dentro del mismo jugador.

Los comportamientos serializados se agregan desde el Inspector con **Agregar comportamiento**. Se pueden combinar desplazamiento, golpe, parry, proyectil y área. Crear un comportamiento nuevo implica derivar de `AbilityAction`; no modificar `PlayerController`.

La espada básica conserva `BasicSwordCombo` y sus ventanas de impacto como adaptación de compatibilidad. La estocada y el giro usan detección y movimiento comunes; el parry reutiliza el receptor defensivo existente. Los componentes históricos de estocada y giro quedan para las escenas y pruebas anteriores, pero el controlador ya no los ejecuta.

`ProjectileInstance` conserva autor, daño y trayectoria al dispararse. `AreaInstance` conserva su posición y duración aunque el jugador cambie de arma o muera. Las zonas limitan altura y comprueban obstáculos; no son estados pegados al jugador. El final del ataque no destruye efectos ya liberados. Morir antes de liberar una habilidad la cancela y limpia sus ventanas y movimiento.

## Alcance pendiente

La progresión por nivel, dominio por familia, materiales, mejoras y desbloqueos siguen pendientes de diseño. No se implementan fórmulas provisionales de progresión. Tampoco se añaden networking ni un catálogo de armas.

`WeaponSystemBuilder.Apply` crea los recursos iniciales en una copia sin ellos. No es necesario ejecutarlo para jugar una vez entregados los assets; volver a ejecutarlo restablece los valores iniciales del kit.

## Validación

`WeaponSystemChecks.RunBatch` ejecuta pruebas reales de Play Mode de equipamiento, recuperación, cooldowns, proyectiles contra blancos y paredes, áreas fijas y altura, cancelación y muerte. Los resultados se registran en `Docs/Validation/WeaponSystem-checks.txt`.

Validación realizada el 7 de septiembre de 2026 en una copia aislada con Unity 6000.3.11f1:

- 31 comprobaciones de Play Mode aprobadas; incluye daño real bloqueado por parry, cancelación del movimiento y cooldowns independientes entre jugadores.
- Regresión de VoxelRegion_7319 aprobada: navegación y referencias, recompensa, victoria del boss y reinicio del intento.
- Capturas revisadas con arco activo y espada activa, con la secundaria en la espalda.
- Referencias de los recursos nuevos verificadas sin GUIDs faltantes.

La prueba de región requiere gráficos: el minimapa existente llama a Camera.Render y Unity se cerró al probarlo sin dispositivo gráfico. La repetición con gráficos pasó. El editor también emitió una excepción de su índice de búsqueda durante el arranque; no provino del código de combate ni impidió las pruebas.

## Corrección del agarre y lluvia de flechas

La región conservaba un avatar anterior oculto con huesos del mismo nombre. El arco se anclaba a ese esqueleto en lugar del personaje visible. WeaponPresentation ahora busca las manos exclusivamente dentro del Animator activo (o el visual del motor como respaldo). Se comprueba en la región real mientras el personaje camina y gira; el prefab aislado no reproducía esta duplicación.

R se muestra como LLUVIA. GroundAreaAction permite configurar el visual de flecha, cantidad por descarga, altura y velocidad de caída. La configuración inicial emite nueve flechas desde cinco metros, a doce metros por segundo, cada 0,6 segundos. Las flechas descienden verticalmente y se eliminan al tocar una superficie o agotar su recorrido. El área conserva su posición al cambiar de arma.

FallingArrowVisual es solo presentación. El área aplica un pulso de daño tras el tiempo de caída; aumentar la cantidad visual no multiplica el daño. BowPresentationChecks.RunBatch verifica el agarre en el esqueleto visible, movimiento y giros, dirección de caída, limpieza y daño por descarga.

## Presentación y apuntado (referencia Cube World)

El arco usa una pose de dos brazos resuelta por geometría de articulaciones; el agarre y la orientación se ajustan al modelo bow_B_withString. Al preparar un disparo, la mano derecha tira hacia atrás y vuelve al soltar. La mira está al 60 % de la altura desde abajo, por encima del personaje. WeaponAim comparte ese punto entre HUD y cámara. El proyectil sale del arco y calcula su trayectoria al punto capturado al pulsar, incluso si la mano se movió durante la preparación.

La locomoción añade seis grados de inclinación al avanzar y doce al sprint, con transición suave, sin inclinar las colisiones del motor. El minimapa mantiene el norte arriba pero usa una cámara oblicua para mostrar relieve.

ActorCombatVisuals acompaña a Health en jugadores y enemigos. Los impactos muestran la vida realmente descontada (sin exagerar daño por overkill). Las muertes ocultan las mallas visibles y emiten hasta 240 partículas de cubo, sin colliders individuales, que desaparecen en dos segundos. El efecto aproxima colores y superficie; no convierte las mallas en vóxeles reales. Revivir restaura las mallas ocultadas. DamageNumbers limita a 64 números simultáneos y los elimina al segundo.

El daño/aturdimiento por caída y una nueva animación independiente de botas quedan pendientes; esta iteración modifica presentación, no las reglas de caída.

Comprobación de esta iteración: pruebas de presentación aprobadas en VoxelRegion_7319, incluyendo entradas simuladas de mouse izquierdo y E, lluvia, daño real, muerte y revivir. La regresión de armas volvió a pasar sus 31 comprobaciones. No se reprodujo que el click invoque E: el input observado fue Attack=True/Parry=False para click y Attack=False/Parry=True para E. Se revisaron las capturas del agarre frontal/lateral y la desintegración del jugador; el aspecto y sensación del apuntado necesitan también la evaluación manual del jugador.

## Corrección de orientación al moverse

La inclinación se aplica mediante PlayerLocomotionLean. Su Update temprano retira la inclinación anterior antes de que el controlador/motor calcule el giro; LateUpdate añade la nueva inclinación después de las animaciones. PlayerAnimationDriver ya no restaura una rotación antigua después del motor. El componente se instala también en prefabs existentes y restaura su transformación al desactivarse.

La regresión de presentación comprueba giros hacia los cuatro puntos cardinales con espada y arco durante actualizaciones normales, con inclinación habilitada.

## Evolución de combate v0.2

Las reglas de compromiso, carga, Posture, Focus y defensas perfectas sustituyen parte del comportamiento inicial descrito arriba. Consultar Docs/CombatV2.md para las reglas actuales y sus comprobaciones.
