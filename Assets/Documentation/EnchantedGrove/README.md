# Enchanted Grove — kit voxel para Mismo

31 prefabs originales para un entorno de fantasía de paleta suave: salvia, rosa, ocre, piedra cálida, madera y agua turquesa. La imagen aportada por el usuario se usa como referencia de ambiente. El diorama demuestra cómo combinan las piezas; no define un mapa que deba reproducir el generador.

## Abrir y usar

- Escena de muestra: `Assets/Scenes/Previews/EnchantedGrove.unity`.
- Prefabs independientes: `Assets/Art/Prefabs/World/EnchantedGrove/`.
- Fuentes editables en MagicaVoxel: `Assets/Art/Source/Environment/EnchantedGrove/`.
- Mallas, materiales y paletas: `Assets/Art/{Meshes,Materials,Textures}/Environment/EnchantedGrove/`.
- Capturas y costes por pieza: `output/enchanted-grove/`.

La escena tiene su propia luz, perfil de color y cámara. En Play Mode, `GrovePreviewEnvironment` inicia el sistema de viento existente con una configuración temporal y sin cargar el catálogo del mundo. Las luciérnagas usan partículas. Para inspeccionar las piezas, arrastrar sus prefabs a otra escena; el viento de vegetación se activa cuando el sistema del mundo esté inicializado.

El generador del kit mantiene separada la escena de muestra. La integración posterior en el mundo procedural se documenta en [WorldIntegration.md](WorldIntegration.md). El perfil de color de la muestra está en `Assets/Data/Rendering/EnchantedGrove/PreviewLighting.asset`.

## Contenido

| Familia | Prefabs |
| --- | --- |
| Árboles | Tree_Sage, Tree_Blossom, Tree_Amber, Tree_Cypress |
| Arbustos | Shrub_Sage, Shrub_Hydrangea |
| Sotobosque | Fern, Grass_Tuft, Flowers_Ivory, Flowers_Coral, Flowers_Lavender, Mushrooms |
| Agua y orillas | Pond_Water, Reeds, Lily_Pads |
| Roca y desniveles | Rock_Moss_Large, Rock_Moss_Small, Cliff_Moss |
| Vegetación colgante | Vines_Long, Vines_Short |
| Ruinas y caminos | Ruin_Arch, Ruin_Pillar, Ruin_Wall, Stone_Steps, Path_Stones |
| Construcciones | Cottage_Exterior, Footbridge |
| Ambientación | Lantern_Post, Lantern_Ground, Fireflies |
| Base de presentación | Diorama_Terrain |

## Coste y escala

| Pieza | LOD0 | LOD1 | LOD2 |
| --- | ---: | ---: | ---: |
| Tree_Sage | 10.488 | 2.632 | 638 |
| Tree_Blossom | 10.526 | 2.562 | 626 |
| Tree_Amber | 10.480 | 2.556 | 638 |
| Tree_Cypress | 5.406 | 1.434 | 444 |
| Shrub_Sage | 1.954 | 476 | 136 |
| Shrub_Hydrangea | 2.134 | 562 | 162 |
| Rock_Moss_Large | 1.602 | 460 | 146 |
| Cliff_Moss | 4.174 | 938 | 264 |

Son triángulos por instancia con un único LOD activo, sin contar pasadas de sombras. Cottage_Exterior tiene 2.474; Footbridge 608; Ruin_Arch 554. El inventario completo está en `assets.txt`.

1 unidad de Unity = 1 metro. Los árboles miden aproximadamente 5,9–6,4 m. Los LOD cambian a 18 %, 7 % y 1,8 % de altura relativa en pantalla; debajo de eso se ocultan. Es un punto inicial de ajuste, no una garantía de rendimiento para cualquier densidad de bosque. Se comparten las dos texturas de paleta de 64 × 1 píxeles y el material principal. No hay una luz en tiempo real por farol: las ventanas y faroles usan emisión selectiva en la paleta. Fireflies emite hasta 24 partículas de 12 triángulos cada una.

## Pivotes, colisiones y límites actuales

- Árboles y construcciones se colocan desde su base. Los árboles tienen cápsula de tronco; su copa no bloquea el paso. Arbustos y plantas bajas no tienen collider.
- Vines_Long y Vines_Short cuelgan desde el pivote superior hacia Y negativo; necesitan una superficie de soporte. Son estáticas en este kit.
- Cliff_Moss se coloca desde la base. El módulo mide unos 4,5 m de ancho y 5 m de alto. Las rocas y ruinas usan colliders simples; el arco conserva el paso central.
- Path_Stones es decoración superficial. Footbridge y Stone_Steps tienen colliders por tablón/peldaño. Las barandas del puente son visuales por ahora.
- Cottage_Exterior es un exterior con puerta cerrada; no tiene interior habitable, apertura de puertas ni navegación de NPC.
- Pond_Water es una superficie opaca con color de orilla y ondas suaves en shader. No implementa natación, profundidad de juego, reflejos del escenario ni cascada. Se adapta mediante posición/escala; Lily_Pads se apoya sobre ella.
- Diorama_Terrain está hecho para comparar el kit. No es un nuevo sistema de terreno ni se registra para generación procedural.
- La integración procedural usa una variante del shader para mar y manantiales existentes; ver WorldIntegration.md. La iluminación y el encuadre de la muestra son parte de su apariencia.

## Editar y regenerar

Cada malla voxelizada tiene un `.vox` de fuente y un `.json` con tamaño del voxel y mínimo espacial original. La correspondencia es VOX X,Y,Z → Unity X,Z,Y. La fuente conserva más detalle que un LOD reducido. El agua y las partículas se generan como geometría/efectos de Unity y no tienen fuente VOX independiente.

Los diseños están en `EnchantedGroveShapes.cs`; `EnchantedGroveVoxels.cs` elimina caras internas y fusiona caras coplanares del mismo color. `EnchantedGroveKit.cs` genera prefabs, fuentes y escena con el menú **Mismo > World > Enchanted Grove > Generar kit y escena de muestra**. La regeneración actualiza intencionalmente los assets de este kit conservando sus GUID. Antes de modificar una pieza manualmente, crear una variante o conservar la edición fuera de estas rutas: el generador sobrescribe sus propias salidas y no reimporta ediciones del `.vox`.

## Verificación

Se ejecutó la generación en una copia independiente de Unity 6000.6.0f1. Se verifican orientación y área de caras, límites de triángulos, referencias de malla/material y compatibilidad de todos los LOD de vegetación con el viento. Las capturas del diorama y las tres planchas de piezas fueron revisadas visualmente. La prueba `EnchantedGrovePlayChecks.RunBatch` abre la escena guardada y comprueba viento y emisión de luciérnagas en Play Mode; el resultado está en `play-mode.txt`.

ProjectOrganizationChecks.Run sigue reportando problemas anteriores en DoubleL, texturas de Bestiary bajo FBX y otros assets ajenos. El informe completo queda en `organization.txt`. La integración procedural posterior sí se verificó con una build Windows; sus resultados y pruebas están documentados en WorldIntegration.md y output/grove-world/. Las mediciones de FPS quedan pendientes.
