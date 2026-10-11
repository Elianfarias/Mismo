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

Las hachas guardadas van en diagonal:
- **Una sola** (`AxePose.holstered`): el mango sobre el hombro derecho y la cabeza a la izquierda de la columna, en la espalda baja.
- **En pareja** se cruzan en X. Para eso, `WeaponPoseProfile` tiene `useDualHolstered`/`dualHolstered`, la pose del arma guardada cuando va en pareja; `WeaponPresentation` la elige con `Holstered(dualWield)`.
  - La primera hacha usa `dualHolstered`; la segunda, el `secondaryHolstered` del `Axe.asset`.
  - Cada mango pasa sobre un hombro y cruza a media espalda; las cabezas cuelgan abiertas a cada lado, y la segunda hacha va 3,5 cm más atrás para que no se atraviesen.
  - Las espadas no usan esta pose.

Las tres poses las calcula `AxeHolsterSetup` (menú *Mismo/Armas/Hachas cruzadas en la espalda*):
- **Qué mide:** hombros, cadera y espalda de `MageSkin` en idle, a la escala del jugador, y el hacha (el mango, ~1 m hasta el centro de la cabeza, es más largo que el torso, así que el extremo sobresale sobre el hombro).
- **Cómo calcula:** cada pose sale de dónde cuelga la cabeza y sobre qué hombro pasa el mango, con la hoja plana contra la espalda y el filo hacia afuera.
- **Capturas:** *Capturas de hachas en la espalda* deja en `Logs/AxeHolster` las tres skins desde atrás, en ¾ y de costado.
- **Prueba:** `AxeHolsterSetup.CheckBatch` verifica la diagonal, la X y que ninguna quede delante de la espalda.
- **Retoques a mano:** el Taller de poses (*Editar arma guardada*) edita `dualHolstered` cuando la vista previa va en pareja.
- **Límite:** la espalda del Guerrero (capa) y de la Rana queda más atrás que la del Mago, que es la referencia, así que las hachas pueden rozar la capa.

## Estructura

`EquipmentLoadout` conserva dos referencias y el arma activa. `TrySwap` alterna; `TryEquip` reemplaza una ranura únicamente fuera de combate. Daño y enemigos en persecución mantienen el combate; el período de gracia inicial es seis segundos configurables. No se añade un inventario ni una interfaz de selección de objetos.

`WeaponDefinition` referencia cuatro `AbilityDefinition`. Las definiciones contienen configuración compartida. El estado por uso está en `AbilityExecution`; `AbilityRunner` resuelve exclusión entre acciones, fases, gasto de stamina, cancelación y cooldowns. El cooldown usa tiempo de juego y se conserva al alternar o reequipar; empieza al aceptar la habilidad. Definiciones idénticas comparten cooldown dentro del mismo jugador.

Una habilidad con **Etapas por pulsación** se reactiva como la Q de Riven. Cada pulsación ejecuta la etapa siguiente con sus propios tiempos y una `AbilityExecution` propia, así que entre pulsaciones el jugador queda libre. Focus y estamina se cobran sólo en la primera.

La ventana para volver a pulsar corre desde que termina cada etapa, en tiempo de juego, y se congela en pausa. El cooldown empieza con la última pulsación o al vencer la ventana; cambiar de arma o equipo, morir o desactivar el jugador también cierra la cadena. Pulsar durante la etapa anterior deja la pulsación en cola hasta que termine.

Todas las etapas comparten el `UseId` de la primera, así que la maestría cuenta un uso por cadena. `RecastStrikeAction` da el golpe de cada etapa y puede romper la postura con `CombatState.BreakPosture`. `RecastAbilityChecks.RunBatch` verifica estas reglas en Play Mode.

### Evoluciones: la regla del rango

