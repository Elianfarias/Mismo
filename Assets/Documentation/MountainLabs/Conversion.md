# Árboles MountainLabs: muestras optimizadas

Se convirtieron Live Tree (8 m) y Fluffy Pine (6 m) desde las celdas y transformaciones de MagicaVoxel. No se sustituyeron entradas de WorldContentCatalog ni perfiles de bioma.

| Prefab | Cerca (LOD0) | Media distancia (LOD1) | Lejos (LOD2) |
| --- | ---: | ---: | ---: |
| MountainLabs_LiveTree | 21.880 | 3.514 | 828 |
| MountainLabs_FluffyPine | 16.648 | 4.620 | 1.440 |

Las cifras son triángulos por árbol para un único LOD activo. No son una medición de FPS ni incluyen pasadas adicionales de sombras. La conversión exacta del árbol daba 5.658.566 triángulos y fue reemplazada: esa malla no se entrega.

## Archivos

- Prefabs: `Assets/Art/Prefabs/World/Nature/MountainLabs/`.
- Mallas: `Assets/Art/Meshes/Nature/MountainLabs/`.
- Materiales y texturas: las carpetas `Nature/MountainLabs` en `Art/Materials` y `Art/Textures`.
- Escena de comparación: `Assets/Scenes/Previews/MountainLabsTrees.unity`; cápsula de referencia de 1,8 m.
- Fuente pequeña y regenerable: `Assets/Art/Source/Nature/MountainLabs/VoxelTreePack/`.
- Herramienta: menú `Mismo > World > MountainLabs > Convertir arbol y pino de muestra`.

## Decisiones

Se eliminan caras internas y se fusionan caras coplanares del mismo color. Además, se reduce la densidad de la cuadrícula original (factor 6 en Live Tree, factor 2 en Fluffy Pine) para quedar por debajo de 25.000 triángulos. Los siguientes LOD usan el doble y cuádruple de ese factor. Cada celda reducida toma el color mayoritario de las celdas ocupadas: conserva colores de la paleta, pero pierde detalle fino y puede engrosar ramas o cerrar huecos. Los originales `.vox` permanecen intactos y no son las mallas usadas por el juego.

LODGroup cambia por altura relativa en pantalla: 25 %, 8 %, 1,5 %; después oculta el árbol. Comparte un material entre LODs. El collider es una cápsula simple en el tronco: la copa no bloquea navegación. El pivote queda al nivel del suelo y centrado en la base del tronco.

VegetationMotionBinding configura los tres LOD para el viento cuando existe VegetationMotionWorld. El tronco inferior permanece fijo y no se dobla por proximidad del jugador. La escena de comparación es estática; la validación renderiza aparte la integración con el sistema de viento. Para usar las muestras en el mundo, asignar los prefabs en las entradas de tipo Tree del catálogo y evaluar densidad, distancias y sombras en el equipo objetivo.

## Validación

Conversión ejecutada en una copia independiente de Unity 6000.6.0f1. Comprobadas caras internas, área de caras fusionadas, orientación de triángulos, rotaciones VOX, alturas, pivotes, materiales y shader de viento. Capturas e informes en `output/mountainlabs-trees`.

Se ejecutó ProjectOrganizationChecks.Run: continúa fallando por problemas anteriores en DoubleL, texturas de Bestiary bajo FBX y otros assets ajenos. El informe completo queda en `organization.txt`. No se cambiaron las escenas incluidas en build ni las cargas de contenido; no se midió todavía un bosque en el juego.

Formato de referencia: https://github.com/ephtracy/voxel-model
Condiciones del proveedor: VoxelTreePack.md.
