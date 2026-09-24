# Viento y respuesta de la vegetación

El mundo aplica movimiento a los árboles, hierba, flores y arbustos colocados mediante `WorldContentCatalog` y sus perfiles por bioma. No requiere modelos, texturas ni paquetes nuevos. El shader propio se encuentra en `Assets/Art/Shaders/VegetationMotion.shader`.

## Ajustar

En `Assets/Data/World/WorldContentCatalog.asset`, abrir **Vegetation Motion**:

- **Enabled**: activa o desactiva todo el movimiento.
- **Wind Strength**: intensidad general; 1 es el valor inicial suave.
- **Gust Strength**: variación de las ráfagas que atraviesan el paisaje.
- **Direction Degrees / Wind Speed**: dirección y velocidad del movimiento.
- **Player Interaction**: respuesta de vegetación baja al acercarse el jugador.
- **Interaction Radius / Strength**: radio en metros y desplazamiento máximo; las plantas pequeñas limitan además su desplazamiento según su altura.
- **Recovery Seconds**: duración de la estela que permite recuperar la posición gradualmente.
- **Shader**: referencia necesaria para cargar e incluir el shader en la build. Conservar la asignación existente.

Cada entrada de naturaleza, en el catálogo o en el perfil del bioma, tiene **Disable Vegetation Motion** para excluir modelos concretos. **Decorative Only** conserva su significado: impide recolectar. Ambos controles son independientes. Los cambios de categorías/exclusiones se aplican a nuevas instancias; salir y volver a entrar a la partida permite comparar toda la zona.

Los árboles conservan inmóvil el 58 % inferior de la malla y oscilan suavemente arriba; no se apartan al tocar el tronco. Hierba, flores y arbustos mantienen fija la base y responden al jugador. Rocas, troncos caídos y construcciones permanecen rígidos. El movimiento es visual: no mueve colliders, navegación ni puntos de recolección.

## Integración y alcance

`ExplorationChunks` inicializa un controlador global. `GatheringDistribution.PlaceAsset` identifica la categoría al colocar contenido. `GatheringNode` vuelve a aplicar el material cuando reaparece su visual. Se incluyen las sustituciones por catálogo de árboles del centro antiguo; la vegetación colocada manualmente fuera de este flujo no se modifica automáticamente.

Las mallas y materiales originales se conservan. Las variantes de material se comparten por material de origen durante la sesión y se destruyen al cerrar el mundo. No hay Update por planta ni colliders nuevos. El shader calcula ráfagas por posición y una estela limitada a ocho posiciones del jugador; descarta influencia entre alturas distintas y limpia la estela al teletransportarse.

El shader está destinado al pipeline URP del proyecto. Admite materiales opacos de URP Lit, Simple Lit, Standard y Mismo/Voxel Landscape, conservando textura base, tinte y recorte alfa. No reproduce mapas de normales, metalicidad, emisión ni efectos especiales de otros shaders: usar la exclusión por entrada para vegetación con esos requisitos. Los materiales transparentes o shaders ajenos no se sustituyen. Usa el mismo desplazamiento en color, sombras, profundidad y normales; conserva normales de caras para la apariencia voxel. No incorpora un pase de vectores de movimiento: si se habilita TAA/motion blur, revisar sus artefactos antes de usarlo.

La GPU realiza trabajo adicional en los vértices. Compartir materiales no garantiza un coste nulo: medir en la plataforma objetivo con su densidad real. Las bounds del renderer se amplían solo en runtime para evitar desapariciones durante la oscilación.

## Verificación

`Mismo > World > Verificar viento y vegetacion` usa una escena de preview aislada y los prefabs reales del catálogo. Comprueba referencias, exclusiones, materiales originales, reutilización al recargar instancias, comportamiento de árboles, envío de posición y limpieza por teletransporte. Renderiza comparaciones de quietud, viento, interacción y recuperación en `output/vegetation-motion` y ejecuta `ProjectOrganizationChecks.Run`.

`VegetationMotionChecks.RunBatch` permite ejecutar esa verificación en una copia aislada con dispositivo gráfico. `VegetationMotionChecks.Build` genera una build de desarrollo de Windows con las escenas habilitadas; usar la copia de validación si el proyecto principal está abierto.

### Resultado del 23/09/2026

Unity 6000.6.0f1, copia aislada: 21 comprobaciones de vegetación aprobadas. Se inspeccionaron los renders del árbol y de la hierba; las comparaciones de píxeles comprobaron movimiento por viento, respuesta al jugador y recuperación. C# y shader compilados, incluidos los pases del shader para DirectX 11 y 12 durante la build.

Unity generó la build de Windows con resultado `Succeeded`, pero registró **1 error** del chequeo global de organización. No es una validación global limpia: ya existen assets fuera de categoría en `Assets/DoubleL`, texturas dentro de FBX de Bestiary y un prefab de arma dentro de FBX. No se reorganizaron esos assets. El detalle queda en `output/vegetation-motion/organization.txt`, el resultado de build en `build.txt` y las pruebas en `checks.txt`. La build de desarrollo permanece en `.validation/Organization/output/vegetation-motion/build`.

Pendiente de evaluación manual: intensidad vista desde la cámara habitual y rendimiento con la densidad real de cada bioma. No se realizó una medición de FPS ni una sesión jugable del ejecutable generado.