Cada habilidad lleva sus evoluciones en `masteryModifiers`. Al dominarla (25 usos efectivos contra monstruos) el jugador elige una en K. **La evolución cambia un poco la habilidad y está activa desde el rango 0; cada rango (hasta el 3, uno cada 10 usos) solo mejora un poco sus números.** Todo valor es `base + extra por rango × rango`: `Amount` y `Amount por rango` (más `bleedDamagePerSecond` y `Daño extra por rango`, `Segundos`, `count`, `interval`, `bonus`, `threshold`, `width`, `distance`, `maxRepeats` según la evolución), todos editables en el Inspector. `AbilityModifierDefinitionDrawer` muestra de cada modificador solo los campos de su evolución. Los comportamientos nuevos se agregan al final de `AbilityModifierBehavior` porque se serializa por índice, y un modificador que se quita se deja con `retired` para que las partidas guardadas sigan siendo válidas.

### Evoluciones de la Hacha sola

| Habilidad | Evolución | Efecto (rango 0 → 3) |
|---|---|---|
| Básico | Cuarto corte | Cada 4.º básico seguido al mismo enemigo lo hace sangrar 3 s; el daño del sangrado pasa de 3 a 3,5 por segundo |
| Hachazo | Reabrir | Si el objetivo ya sangra, alarga su sangrado 3 s → 3,5 s |
| Hachazo | Barrido | El golpe cubre el doble de ancho; lo de los costados recibe 60 % → 70 % del daño |
| Desgarre | Desgarro profundo | Armadura reducida 39 % → 45 % (30 % sin evolución; misma duración) |
| Desgarre | Carne viva | Además sangra 3 s; 3 → 3,5 por segundo |
| Tajo sangrante | Filo oxidado | Daño del sangrado +50 % → +75 % |
| Combo furioso | Escalada | +10 % → +15 % de daño base por cada golpe anterior de la cadena que acertó |
| Combo furioso | Quebranto | El tercer golpe rompe la postura 1,3 s → 1,45 s |
| Filo cruel | Sangre fría | Los básicos contra un objetivo con la armadura reducida también dan 3 → 3,5 Focus |
| Verdugo | Ejecución | Contra un objetivo que sangra y tiene menos de 30 % → 35 % de vida, el bono de Verdugo sube a +40 % |

Hachazo (Potencia y Recuperación) y las Potencias de Filo cruel y Verdugo quedaron `retired`: ya no se ofrecen. Valores que se ajustan desde el Inspector y no están en el código: `Daño adicional de los básicos` de la familia (`WeaponFamilyDefinition.basicDamageBonus`, 0,15 en el hacha: alcanza solo al básico); `passiveValue` de Verdugo y Filo cruel; y `damagePerSecond`, `bleedSeconds` y `markSeconds` de `PrimeBleedAction`. El sangrado de Tajo sangrante hace 3 por segundo y, como los otros sangrados, se multiplica por el nivel del arma.

Un enemigo tiene un solo sangrado y gana el más fuerte (`CombatAilment.Poison`): uno igual renueva la duración, uno más débil no lo reemplaza ni lo alarga. Reabrir sí alarga el que ya corre (`CombatAilment.ExtendPoison`). Los efectos al impactar de un básico (Tajo, Cuarto corte, Filo cruel) se aplican al primer enemigo alcanzado por el golpe, porque `WeaponSkillEffects.BasicHit` corre una vez por golpe.

`OneHandAxeChecks.RunBatch` verifica estos datos y `OneHandAxeEvolutionChecks.RunBatch` las reglas en Play Mode, con el hacha real y un inventario en memoria que no toca el guardado.

### Evoluciones de Dos hachas

| Habilidad | Evolución | Efecto (rango 0 → 3) |
|---|---|---|
| Básico | Ráfaga | Cada 5.º básico seguido al mismo enemigo añade 2 golpes extra; cada uno pega 40 % → 46,7 % del daño |
| Hachazo doble | Remate | Al aterrizar y dar daño encadena un básico gratis; ese básico hace 100 % → 116,7 % de su daño |
| Giro mortal | Torbellino | 15 % → 17,5 % de probabilidad de repetir el giro una vez, sin costo |
| Lanzamiento | Hacha errante | Rebota a 2 enemigos más como máximo (≤ `distance`); cada rebote pega 30 % → 25 % menos que el anterior |
| Berserker | Matanza | Cada baja durante el modo cura 5 % → 5,8 % de la vida máxima |
| Berserker | Furia creciente | La velocidad de ataque sube +1,5 % → +1,75 % por segundo mientras dura el modo |
| Doble filo | Corte cruzado | Cuando sale el golpe doble, el segundo corte pega +20 % → +23,3 % |
| Sed de sangre | Sed insaciable | Con menos de 50 % de vida, la restauración también cura 5 % → 5,8 % de la vida máxima |

