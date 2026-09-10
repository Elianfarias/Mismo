# Región procedural voxel

Escena jugable: `Assets/Scenes/VoxelRegion_7319.unity`. Abrir y presionar Play. La escena anterior `InitialRegion` se conserva como prueba de combate; esta región implementa la dirección del GDD v0.2.

La región central ahora se extiende durante Play con chunks, árboles trepables y navegación dinámica. Configuración y controles: [Exploration.md](Exploration.md). El resto de este documento describe la generación original de la zona central.

## Generar otra variante

1. Abrir **Mismo > World > Voxel Region Generator**.
2. Elegir **Crear o cargar configuración**.
3. Ajustar semilla, tamaño, relieve, escalón y vegetación.
4. Elegir **Generar nueva región**. Unity solicita guardar cambios pendientes antes de cambiar de escena.

El generador crea una escena y una carpeta de assets nuevas. Nunca reemplaza una escena editada. Para retirar una variante descartada, eliminar explícitamente su escena y su carpeta en `Assets/Data/World/Generated`, después de comprobar que no se usan. La herramienta registra el grupo creado en Undo; los assets guardados no forman parte de ese Undo.

## Modelo y alcance

- Mapa de alturas escalonado, celdas de 1 m y escalones iniciales de 0,25 m. El tamaño se redondea a múltiplos de 32 m dentro de 256–384 m.
- Mallas de sectores de 32 m, con caras expuestas, color por vértice y MeshCollider. No hay objetos individuales por voxel.
- Paisaje distante de menor resolución sin colisión y límites invisibles fuera del recorrido normal.
- Árboles con copas voxel y mallas compartidas; collider solamente en el tronco. Plantas pequeñas agrupadas y sin colisión.
- Relieve y vegetación dependen de la semilla. Las ubicaciones de pueblo, rutas, secreto y encuentros están diseñadas y son fijas en esta versión.
- Casas y ruina son geometría provisional. El secreto es una ruina abierta; no hay cuevas volumétricas, bloques editables, generación durante Play ni mundo infinito.

La misma semilla, configuración y versión del generador produce el mismo paisaje. Editar parámetros no cambia una escena ya guardada. Morir recarga el mismo mapa y reinicia enemigos, boss y recompensa.

## Edición manual e integración

`RegionGeometry` contiene `GeneratedGeometry` y `AuthoredGeometry`: ambas ramas estáticas alimentan `GoblinNavigation`. Los enemigos, la recompensa y la puerta quedan fuera. Cambiar geometría requiere volver a iniciar Play para reconstruir navegación.

La generación valida una ruta navegable desde el pueblo hasta cada lugar importante antes de guardar. Un fallo informa el destino inválido y retira solamente los assets de esa generación; las variantes previas se conservan.

Se reutilizan jugador, cámara, espada, goblins, Elite visual, boss, restauración de salud y retorno al pueblo. No se modifican estadísticas ni controles. La victoria abre el santuario posterior; no carga una segunda región.

## Validación

En una copia del proyecto, `VoxelRegionGenerator.BuildBatch` genera la semilla 7319 y comprueba el relieve de tres semillas. `VoxelRegionGenerator.ValidateAdditionalSeeds` construye y valida navegación para las semillas 17 y 942.

`InitialRegionChecks.RunVoxelBatch` ejecuta Play Mode, comprueba navegación, camina rutas usando PlayerMotor sin saltar, verifica la puerta, la restauración, la victoria y el regreso al pueblo. Las IA y colliders enemigos se desactivan durante la prueba de caminata para aislar el terreno; esto no modifica la escena guardada.

Las vistas y resultados de la ejecución se guardan en `Docs/Validation`. Las comprobaciones automáticas no certifican el ritmo de exploración, la dificultad del combate o FPS en otros equipos. Esos ajustes requieren pruebas de juego.

La semilla entregada pasó navegación, caminata física sin saltos hasta los encuentros y el acceso a la ruina, bloqueo de puerta, restauración única, victoria y reinicio del intento. Las semillas 17 y 942 también se generaron y pasaron navegación. La construcción de la semilla elegida tardó aproximadamente 10 segundos en la copia de validación; no es una garantía para otros equipos.

También se compiló correctamente una build de desarrollo Windows de la escena elegida mediante `InitialRegionChecks.BuildVoxelPlayer`. La build se produjo en la copia temporal de validación; no se incluyó el ejecutable en los assets del proyecto. La escena voxel figura primero en la lista de escenas del proyecto, conservando las escenas previas.

Unity emitió una excepción de su indexador de búsqueda al iniciar la copia de pruebas. Las comprobaciones de gameplay finalizaron correctamente; no se atribuye ese mensaje al generador.
