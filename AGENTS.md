# Convenciones del proyecto Mismo

Antes de añadir, importar, generar o mover assets, leer `Docs/Buenas-practicas-de-assets.md`.

Antes de crear o integrar un enemigo voxelizado, leer `Docs/Crear-enemigos-voxelizados.md`: describe el flujo del goblin, el taller de ataques, la compatibilidad Humanoid y el registro por bioma en WorldContentCatalog.

- Todo el arte y audio pertenece a `Assets/Art`, separado en `FBX`, `Models`, `Animations`, `Meshes`, `Prefabs`, `Materials`, `Textures`, `UI`, `Sprites`, `Audio`, `Fonts`, `Shaders` y `Source` según el tipo.
- Los ScriptableObjects pertenecen a `Assets/Data`, agrupados por sistema. Una malla `.asset` pertenece a `Art/Meshes`, no a `Data`.
- Código en `Assets/Scripts`; herramientas dentro de subcarpetas `Editor`; escenas en `Assets/Scenes`.
- No crear carpetas `Resources` ni usar `Resources.Load`, `Resources.LoadAll` o `Resources.LoadAsync`. Usar referencias serializadas; para búsquedas globales, `Mismo.Core.ProjectAssets` y el catálogo de `Data/System`.
- No volver a crear carpetas sueltas de proveedores en la raíz de `Assets` ni usar `Data` como destino genérico de prefabs, materiales o animaciones.
- Mover assets mediante Unity/`AssetDatabase.MoveAsset` conservando los GUID y archivos `.meta`. Actualizar también las rutas de las herramientas de editor.
- Antes de borrar originales, comprobar dependencias. Los voxelizados no necesariamente contienen copias independientes de sus texturas y materiales.
- Al cambiar organización o cargas de contenido, ejecutar `ProjectOrganizationChecks.Run` y las pruebas pertinentes; verificar también una build cuando se cambie qué contenido se incluye en el juego.
- Respetar los cambios existentes del usuario. No regenerar escenas, materiales o prefabs ajenos para resolver una reorganización.

Para feedback de combate y actualizaciones de Feel, leer `Docs/Feel-en-combate.md`. El canal 7401 usa el receptor de la cámara orbital; no instalar otro shaker automático ni duplicar el hit stop existente.
