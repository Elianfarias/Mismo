# Presentación del combate

## Espada sola: Rompeguardia a dos manos (2026-10-03)

`Sword_GuardBreaker_TwoHanded.anim`, en `Assets/Art/Animations/WeaponCombat/OneHandSword`, contiene una carga baja, elevación de una sola espada sobre la cabeza, descarga vertical y salida baja con ambas manos en la empuñadura. Los hombros acompañan la elevación para despejar la cara; la pierna delantera da un paso de apoyo. La hoja gira 90° sobre su eje longitudinal para bajar con el filo: su cara ancha queda en el plano vertical del corte. Este giro está horneado en las manos del clip, sin cambiar la orientación global de la espada. Es un clip Humanoid de cuerpo completo y 0,9 s, sin desplazamiento de la cápsula ni IK adicional en runtime.

El enlace propio de `GuardBreaker` en `OneHandSwordCombatAnimations` usa este clip: activo a 0,5 s (`activeStartsAt = 0.5555556`), recuperación a 0,7 s (`recoveryStartsAt = 0.7777778`) y mezcla de 0,045 s. Se mantienen preparación/activo/recuperación de 0,5/0,2/0,2 s, recarga de 8 s, daño 20, postura 90, alcance y sonidos. Se retiró la referencia de preparación a `BladeCharge` de esta habilidad para quitar el círculo sobre la espada; el prefab compartido sigue disponible para otras habilidades. No cambian los enlaces de Parry, Estocada, combo ni otras familias.

Fuente reproducible: `SwordGuardBreakerAnimationSetup.Run`, menú **Mismo → Armas → Reconstruir Rompeguardia a dos manos**. Hornea curvas Humanoid desde el rig del mago y el Idle existente; no modifica modelos, avatares ni prefabs. Al reconstruir conserva el GUID y modifica sólo el enlace de Rompeguardia.

`SwordGuardBreakerChecks.RunBatch` se ejecuta exclusivamente en una copia `.validation`: comprueba el agarre y la orientación del filo durante toda la descarga en mago, caballero y rana; reproduce el Player real con una espada; verifica ausencia del círculo, pausa, recuperación, cancelación y un único contacto a 0,51 s (muestreo de 10 ms), capaz de romper 80 de postura con los 90 existentes. También construye y recarga un bundle con el clip y su enlace. Pasó `ProjectOrganizationChecks.Run` sobre la copia aislada; no equivale a una auditoría del contenido completo del proyecto ni a una build completa del juego.

Capturas de Play Mode y resultados: `output/guard-breaker/`. La sesión de validación registró además una excepción del índice de búsquedas de Unity y avisos de asignaciones temporales al cerrar; se conservan por separado en `validation-notes.txt`. No hubo errores de compilación ni fallos de las comprobaciones de Rompeguardia.

## Espada: Estocada y Parry (2026-10-03)

Concepto aprobado en `Assets/Art/Textures/Concepts/Weapons/Sword/SwordVfxApproved.png`.

- Combo básico: se mantienen movimientos, sonidos y VFX aprobados.
- Estocada: `SwordLungeTurquoise.prefab` ilumina la hoja completa en turquesa usando su malla y los extremos del perfil de arma, sin modificar materiales compartidos. La estela se genera a partir del desplazamiento real de la hoja y los laterales del personaje, con muestras en coordenadas de mundo que permanecen atrás, se afinan y caducan a los 0,18 s. No genera arcos ni círculos prefabricados: sin desplazamiento no hay cinta. Pequeñas partículas acompañan el movimiento. Dura 0,38 s, con desvanecimiento durante la recuperación. Conserva su audio y animación.
- Parry, intento: usa el trail normal de `WeaponTrailPresentation`, siguiendo base y punta de la hoja en la animación, con el color, material y duración del perfil del arma. `weaponVfx` queda vacío: el prefab de whoosh visual ya no se instancia ni se regenera. Conserva el sonido de aire `Sword_Parry_Whoosh.wav` de 0,16 s.
- Parry, movimiento: `Sword_Parry_Deflect.anim` sustituye el ataque ascendente reutilizado. Clip Humanoid de 0,62 s: entrada a guardia en 0,09 s, un desvío hasta 0,19 s, guardia estable hasta 0,50 s y regreso a reposo en 0,12 s. El enlace empieza en el fotograma inicial y sincroniza recuperación con 0,50/0,62; conserva máscara y comportamiento de defensa. `SwordParryAnimationSetup` lo hornea desde la pose de reposo y la trayectoria del brazo, sin correcciones de arma en runtime. Prueba de movimiento con reloj fijo, capturas y contactos reales: `output/parry-motion/playmode.txt`.
- Parry confirmado: `CombatImpactPool` conserva el sonido metálico existente y emite `SwordParryContact.prefab` únicamente tras Parry/PerfectParry. Ráfaga exagerada de 40 chispas voxel de 0,12–0,20 m, velocidad 2,4–5,5 m/s y vida 0,32–0,48 s. `useDefenderBlade` proyecta el contacto sobre la hoja y toma su color de `trailStartColor`, aclarado para que las chispas se lean. Sin una hoja válida se conserva el punto y tinte del perfil.

`WeaponAbilityVfx` sigue siendo dueño de la duración y limpieza: cambiar arma, cancelar o morir retira el efecto; la pausa detiene su avance. No se agregan hit stop ni receptores de cámara: Feel mantiene el canal 7401 existente. Las demás habilidades quedan pendientes de revisión.

