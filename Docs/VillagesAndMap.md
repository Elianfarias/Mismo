# Pueblos medievales y mapa

El pueblo usa la composición `Medieval_Voxel_Assets_Example.obj` del paquete Medieval Assets: murallas, torres, viviendas, mercado y decoración. Se genera una copia sin el gran suelo de presentación ni puertas/puentes cerrados. Los archivos originales se conservan. La plaza está alineada con el punto de inicio del mundo y el perímetro se reserva para evitar árboles y vegetación dentro de las murallas.

## Aparición

WorldContentCatalog contiene la entrada `medieval.village`, de tipo Village. ExplorationContent instancia el prefab completo en cada WorldSiteKind.Village, sustituyendo la composición provisional de cuatro casas. Respeta los puntos de interés de la semilla existente. En el centro original de partidas legacy/Play directo también sustituye el pueblo inicial.

Prefab: `Assets/Prefabs/World/Villages/MedievalVillage.prefab`. Materiales y modelo adaptado: `Assets/Art/Materials/MedievalVillage`. Se pueden añadir otros prefabs Village al catálogo y controlar su peso y biomas. El menú Mismo → World → Integrate medieval village reconstruye el prefab y materiales del ejemplo; sobrescribe cambios manuales dentro del prefab generado. Las tiendas son edificios visuales; no se agregan NPC ni nuevos sistemas comerciales.

## Mapa

- M: abrir/cerrar; Escape: cerrar.
- Rueda o botones +/−: zoom.
- Arrastrar con botón izquierdo/central: desplazar.
- Arrastrar con botón derecho: girar e inclinar.
- WASD: desplazar; Inicio o Mi posición: centrar en el jugador.
- Norte: restablecer orientación.

Vista 3D del relieve de la misma semilla, con marcadores del jugador, pueblos, ruinas, arenas y santuarios. Puede explorar zonas no cargadas sin generar sus chunks ni enemigos. Es un mapa de consulta completo, sin niebla de exploración. La geometría se calcula progresivamente en una cuadrícula acotada y se reutiliza una RenderTexture; no crea una segunda copia jugable del mundo.

Mientras está abierto se bloquean movimiento, ataques y giro de la cámara del personaje; las acciones activas se cancelan. El mundo sigue transcurriendo. No se abre a la vez que el inventario. Al cerrar se restaura el cursor y se bloquea el mismo frame de cierre para evitar ataques accidentales.

Las comprobaciones reproducibles están en VillageMapChecks.RunBatch; se ejecutan con datos temporales en una copia del proyecto y no leen ni escriben partidas personales. Capturas e informes en Docs/Validation.
