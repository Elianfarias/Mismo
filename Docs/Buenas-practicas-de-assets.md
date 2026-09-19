# Organización y buenas prácticas de assets

Esta es la estructura de trabajo del proyecto Mismo. Se aplica tanto a archivos importados manualmente como a los creados por scripts, ventanas de editor y asistentes.

## Dónde guardar cada archivo

| Carpeta | Contenido | Ejemplo |
| --- | --- | --- |
| `Assets/Art/FBX` | Modelos FBX originales y sus rigs | `Monsters/Dragon Firyx/...` |
| `Assets/Art/Models` | Modelos OBJ, GLB y DAE; MTL asociados al modelo | `Town/Medieval Assets/...` |
| `Assets/Art/Animations` | Clips `.anim`, FBX de animaciones, controllers, overrides y AvatarMasks | `Voxelized/Nightmare_Animated_Animations` |
| `Assets/Art/Meshes` | Mallas generadas guardadas como `.asset` | `Voxelized/Nightmare_Animated.asset` |
| `Assets/Art/Prefabs` | Prefabs de personajes, bosses, objetos y escenario | `DragonBosses/Firyx_Red.prefab` |
| `Assets/Art/Materials` | Materiales `.mat` | `Monsters/FourEvilDragonsPBR/DragonNightmare` |
| `Assets/Art/Textures` | Texturas de modelos, mapas PBR y paletas | `Monsters/FourEvilDragonsPBR/DragonNightmare/Blue` |
| `Assets/Art/UI` | Imágenes y recursos visuales de interfaz | `Game/HUD/VitalsPaintedFrames.png` |
| `Assets/Art/Sprites` | Sprites que no pertenezcan a la interfaz | Una animación 2D o un sprite del mundo |
| `Assets/Art/Audio` | Música, efectos y AudioMixers | `Music/Exploration.wav` |
| `Assets/Art/Fonts` | Fuentes y sus licencias | `Cagliostro-Regular.ttf` |
| `Assets/Art/Shaders` | Shaders y archivos asociados | `CombatParticles.shader` |
| `Assets/Art/Source` | Fuentes auxiliares de arte y paquetes archivados | Archivos `.vox` o archivos comprimidos originales |
| `Assets/Data` | ScriptableObjects y datos, agrupados por sistema | `Enemies/DragonBosses/Firyx_Red.asset` |
| `Assets/Settings` | Configuración importada que no sea un ScriptableObject | `Input/InputSystem_Actions.inputactions` |
| `Assets/Scripts` | Código, asmdefs y subcarpetas `Editor` | `Core/RuntimeAssetCatalog.cs` |
| `Assets/Scenes` | Escenas `.unity` | `MainMenu.unity` |
| `Assets/Documentation` | Documentación y licencias de paquetes importados | Instrucciones del proveedor |
| `Docs` | Documentación del proyecto | Esta guía |

**La extensión `.asset` no determina la carpeta.** Un `DragonBossSettings` es un ScriptableObject y va en `Data`; una `Mesh` también puede ser `.asset`, pero va en `Art/Meshes`. Un AudioMixer es audio, y un material `.mat` pertenece a `Art/Materials`.

Los archivos de fuente `.ttf`/`.otf` van en `Art/Fonts`; un `TMP_FontAsset` generado es un ScriptableObject y va en `Data/UI/Fonts`, junto con sus subassets internos.

Dentro de cada categoría, agrupar por sistema, familia o proveedor. Mantener nombres estables y descriptivos; evitar `New Material`, `New Prefab`, `cosas`, `varios` o carpetas que mezclen materiales, mallas y configuraciones. No crear carpetas sueltas en la raíz de `Assets` al importar paquetes.

## Cargar assets sin Resources

No crear ninguna carpeta llamada `Resources` ni volver a introducir `Resources.Load`, `LoadAll` o `LoadAsync` para contenido del proyecto.

