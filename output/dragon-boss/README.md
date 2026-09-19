# Dragones voxelizados con rig y animaciones

Generados con Unity 6000.6.0f1 mediante **la misma herramienta** `WeaponVoxelizerWindow.ExportModel` que utiliza la ventana de voxelización.

## Actualización al paquete completo FourEvilDragonsPBR

Nightmare usa ahora el material **BluePBR** del paquete completo: albedo azul, metallic/smoothness y oclusión. Se actualizó `Assets/Art/Materials/Nightmare_Animated_BlueHP.mat` manteniendo su GUID, por lo que los prefabs existentes reciben la corrección sin reasignarlos. Se usan las normales planas de las caras voxelizadas; el normal map del modelo original requiere una base tangente que estos cubos no tienen.

El FBX y los 16 FBX de animación Nightmare son idénticos byte por byte al paquete anterior. Una nueva exportación a resolución 128 produjo los mismos vértices, UV y pesos. Se conservaron la malla, los huesos, los clips y el controller existentes. Firyx y SoulEater también se conservaron: los prefabs y controllers del paquete completo referencian los mismos modelos y sus 18/17 animaciones ya importados. El detalle y hashes están en `package-comparison.json`.

Para repetir únicamente la corrección: **Mismo → Modelos → Actualizar material Nightmare desde paquete completo**. El generador de dragones usa ahora el FBX, BluePBR y la carpeta `Animations/DragonNightMare` del paquete completo para futuras exportaciones de Nightmare. No necesita la carpeta antigua eliminada.

Para otra variante, seleccionar su prefab dentro de `Assets/Art/Prefabs/Monsters/FourEvilDragonsPBR/DragonNightmare` en la ventana del voxelizador. La búsqueda automática elige su familia de animaciones; ante una carpeta con varias familias sin coincidencia, usa los clips del Animator o solicita una subcarpeta específica, evitando mezclar rigs. El dragón adicional TerrorBringer del paquete no se ha generado en esta actualización.

Validación de esta actualización: nueva exportación de Nightmare mediante la herramienta, coincidencia de geometría/UV/pesos, 51 clips existentes, referencias PBR y detección de la familia de animaciones. Vista previa Nightmare actualizada. Las pruebas de combate indicadas más abajo corresponden a la entrega anterior; esta actualización no cambia su comportamiento.

## Uso para futuros modelos

1. Abrir **Mismo → Modelos → Voxelizar modelo**.
2. Seleccionar la raíz completa del modelo original, incluyendo los huesos.
3. Activar **Conservar rig y animaciones**.
4. Dejar vacía **Carpeta de animaciones** para buscar `Animation` o `Animations` junto al modelo, o arrastrar la carpeta que contiene sus clips.
5. Elegir resolución (64 para comenzar; 128 para más detalle) y generar.

El resultado incluye `SkinnedMeshRenderer`, huesos, pesos, clips `.anim`, Animator Controller y un componente `VoxelRigInstance` con la biblioteca de estados. Se conserva la pose y el tamaño del modelo, se compensan las escalas de importación y los vértices coincidentes comparten pesos para evitar separaciones al abrir las alas. Los bloques se deforman con el rig: no son cubos rígidos independientes. El collider animado es una cápsula de cuerpo, no la malla congelada de la pose original.

La herramienta mantiene el modo estático para armas. Recupera texturas faltantes cuando existe una imagen del mismo nombre que el material en la carpeta `Texture`/`Textures`, y adapta materiales Standard a URP cuando está disponible. No modifica los materiales originales. Los clips deben usar el mismo rig con curvas Transform/Generic; un Humanoid requiere convertir/adaptar sus clips previamente. Seleccionar solamente una malla con huesos fuera de su jerarquía produce un mensaje para seleccionar la raíz completa.

## Entregables

