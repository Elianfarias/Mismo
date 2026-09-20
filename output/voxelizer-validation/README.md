# Validación del voxelizador

Unity 6000.6.0f1, 19 de septiembre de 2026.

## Causa y corrección

El dragón tiene un SkinnedMeshRenderer con escala 0.01. La llamada anterior a BakeMesh sin compensación de escala, seguida por la transformación del MeshCollider, reducía la superficie otra vez: aproximadamente 0.16 unidades frente a un volumen del renderer de aproximadamente 16 unidades. La cuadrícula se dimensionaba con ese volumen grande, dejando solo unos pocos bloques ocupados.

El muestreo nuevo usa BakeMesh con compensación de escala y transforma los vértices una sola vez. Calcula límites desde los triángulos de la pose y rasteriza directamente sus intersecciones con las celdas. Ya no depende de colliders ni de la capa 31. Material y UV se obtienen del triángulo más cercano al centro de cada voxel, respetando el submesh.

## Verificación

Compilación correcta del ensamblado Mismo.Gameplay.Player.Editor con las referencias del proyecto. Las advertencias restantes pertenecen a otros scripts existentes.

Pruebas ejecutadas en un proyecto Unity aislado:

- Superficie de un cubo cerrado, relleno interior y eliminación de caras internas.
- Independencia de colliders grandes y de la capa de física.
- Traslación lejana, rotación, escala muy pequeña y escala no uniforme.
- Mallas sin Read/Write, raíz inactiva y variantes hijas ocultas.
- Triángulos sin espesor y de ambas orientaciones, piezas separadas, materiales por submesh y UV interpolados.
- SkinnedMeshRenderer con límites inflados, huesos externos a la selección y escala aplicada una sola vez.
- Dragón real: tamaño contrastado con una deformación calculada independientemente desde pesos, huesos y bindposes.
- Exportación desde WeaponVoxelizerWindow: prefab, mesh, collider y tamaño final.
- Render de las resoluciones 32, 64 y 128; revisión visual de la salida a 128.

| Resolución máxima | Cuadrícula | Voxeles con relleno | Vértices visibles |
| --- | --- | --- | --- |
| 32 | 17 × 12 × 32 | 803 | 5.992 |
| 64 | 34 × 23 × 64 | 3.568 | 20.400 |
| 128 | 68 × 46 × 128 | 18.942 | 72.824 |

Prefab de ejemplo: `Assets/Data/Models/Voxelized/DragonUsurper_Voxel_128.prefab`. Conserva el material RedHP original; su slot de respaldo utiliza el material VoxelModel_Voxel existente del proyecto. Las imágenes usan iluminación y material Standard de prueba con la misma textura, por lo que la iluminación puede variar dentro del proyecto URP.

Para repetir las regresiones: **Mismo → Modelos → Verificar voxelizador**. La conversión sigue generando una pose estática; no transfiere rig ni animaciones. El relleno ocupa cavidades cerradas por la superficie voxelizada y no repara agujeros grandes en una malla abierta.