El juego usa `Assets/Data/System/RuntimeAssetCatalog.asset`, registrado en **Player Settings → Preloaded Assets**. El catálogo mantiene referencias serializadas a los assets; por eso su funcionamiento en una build no depende de `AssetDatabase` ni de rutas físicas del editor. Esta forma de registrar ScriptableObjects está soportada por [Unity: SetPreloadedAssets](https://docs.unity.com/en-us/engine/6000.7/script-reference/unityeditor/playersettings/setpreloadedassets).

```csharp
using Mismo.Core;

var catalog = ProjectAssets.Load<ItemCatalog>("ItemCatalog");
var materials = ProjectAssets.LoadAll<MaterialDefinition>("Materials");
```

Las claves son identificadores lógicos, no rutas de carpetas. Por ejemplo, `Audio/Music/Exploration` puede apuntar a `Assets/Art/Audio/Music/Exploration.wav`.

Para un asset que solo usa un prefab o componente, preferir un campo serializado y asignarlo desde el Inspector. Añadirlo al catálogo únicamente cuando un sistema deba buscarlo globalmente. No registrar toda la carpeta `Art`: el catálogo y sus dependencias se precargan, por lo que incluir contenido innecesario aumenta el coste de inicio y la memoria.

Para añadir una carga global:

1. Guardar el archivo en su categoría correcta.
2. Si pertenece a una carpeta de descubrimiento del catálogo, ejecutar **Mismo → Proyecto → Actualizar catálogo de assets**. Las carpetas se examinan directamente; añadir otra entrada de descubrimiento para una nueva subcarpeta.
3. Para una clave especial, añadir una entrada al catálogo con una clave única y arrastrar las referencias necesarias a `assets`.
4. Ejecutar **Mismo → Proyecto → Verificar organización y referencias** y comprobar el comportamiento en Play Mode y en una build cuando se cambien cargas de contenido.

La build actualiza el catálogo y verifica la organización automáticamente. No ignorar una referencia nula o una clave duplicada. Conservar el catálogo en Preloaded Assets.

## Mover, renombrar e importar

- Mover o renombrar desde el panel Project de Unity o con `AssetDatabase.MoveAsset`. Conservar los `.meta` y GUID existentes.
- No borrar un asset para volver a crearlo con el mismo nombre: las referencias usan su GUID, no su nombre.
- Antes de quitar un paquete, revisar las dependencias de sus materiales, texturas, modelos, prefabs y animaciones. Un voxelizado puede conservar referencias al paquete original aunque tenga malla propia.
- Mantener las licencias y documentación de terceros. Las rutas internas de OBJ/MTL y otras fuentes pueden necesitar ajustes al separar modelos y texturas.
- Revisar las rutas escritas en scripts de editor. Un traslado por GUID conserva referencias serializadas, pero no corrige automáticamente un texto como `AssetDatabase.LoadAssetAtPath("...")`.
- Un importador o generador nuevo debe crear los directorios de destino antes de guardar y no sobrescribir assets existentes salvo que esa actualización sea intencional.
- Evitar copias con nombres `1`, `2`, `FinalFinal`. Si hace falta una variante, nombrarla por su función o color y compartir mallas, rigs y clips compatibles.

## Paquetes de Unity

Addressables guarda sus datos en `Data/Addressables`. `ProjectPackagePaths` adapta la ruta interna del perfil porque la versión instalada no expone un ajuste público; revisar esa compatibilidad al actualizar el paquete. No editar `Library/PackageCache`.

El paquete de pruebas de rendimiento de Unity puede crear archivos temporales durante una build y retirarlos al terminar. Si Unity se cierra durante la build, revisar esos residuos antes de volver a ejecutar la verificación; no usar esa carpeta temporal para contenido del juego.

## Generadores y dragones

La ventana **Mismo → Modelos → Voxelizar modelo** guarda prefabs en `Art/Prefabs/Voxelized`, mallas en `Art/Meshes/Voxelized`, clips y controllers en `Art/Animations/Voxelized`, y materiales en `Art/Materials`. El modo de armas utiliza las subcarpetas `Weapons/Voxelized` correspondientes.

Las configuraciones de bosses Firyx están en `Data/Enemies/DragonBosses`; sus prefabs, malla de partículas y materiales están en las categorías de `Art` correspondientes.

Los originales del paquete de dragones se distribuyen bajo `Monsters/FourEvilDragonsPBR` en las categorías de `Art`. La antigua carpeta `Assets/FourEvilDragonsPBR` deja de ser necesaria. **Esto no significa que se puedan borrar los archivos reubicados**: algunos son dependencias del resultado y otros permiten volver a generarlo.

Seleccionar la raíz completa del modelo con sus huesos y un material correctamente configurado. Al elegir una carpeta de animaciones, usar la subcarpeta de esa familia; no mezclar clips de rigs distintos. Conservar el original cuando se quiera cambiar resolución o volver a voxelizar.

## Comprobaciones antes de entregar cambios

1. La consola compila sin errores nuevos.
2. **Verificar organización y referencias** termina correctamente.
3. Los prefabs afectados mantienen materiales, texturas, scripts y referencias válidas.
4. Las herramientas modificadas generan nuevos assets en las categorías correctas.
5. Si se cambia carga de contenido, validar una build: que funcione en el editor no demuestra que el archivo esté incluido en el juego.

No modificar escenas abiertas ni cambios ajenos como efecto secundario de una limpieza. Guardar un informe de migración y comprobar la conservación de GUIDs cuando se hagan traslados masivos.