| Modelo | Prefab | Animaciones | Huesos | Vértices a resolución 128 |
| --- | --- | --- | --- | --- |
| Firyx | `Assets/Art/Prefabs/Voxelized/Firyx_Animated.prefab` | 18 | 119 | 238.368 |
| Nightmare | `Assets/Art/Prefabs/Voxelized/Nightmare_Animated.prefab` | 16 | 70 | 251.976 |
| SoulEater | `Assets/Art/Prefabs/Voxelized/SoulEater_Animated.prefab` | 17 | 57 | 296.280 |

Las carpetas `_Animations` en `Assets/Art/Animations/Voxelized` contienen todos sus clips y estados. Los tres modelos tienen vistas previas GIF en esta carpeta. La iluminación de las vistas previas utiliza un material Standard de prueba con las texturas exportadas; el proyecto usa los materiales URP.

## Bosses Firyx

En `Assets/Art/Prefabs/DragonBosses`:

- `Firyx_Red.prefab`: Brasa, más rápido y con aliento frecuente.
- `Firyx_Blue.prefab`: Llama Azul, vuelo más frecuente y mayor altura.
- `Firyx_Green.prefab`: Llama Esmeralda, más salud y defensa frontal.
- `Firyx_Purple.prefab`: Llama Violeta, menor recuperación y más cadenas de garra → mordida.

Cada prefab usa sus propios colores y `DragonBossSettings`, y comparte la malla y biblioteca de animaciones Firyx. Incluye detección del jugador, rugido, persecución, garra, mordida, aliento de partículas cúbicas, vuelo, aterrizaje, defensa, parry/postura, fase de furia, regreso a su zona, reinicio y muerte. El sistema de desintegración espera a que termine el clip de muerte. Se integra con Health/DamageReceiver, música de boss, barra de vida y recompensas de boss existentes.

Arrastrar el prefab de boss a un suelo con collider. Busca automáticamente un `PlayerController`; también admite `SetTarget`. Utiliza NavMesh si encuentra uno, o desplazamiento directo con comprobación de obstáculos y suelo cuando no hay NavMesh. Para navegación alrededor de obstáculos complejos se necesita un NavMesh apropiado al tamaño del dragón. Los dos modelos nuevos se entregan animados; los cuatro perfiles de boss se aplican a las variantes Firyx solicitadas.

Los valores de daño, salud, velocidades, cooldowns, vuelo y probabilidades se ajustan en los assets `DragonBossSettings`. El aliento es un VFX cúbico básico con daño en cono, no una simulación volumétrica de fuego.

## Validación

- Compilación de `Mismo.Gameplay.Player`, `Mismo.Gameplay.Enemies` y `Mismo.Gameplay.Player.Editor` con las referencias del proyecto.
- 51/51 clips muestreados en tres momentos: deformación real, escala, pesos normalizados, huesos válidos y continuidad de las esquinas.
- Revisión visual de secuencias de vuelo/carrera de los tres modelos; corregidas las referencias rotas a texturas de Nightmare y SoulEater durante la exportación.
- Play Mode de los cuatro bosses: daño de aliento y melee, defensa frontal, vuelo y aterrizaje, parry, furia, regreso, obstrucción por paredes, muerte única, animación de muerte visible y reutilización del prefab.
- Regresión del voxelizador estático: superficies finas, materiales/UV, escala, relleno, colliders ajenos y mallas sin Read/Write.

Repetir la validación de rigs desde **Mismo → Modelos → Verificar rigs voxelizados de dragón**. Las pruebas de combate se ejecutan en batch en un proyecto aislado (`DragonBossPlayChecks.Begin`), para no reemplazar la escena de trabajo.

El editor aislado registró una excepción de su índice de búsqueda (`UnityEditor.Search.SearchDatabase`) al entrar en Play Mode. Las comprobaciones de rigs y combate finalizaron correctamente; no corresponde al código de los dragones.

## Organización del proyecto

Las configuraciones de boss se guardan en `Assets/Data/Enemies/DragonBosses`. Los originales del paquete completo están repartidos en `Assets/Art/{FBX,Animations,Materials,Textures,Prefabs}/Monsters/FourEvilDragonsPBR`. No borrar estas dependencias. Ver `Docs/Buenas-practicas-de-assets.md` para las reglas de guardado.