Las Potencias de Doble filo y Sed de sangre quedaron `retired`.

Cómo funcionan por dentro:
- **Remate:** `FuriousComboAction` deja pedido `AbilityExecution.Chain` cuando el aterrizaje alcanza a alguien; `AbilityRunner.Advance` lo ejecuta al terminar el tick (`ChainBasic` en `AbilityRunner.Chains.cs`) para no cambiar `Current` mientras se recorren las acciones. Termina el Hachazo (su recarga ya corre) y arranca el combo del básico con `BasicSwordCombo.RequestAttack(..., freeFirstStep: true)`: solo el primer paso es gratis. El daño extra lo da `WeaponSkillEffects.BoostNextBasic`, que multiplica solo ese básico.
- **Torbellino:** al terminar la fase activa, `AbilityRunner.RepeatActive` rebobina `Elapsed` al inicio de esa fase, limpia los blancos golpeados y vuelve a llamar `Begin` de las acciones. El clip del giro (`Combat_DualAxe_DeathSpin.anim`) repite la misma ventana; `DualAxeChecks` mide que la pose al principio y al final de esa ventana coincidan (hoy la diferencia es 0).
- **Ráfaga:** comparte el contador de Cuarto corte en `WeaponSkillEffects.BasicStreak`; los golpes extra son una corrutina corta (`interval` entre golpes) con la misma identidad de uso, así que la maestría cuenta una vez.
- **Hacha errante:** cada rebote es un vuelo visual (`ReboundFlight`) cuyo daño se aplica al llegar; elige el enemigo más cercano que no haya sido golpeado y el daño se acumula (100 % → 70 % → 49 % con 30 % de pérdida). Comparte el `abilityUseId` del lanzamiento.
- **Orientación del hacha lanzada:** el modelo del hacha tiene el filo en su eje +X; `AxeThrowAction.visualRotation` (Euler, por defecto `(0, -90, 0)`) lo gira respecto del vuelo para que el filo vaya adelante, tanto en el lanzamiento como en los rebotes (`ReboundFlight`, que mira a su destino y gira solo un pivote interno).
- **Timer del Modo Berserker:** `WeaponSkillEffects.BerserkTimer` da los segundos que quedan. `PlayerHUD` los muestra en el centro del ícono con una barrita (`ModeTimer`, color de los buffs de daño de `BuffPresentation`) en lugar del número de recarga, y el modo aparece como tarjeta de buff junto a la vida (`BuffView.mode`, sin símbolo en el mundo: eso queda para los VFX).
- **Matanza:** `DamageReceiver` avisa `WeaponSkillEffects.TargetDefeated` solo cuando el golpe mata. **Furia creciente:** `SpeedBonus` suma la tasa por los segundos transcurridos del modo, dentro del tope de velocidad de las reglas.
- **Corte cruzado:** `DamageDealer.ApplyTo` multiplica por `WeaponSkillEffects.SecondStrikeMultiplier` cuando la otra hacha golpea a un objetivo ya alcanzado en el paso. **Sed insaciable:** `TargetDefeatedOrOpened` cura después de restaurar estamina y Focus.
- **Pasivas:** `passiveValue` de Doble filo (0,5) y de Sed de sangre (0,2) reemplaza las constantes del código.

### Balance de Focus de las hachas

El Focus (máximo 100) se gana con los golpes y se gasta en las habilidades. Cada familia tiene generadores y gastadores; todo está en `focusCost` y `focusGainOnHit` de cada habilidad (Inspector).

