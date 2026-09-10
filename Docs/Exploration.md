# Exploración, escalada y habilidad especial

Abrir `Assets/Scenes/VoxelRegion_7319.unity` y entrar en Play. `Assets/Resources/ExplorationWorldSettings.asset` activa la extensión procedural. El pueblo, terreno central, edificios y encuentros guardados se conservan. Los límites invisibles y el paisaje decorativo exterior se desactivan durante Play.

## Chunks

Fuera del cuadrado central se generan chunks de 32 × 32 metros al acercarse el jugador. Comparten la función de alturas de la generación original. Se crea como máximo un chunk por frame, priorizando los cercanos; se conservan tres chunks de radio por defecto y un margen de descarga de un chunk. Las mallas lejanas se destruyen. La navegación se reconstruye de forma asíncrona cuando termina una tanda de generación y conserva el suelo anterior hasta que termina la primera actualización.

La semilla y las coordenadas determinan el terreno y los árboles, incluso con coordenadas negativas. Volver a una zona descargada reconstruye el mismo contenido. El tamaño central, semilla, relieve y escalón deben coincidir con la escena original. Cambiar estos valores no regenera la parte central guardada.

Esta entrega genera terreno y vegetación; todavía no añade biomas, encuentros nuevos, persistencia de cambios en árboles ni regiones con mínimos de nivel. Los encuentros existentes siguen en el centro. No hay un límite explícito de exploración, pero distancias extremas siguen sujetas a la precisión de coordenadas de Unity. La generación por frame limita la cantidad de trabajo, no garantiza un presupuesto de milisegundos en todos los equipos.

## Trepar árboles

Acercarse mirando el tronco y mantener **ESPACIO**. Usar **W/S** para subir o bajar. Soltar ESPACIO deja caer al personaje. Tanto moverse como permanecer agarrado consumen **35 de stamina por segundo**; al agotarse se suelta automáticamente. Con 100 de stamina hay aproximadamente 2,9 segundos de agarre continuo. `TreeClimbing` permite ajustar consumo y velocidad; usa el motor y las colisiones existentes.

Los árboles actuales y los generados reciben `ClimbableTree`. Solo el tronco es escalable: las copas siguen sin plataformas de colisión. No se incorpora una animación específica de manos y pies para escalada en esta entrega.

## Habilidad especial

La tecla **C** mantiene el dash inicial. `SpecialAbilityDefinition` define nombre, duración, cooldown, restricciones, movimiento opcional y callbacks de inicio, actualización y limpieza. `SpecialAbilityExecution` almacena estado por activación y por jugador; el ScriptableObject se comparte sin guardar estado de ejecución.

`BeltDash` se conserva como componente compatible con las escenas existentes. Su campo **Special Ability** permite sustituir la acción inicial y `EquipSpecial` conecta una futura fuente de equipamiento. No presupone si será cinturón, alma o mascota. `DashBehaviour` es una especialización que controla movimiento; un efecto sin movimiento permite seguir caminando y usando armas.

Cambiar la habilidad, desactivar el componente o morir cancela la ejecución y llama a su limpieza. Cambiarla no borra el cooldown del slot. El HUD muestra el nombre de la habilidad. Escudo, robo de vida, flotación e invisibilidad son futuras implementaciones de efectos sobre esta interfaz; no se agregan como poderes completos en esta entrega.

## Validación

`ExplorationChecks.RunBatch` pasó nueve comprobaciones de geometría y habilidad especial, y ocho en Play sobre la escena real: arranque, árboles trepables, colisión y navegación exterior, límite de chunks cargados, ascenso, gasto y agotamiento de stamina. Las mallas se compararon con el generador original y con regeneraciones de la misma coordenada.

El editor de la copia de validación emitió la excepción ya conocida de su indexador `UnityEditor.Search.SearchDatabase`; las comprobaciones de gameplay finalizaron correctamente.

`MovementPrototypeChecks.Run` también pasó la regresión de suelo, salto, aterrizaje, recursos, cooldown y colisiones a 30, 60 y 144 FPS. El dash conservó sus 5 metros en las tres tasas.
