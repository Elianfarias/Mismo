# Integración procedural de Enchanted Grove

El catálogo real `Assets/Data/World/WorldContentCatalog.asset` referencia ahora `GroveWorldStyle.asset`. Los perfiles de Meadow, Forest y Highlands usan los árboles, arbustos, hierbas, flores, hongos y rocas del kit. Cada bioma tiene pesos distintos; el bosque favorece salvia, cipreses y helechos, la pradera tiene más flores y árboles rosados, y las tierras altas favorecen ocres, cipreses y rocas. Se conservan los perfiles propios de desierto, hielo, montañas y océano.

## Terreno y colocación

- La superficie usa manchas de color en coordenadas globales, con caminos distinguibles y piedra cálida en las caras verticales. La paleta se convierte a espacio lineal para coincidir con las texturas sRGB de los modelos.
- TerrainSurface respeta el marcador de color literal del terreno (alpha de vértice cero). Antes volvía a teñir esos colores como hierba genérica. Se conserva el enmascaramiento del horizonte en las pasadas de color, profundidad y normales.
- El suelo conserva sus alturas, resolución, escalones, colisiones y caminos. No se instala la base cuadrada del diorama en el mapa ni se modifica la semilla o versión de generación de las partidas.
- Hierba, arbustos y flores tienen densidad modulada por manchas amplias para alternar grupos y espacios abiertos. Se mantienen las comprobaciones de pendientes, reservas y apoyo del generador.
- Hay como máximo dos enredaderas por chunk: requieren un borde con caída real y apoyo a lo ancho. No tienen collider. Las luciérnagas aparecen en los sitios secretos de biomas verdes.
- Las ruinas usan un grupo modular de arco, columna, muro y vegetación, dejando el paso del arco libre. Los edificios y ruinas respetan también el margen de su huella respecto de los caminos.
- El material WorldWater se aplica a mar y manantiales existentes. Las posiciones y reglas de agua anteriores se mantienen; no se añaden lagos, cauces o puentes sin soporte.

## Recolección y rendimiento

Árboles, flores/hongos y rocas del catálogo conservan su integración con GatheringNode. Los arbustos del kit y la hierba son decorativos. Los recursos genéricos de madera, piedra y hierba usan también los nuevos prefabs disponibles; recompensas, tiempos, identificadores persistentes y minerales especializados se conservan.

Los árboles y arbustos mantienen sus LOD. Las plantas pequeñas se ocultan por debajo del 2 % de altura relativa en pantalla; no permanecen dibujadas hasta el borde de todos los chunks cargados. La integración no duplica mallas por instancia ni añade luces a cada farol.

La entrada de House usa Cottage_Exterior. El poblado completo y el taller existentes conservan sus referencias para mantener NPC, servicios y distribución funcional. Puente, escalones, nenúfares y juncos siguen disponibles como piezas del kit; no se dispersan por suelo seco ni se colocan sobre caminos sin una regla geográfica apropiada.

## Aplicación y comprobaciones

No hace falta crear una partida nueva. Salir y volver a entrar en Play Mode o recargar el mundo permite reconstruir sus chunks con los datos actuales. En partidas antiguas con centro previamente construido se mantiene ese centro; sus árboles ya tenían un reemplazo por catálogo en runtime y reciben los nuevos prefabs. Los demás cambios de terreno se ven en los chunks reconstruidos.

Herramienta de integración: **Mismo > World > Enchanted Grove > Integrar en mundo procedural**. Actualiza intencionalmente los perfiles verdes, las entradas de casa/ruina, tres recursos comunes y el material del terreno. El estilo y la densidad se pueden ajustar después en sus assets del Inspector; no es necesario volver a ejecutar la herramienta para jugar.

Informes y captura en `output/grove-world/`:

- `checks.txt`: 2.898 comparaciones de alturas y reservas, versiones de generación 0/1/2, dos semillas, selección determinista y dependencias serializadas.
- Pruebas de TerrainCoverage: continuidad del horizonte y enmascaramiento de color, profundidad y normales.
- `play-mode.txt`: streamer real, 25 chunks, modelos/LOD/viento, recolección y persistencia después de descargar y recargar el chunk. Inventario en memoria, sin escribir partidas del usuario.
- `build.txt`: resultado de la build Windows con contenido integrado.
- `organization.txt`: fallos de organización anteriores del proyecto, fuera del kit.

La captura `procedural-forest.png` usa el campo de alturas real, el catálogo y la dispersión procedural, con luz fija de inspección. El aspecto durante el juego también depende de su ciclo día/noche. No se ha medido aún una tasa de FPS objetivo para un bosque completo.

La build Windows finalizó con resultado Succeeded y un error reportado por ProjectOrganizationChecks: assets anteriores de DoubleL, Bestiary y otros proveedores están fuera de sus ubicaciones establecidas. El ejecutable se generó, pero el chequeo general de organización no está limpio.
