# Pueblos grandes, apoyados y poco frecuentes

## Resultado

- El pueblo se instancia al doble de su escala anterior: aproximadamente 88 metros de ancho, con casas, puertas, murallas y espacios interiores escalados juntos.
- El origen del modelo tenía las bases de las estructuras a 0,1867 m del suelo. La colocación ahora compensa esa diferencia por la escala y deja 2 cm de penetración para evitar huecos.
- El terreno se nivela bajo toda la huella ampliada y conserva una transición exterior. También se amplían las reservas de vegetación.
- La entrada inicial acompaña la nueva escala y conserva espacio para la cámara.
- La distribución usa regiones grandes con un solo candidato en su interior. Con los valores actuales, las regiones miden 1.024 m por lado y cada una tiene un 65 % de probabilidad de contener un pueblo. La región inicial conserva únicamente el pueblo garantizado.
- Los sitios del mapa consultan la misma generación, por lo que reflejan la distribución nueva.

## Configuración

`Assets/Resources/WorldContentCatalog.asset`:
- Village Size Multiplier: 2.
- Village Ground Offset: -0.19670273, en unidades del prefab antes de escalar.
- Village Spawn Offset: coordenada de llegada antes de escalar.

`Assets/Resources/ExplorationWorldSettings.asset`:
- Village Region Cells: 8. Cada celda usa Site Spacing (128 m).
- Village Region Chance: 0.65.

## Partidas existentes

Se conserva la semilla y el perfil. Al continuar un guardado anterior a este cambio se actualiza su punto de reaparición. Si el jugador estaba dentro de un pueblo que se mantiene, se mueve una vez a la entrada para evitar quedar encerrado por las casas ampliadas. Si estaba lejos, conserva la posición. La distribución antigua de pueblos se reemplaza por la nueva al reconstruir el mundo.

No incluye NPC nuevos ni teletransporte: esas mecánicas siguen pendientes de desarrollo. La build HTML exportada previamente no se modifica automáticamente al editar el proyecto.

## Verificación

`VillageSizeChecks.RunBatch` valida distribución en tres semillas, separación entre pueblos, nivelación, apoyo de diez grupos estructurales, migración de posiciones, espacio de cámara y navegación desde la entrada a la plaza. Informes y captura en `Docs/Validation/VillageSize`.