| Familia | Genera al acertar | Consume |
|---|---|---|
| Hacha sola | Básico 5 por golpe, Combo furioso 4 por golpe (más Filo cruel y Sangre fría) | Hachazo 20, Desgarre 20, Tajo sangrante 30 |
| Dos hachas | Básico 3 por golpe, Hachazo doble 8, Lanzamiento 6 (más Modo Berserker, +1 por básico) | Giro mortal 25, Modo Berserker 40 |

El básico de Dos hachas da menos por golpe porque pega más del doble de rápido (0,45 s contra 0,98 s): el ritmo de Focus por segundo queda parecido. Las habilidades encadenadas sin costo (Remate, Torbellino, las pulsaciones 2 y 3 del Combo furioso) no vuelven a cobrar.

### Apuntado y contadores del HUD

- `WeaponAim.AimPoint` ignora lo que esté entre la cámara y el personaje y `WeaponAim.Direction` nunca deja una habilidad de espaldas a la cámara. Sin eso, el básico de la Hacha sola (apunta con la cámara) hacía girar al personaje hacia la cámara cuando el rayo pegaba en algo pegado a él.
- Tajo sangrante, desde que se lanza hasta que el básico entrega la marca, publica un `BuffView` como Tercer impacto: tarjeta "TAJO SANGRANTE · LISTO · N s", marco rojo en el ícono y, por `iconTimer`, la barrita ámbar de segundos (`BuffHud.DrawSkill`); los segundos del centro salen de `WeaponSkillEffects.BleedMarkTimer`. Tercer impacto también muestra esa barrita mientras está listo.
- El aura de la marca es `BleedMarkVisual`: tres gotas que orbitan con los mismos parámetros que la órbita compartida y la cabeza del hacha iluminada con el shader `Mismo/Sword Blade Glow` (copias de las mallas del arma, como la Estocada), latiendo y parpadeando en el último segundo. Sus ajustes están en `BuffPresentation` (sección "Marca de sangrado"); `BuffPresentationSetup.InstallBleedMark` crea la gota y el material. Entregar el sangrado todavía no tiene efecto de impacto.
- El aura del Modo Berserker es `BerserkVisual` y crece con la furia (`FuryAt`: la parte del modo ya transcurrida). Las dos hachas se iluminan con `WeaponHeadGlow`, el mismo helper que usa Tajo sangrante, y sueltan brasas voxel desde la cabeza (un emisor por hacha, en espacio mundial, así que dejan estela al golpear; un hacha lanzada no suelta). Además hay ondas en el piso cada vez más seguidas; y el borde rojo de pantalla, que dibuja `PlayerHUD` detrás del resto del HUD. La entrada lanza una onda grande y un golpe del borde; los últimos 1,5 s parpadean. Los ajustes están en `BuffPresentation`, sección "Modo Berserker" (`screenEdgeMax` en 0 apaga el borde), y `BuffPresentationSetup.InstallBerserk` crea los assets.
- Sonidos sintetizados (`AxeSoundSetup`, menú *Mismo/Audio/Generar sonidos de hachas*; *Regenerar* los sobrescribe). Son un port de las muestras elegidas en el navegador, con el mismo generador y las mismas semillas, y viven en `Art/Audio/Weapons/Axe` y `Art/Audio/Weapons/Double Axe`.
  - Los básicos de las dos familias comparten `Axe_Attack_Basic_Light` (muestra A "Filo ligero"). Es un whoosh corto, casi todo viento, con un poco del swing sin graves debajo, para que suene más liviano que las habilidades.
    - En Dos hachas, el golpe izquierdo (`comboStepSfx[1]`) usa `Axe_Attack_Basic_Light_Left`: la misma receta con otra semilla, el viento un 8 % más agudo y el swing un 4 % más rápido.
    - Los dos cortan a 0,34 s, donde cortaba el básico anterior, así la separación de 0,41 s de los golpes dobles sigue valiendo.
    - `Axe_Attack_Basic.mp3` queda sin uso en las habilidades; solo lo usan las alternativas guardadas del combo.
  - Combo furioso tiene un sonido por pulsación (`comboStepSfx[0..2]`, por etapa de reactivación): `Axe_Furious_Combo_1..3`, muestra B "Swing al viento". Es un barrido de viento con un "tss" de filo, sumado a `Axe_Attack_Swing` acelerado y sin graves (dos pasa altos de 330 Hz).
    - Cada clip empieza con la fase activa de su pulsación, y su pico cae en el golpe: `strikes[i].at × recastStages[i].active`, leído del asset al generar. Si cambiás esos tiempos, hay que regenerar; `Check` avisa si el pico quedó a más de 0,04 s del golpe.
    - Las otras dos muestras quedan guardadas sin uso en `Axe/Alternatives`: `BasicWind` ("Básico al viento") y `BodyWind` ("Viento con cuerpo").
  - Hachazo (`Axe_Forward_Swing_Heavy`, muestra A "Swing pesado") es el whoosh del combo, más lento y grave, sin golpe.
  - Desgarre (`Axe_Armor_Rend_Break`, muestra A "Rompe escudo") suma un golpe grave con sub, un crujido de astillas y restos que caen.
  - En los dos, el pico cae donde caía el del sonido anterior: 0,17 s y 0,22 s después de que arranca la fase activa.
  - `Axe_Attack_Armor_Rend.mp3` queda sin uso. `Axe_Attack_Swing.mp3` sigue siendo la base de los whooshes.
  - Giro mortal (`DualAxe_Death_Spin`, muestra B "Remolino") es un viento que gira toda la vuelta y se abre en el corte de cada hacha, con un poco del swing sin graves.
    - Los cortes salen de `hits[i].at × active` de su `FuriousComboAction`, leídos del asset al generar: hoy 0,21 s y 0,45 s.
    - Si cambiás esos tiempos, hay que regenerar.
  - Hachazo doble (`DualAxe_Leap_Slam`, muestra A "Salto y doble tajo") tiene despegue, aire que sube durante la caída y las dos hachas cortando con 25 ms de diferencia al aterrizar, sobre un golpe medio y tierra.
    - El aterrizaje sale de `hits[0].at × active`, leído del asset al generar: hoy 0,45 s.
  - Lanzamiento de hacha tiene tres partes:
    - **Lanzamiento** (`DualAxe_Throw_Release`, "Viento suave"): el sonido de ejecución, solo aire, grave y redondo.
    - **Vuelo** (`DualAxe_Throw_Flight`, "Hacha giratoria"): un loop de 1 s con 4 "vum" por segundo. Es `AxeThrowAction.flightSfx`, que `FlightAudio` pega como `AudioSource` 3D (rolloff logarítmico de 3 a 30 m) al hacha lanzada y a cada rebote de Hacha errante, y muere con ella.
    - **Impacto** (`DualAxe_Throw_Impact`, "Corte limpio"): `impactSfx`, un corte sin golpe grave. Suena solo si alcanza a un enemigo, y en cada rebote más bajo según el daño que conserva (`Lerp(0,6; 1; kept)`).
    - El regreso a la mano no suena, tampoco al agotar el recorrido.
  - Tajo sangrante (`Axe_Bleeding_Cut_Mark`) y la entrada del Berserker (`DualAxe_Berserk_Entrance`) son el sonido de ejecución de cada habilidad.
  - El latido, el loop de brasas y el fin están en `BerserkAction`. `BerserkVisual` dispara el latido en cada pulso del brillo de las hachas, sube el volumen de las brasas con la furia y solo hace sonar el fin cuando el modo se agota.
- El Modo Berserker muestra los segundos y la barra en el color de los buffs de daño (`BerserkTimer`), sin tarjeta junto a la vida.

`DualAxeChecks.RunBatch` verifica los datos y `DualAxeEvolutionChecks.RunBatch` las reglas en Play Mode con las dos hachas equipadas. Los básicos de Dos hachas aplican su daño en `AttackHitbox.LateUpdate`, así que esas pruebas avanzan cuadro a cuadro; además desactivan a los enemigos del arena, que se interponen en los swings y en los lanzamientos.

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