La revisión de movimiento pasó Play Mode con guardia inmóvil durante la defensa y parries contra el goblin. La build y recarga de contenido incluyeron el clip Humanoid y su enlace (`output/parry-motion/content-build.txt`). La revisión global de organización conserva incidencias previas de assets ajenos; informe en `output/parry-motion/organization.txt`.

La revisión del trail normal y las chispas exageradas pasó en la copia aislada: trail de hoja visible sin prefab de intento, ráfaga grande sólo al confirmar contacto, defensa contra goblin y build/recarga del contenido. Capturas e informes en `output/parry-default-trail`.

Herramienta de integración: **Mismo → Armas → Aplicar concepto Estocada y Parry** (`SwordConceptFeedbackSetup`). Los archivos nuevos respetan Art/Prefabs, Art/Materials, Art/Meshes, Art/Shaders y Art/Audio. Los prefabs compartidos anteriores no se modifican ni eliminan. Informes en `output/sword-concept-feedback`; las pruebas y capturas del personaje real se ejecutan con `ParryRegressionPlayChecks` y quedan en `output/parry-regression`.

Validado: compilación, build y recarga de dependencias; activación, pausa, cancelación y conservación de materiales de Estocada; whoosh sin feedback de contacto al fallar Parry; seis contactos frontales en distintas ventanas con/sin VFX y ataque real de goblin con/sin defensa. Capturas revisadas: `estocada-turquesa.png`, `parry-intento-whoosh.png` y `parry-efectivo-chispas.png`. El chequeo global de organización sigue detectando assets previos ajenos a estos efectos; detalle en `output/sword-concept-feedback/organization.txt`. No equivale a una build completa del juego.

Abrir `Assets/Scenes/VoxelRegion_7319.unity` y entrar en Play. No hace falta regenerar la región.

- HUD: vida y stamina numéricas, teclas y estados de combo, Q, E, R y C; cooldowns reales y aviso de stamina insuficiente para estocada y giro.
- Minimap local de 90 m orientado al norte, con dirección del jugador. Renderiza a 256 px cinco veces por segundo. No es un mapa global ni un sistema de descubrimiento.
- Espada: corrige la escala heredada de la mano importada, diferencia cortes alternados y golpe descendente, y sincroniza el barrido con las ventanas de impacto. Estela solamente durante acciones activas; desaparece al cancelar.
- Enemigos: barras en pantalla por proximidad y visibilidad, identificación del Elite, barra destacada de boss y avisos de preparación. Se ocultan los antiguos billboards. Se agregan cinturón, hombreras, pupilas y cejas a los prefabs existentes; los tintes conservan el detalle de los materiales.

El HUD lee los componentes existentes. Las animaciones siguen siendo procedurales; no se incorporaron clips de captura de movimiento ni un rig nuevo. No cambiaron vida, daño, alcance ni costes de habilidades.

`CombatPresentationUpgrade.Apply` aplica detalles de enemigos de forma idempotente. `InitialRegionChecks.RunPresentationBatch` verifica HUD, render texture del minimapa, escala del arma, estela durante impacto y cancelación, y repite las comprobaciones de la región. Esas pruebas y la compilación Windows finalizaron correctamente en una copia temporal.

Las capturas de pantalla en segundo plano resultaron negras y no se consideran validación visual. Queda comprobar en juego el encuadre, las poses y la legibilidad a distintas resoluciones. El helper `CombatPresentationCapture` funciona solamente en builds de desarrollo con el argumento explícito `-presentation-capture`; no se activa en partidas normales.

## Corrección de escala y recursos Town (2026-09-07)
- La espada se separa de la mano importada (escala 100), mantiene escala mundial 1 y aplica poses en metros siguiendo la orientación del motor. Se verificaron capturas reales en reposo y durante impacto.
- EnemyNameplate oculta las referencias serializadas de Status de GoblinPresentation y BossPresentation; elimina la búsqueda incorrecta por nombre Billboard.
- Town contiene diez OBJ con una paleta PNG idéntica ya incluida. TownVoxelPalette conecta esa textura, sin mipmaps ni filtrado bilineal, a todos los modelos mediante sus importadores. No se necesitan imágenes generadas.
- TownMaterialChecks verifica los materiales de los diez modelos y renderiza una vista conjunta. Los modelos quedan listos para colocar; esta corrección no cambia la distribución del pueblo.
- Capturas: Validation/sword-rest.png, Validation/sword-impact.png, Validation/town-materials.png.

## Integración procedural de Town (2026-09-07)
- Las nuevas regiones llaman a TownVillageBuilder: cuatro casas importadas sustituyen las casas de bloques, con 26 elementos auxiliares de Town. Los modelos conservan sus materiales y vínculo al OBJ.
- Tamaño uniforme calculado desde la geometría, pivote apoyado en el terreno y colisiones simples para casas y objetos sólidos. Vegetación sin colisión.
- La semilla determina variantes de cajas, tamaños y rotaciones de decoración. Los cuatro lotes y el corredor central permanecen controlados para preservar navegación y Scope 0. Las casas son exteriores, sin interiores nuevos.
- La escena VoxelRegion_7319 ya contiene el pueblo integrado. Para otras escenas existentes: Mismo > World > Update Town In Current Region; guardar la escena después. Las nuevas regiones lo incorporan automáticamente.
- Validación en copia temporal: cuatro casas y 30 instancias totales, textura asignada, calle central despejada, aproximaciones accesibles, navegación, recompensa, boss y respawn correctos. Vista revisada: Validation/town-integrated.png.
