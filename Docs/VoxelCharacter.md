# Animaciones del personaje voxel

El personaje usa `Assets/Art/FBX/Voxel_Adventurer_Animated.fbx`, integrado en `PlayerVoxelSwordE.prefab` y en `VoxelRegion_7319`. La fuente editable está en `ArtSource/VoxelAdventurer/AnimationV2/Voxel_Adventurer_Animated.blend`.

## Clips nuevos

Idle tiene respiración y balanceo sutil, con brazos relajados. Walk y Run son ciclos distintos: contacto y elevación de pies, rodillas flexionadas, contrapeso de brazos y mayor inclinación en carrera. Jump, Fall y Land cubren despegue, caída y recepción del peso. Attack1, Attack2 y Attack3 son cortes con preparación y recuperación; se incluyen además Dash, Parry, Lunge y Spin para las habilidades existentes.

Solo Idle, Walk y Run están en bucle. Los ataques se muestrean con el tiempo normalizado del combo/habilidad: el daño continúa controlado por los hitboxes existentes. El salto y desplazamiento global continúan a cargo de PlayerMotor, con root motion desactivado. La espada conserva el vínculo a Hand.R y su textura original.

`PlayerAnimationDriver` decide el estado por movimiento real, sprint, velocidad vertical, aterrizaje y habilidades activas. `VoxelLocomotion.controller` contiene los 13 estados. Se retiró la superposición de poses procedurales del brazo y el estiramiento provisional del visual para este prefab. El feedback sonoro y de estela se conserva.

Se corrigió el aviso Landed de PlayerMotor: ahora compara el estado de suelo tras resolver el movimiento y lo emite al pasar de aire a suelo.

## Validación

Exportación reimportada en Blender: 13 acciones, 22 huesos, espada vinculada y raíz sin desplazamiento. Se revisaron secuencias de poses y se generó `AnimationV2/Animaciones.gif`.

Compilación y pruebas en Unity aprobadas en una copia aislada. Se ejercitó el motor real durante Idle, Walk, Run, Jump, Fall, Land, combo de tres golpes, recuperación y Dash; también se verificaron los bucles y el vínculo del arma. Evidencia: `Docs/Validation/VoxelAnimationChecks.txt`. El HUD se desactivó solo en la prueba sin gráficos.

Las animaciones son una primera pasada propia sobre el rig voxel existente, ajustable en Blender; no son captura de movimiento. El proyecto conserva el personaje y las exportaciones anteriores. La escena, prefab y controlador previos están respaldados en `ArtSource/VoxelAdventurer/AnimationV2/BeforeAnimationUpgrade`.

## Correcciones tras revisar gameplay (7 de septiembre)

El controlador estaba enlazando los subassets `__preview__` del FBX. Estos clips de previsualización no respetaban el bucle del clip de juego: el estado Run seguía activo con una pose inmóvil. La integración ahora excluye esos subassets y la reparación actualiza las referencias de los 13 estados sin reconstruir la escena ni el prefab.

Spin rota alrededor de la vertical del personaje (Y en Unity). En Blender, esa vertical es Z del mundo, pero corresponde a Y local del hueso Root; usar Z local tumbaba al personaje. Se corrigió también la conversión del desplazamiento de cadera y del giro de torso, y se ajustaron la zancada, elevación de pies y balanceo de brazos de Run.

La importación intermedia de sword_E cambiaba la frecuencia de la escena de 60 a 30 fps, duplicando la duración exportada. El autor restaura 60 fps antes de guardar/exportar y la prueba exige la duración prevista de Run (37/60 segundos).

La presentación tolera pérdidas de contacto de hasta 0,12 segundos al bajar escalones, salvo cuando hay velocidad ascendente de salto. Land se reserva para aterrizajes detenido; al aterrizar en movimiento vuelve directamente a caminar/correr. La física de desplazamiento no cambia.

Las pruebas ampliadas comprueban las referencias reales de los estados, cuatro segundos de carrera con movimiento de pierna y varios ciclos, escalones descendentes y la verticalidad durante R. Las vistas actualizadas de Run y Spin están en `Docs/Validation/GameplayReview/Run_fixed.gif` y `Spin_fixed.gif`.

## Dirección del ataque

El hitbox del combo se orienta con PlayerMotor.Facing después de actualizar el movimiento. Su consulta física usa el volumen de la caja orientada, y descarta objetos que no reciben daño. Las partículas del combo siguen esa misma orientación y las de la estocada su dirección de avance.

La estela de sword_E sigue un vértice de la punta de la espada evaluada por el esqueleto; reutiliza una malla temporal y la lista de vértices. Esto evita depender de un desplazamiento fijo sobre los ejes de la mano importada.

La regresión coloca objetivos delante y detrás en cuatro orientaciones y verifica daño frontal, ausencia de daño trasero, origen de partículas y correspondencia de la estela con la espada.
