# Ajuste de proporciones — 21/09/2026

- Habitantes: escala global configurable `residentScale = 1.3` en `Assets/Data/Quests/VillageNpcSettings.asset`. Los prefabs base de 1,75 unidades aparecen con una altura de 2,275; también aumentan su colisión y la posición de los marcadores.
- Pueblo: `villageSizeMultiplier = 2.6` en `Assets/Data/World/WorldContentCatalog.asset`, antes 2. Casas, calles y murallas aumentan un 30%. Se conserva el ajuste de cimientos y se amplía el terreno preparado mediante el sistema existente.
- Elena se ubica en `(-4, 0, 2)` local, en un sector conectado con la entrada. La comprobación exige una ruta completa y línea de visión dentro del alcance de conversación.
- Partidas anteriores: revisión de distribución 2. Si el personaje estaba dentro de un pueblo, se reubica una sola vez en su entrada. Conserva posiciones lejanas e inventario/progreso. La verificación utilizó datos aislados, sin modificar partidas reales.

## Animaciones del paquete

Se inspeccionaron los 28 FBX originales: 14 exportaciones Unity y 14 alternativas. Todos tienen `importAnimation = true`, cero clips y cero tomas de origen. Los esqueletos se conservan al voxelizar. El movimiento actual de reposo es procedural; el paquete no aporta caminar, correr ni idle. Para desplazarlos harían falta clips compatibles y comportamiento de movimiento.

Evidencia: `ScaleInspection.txt`.

## Validación

`WorldChecks.txt`: PASS. Cinco residentes, tres semillas procedurales, pueblo antiguo y mundo finito; acceso desde la entrada, línea de visión, escala y colisión, migración única de posición, conservación de posiciones lejanas, prevención de duplicados y descarga.

`VillagePreviewChecks.txt`: PASS, 7 comprobaciones en Play Mode. Incluye llegada sin misiones aceptadas, conversación presencial, selección del aldeano, aceptación, marcadores de progreso/entrega y ausencia de errores de presentación del juego. Captura `14-scale-comparison.png`: jugador y Mara a la misma profundidad respecto a la cámara, con las casas ampliadas de fondo.

No se modificó la inclusión de contenido ni se generó una build nueva para este ajuste. Unity emitió su excepción conocida del indexador `UnityEditor.Search.SearchInit` al iniciar; quedó separada de los errores del juego.

Los ajustes se aplican al volver a cargar el mundo.
