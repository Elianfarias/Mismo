# Suelo y llegada al pueblo

Se eliminó Stone foundation del prefab MedievalVillage: el suelo es ahora el terreno nivelado del mundo. La generación del prefab tampoco vuelve a crear la plataforma.

Las partidas nuevas aparecen once metros por fuera del centro del puente abierto, mirando hacia la puerta. La altura se calcula sobre el terreno. El catálogo WorldContentCatalog expone Village Spawn Offset y Village Spawn Yaw para ajustar la llegada. Continuar conserva la posición guardada. Se reserva espacio alrededor de la llegada para evitar vegetación bloqueando al personaje o la cámara.

La cámara comienza orientada como el personaje; conserva sus nueve metros de distancia y la protección contra obstáculos.

El material Assets/Resources/TerrainSurface.mat controla las nuevas superficies procedurales:
- Grass Color: tono del pasto.
- Dirt Color: tono de los caminos y de las caras de tierra.
- Texture Detail: intensidad del detalle pixelado.
- Pixels Per Metre: tamaño del detalle.

El patrón usa coordenadas globales, así que no hay costuras entre chunks. Se suaviza a distancia para reducir parpadeo. Afecta al terreno y deja los materiales de árboles, casas y criaturas como estaban. La niebla permanece desactivada según la configuración actual del usuario.
