# Primera mejora de fluidez — 2026-09-09

- Mezcla continua Idle/Walk/Run en VoxelLocomotion; compatible con reproducción de acciones por Playables.
- Cadencia suavizada en ambas rutas de animación y selección de carrera según velocidad física con histéresis.
- Inclinación por aceleración/frenada y giro; absorción visual de aterrizaje mientras corre, sin alterar el collider.
- Los controladores de armas sin LocomotionSpeed mantienen su funcionamiento anterior.

Validación: compilación del assembly del jugador sin errores. Unity 6000.3.11f1, proyecto aislado .validation/LocomotionFluidity: PASS al cargar árbol y clips, recorrer velocidades y reproducir salto/caída/aterrizaje/ataque/dash y regreso. No sustituye evaluación visual en la escena final.

Quaternius: pack Standard descargado (14.541.205 bytes) y extraído en ArtSource/Quaternius. Publicación del autor: https://opengameart.org/content/universal-animation-library . Licencia incluida en el ZIP. Esta copia puede ser anterior a las actualizaciones de itch.io; no se presenta como la edición más reciente. Los clips descargados aún no se asignaron al personaje.

La prueba de Unity indica Animator.isHuman=False en el modelo guardado. Un esqueleto con forma humana puede estar importado como Generic: hace falta validar el mapeo Humanoid y las animaciones de combate actuales antes de cambiar esa configuración. Se conservó el Avatar existente.

Actualización: los clips de Quaternius ya fueron adaptados y asignados. Ver Docs/QuaterniusAnimationIntegration.md.
