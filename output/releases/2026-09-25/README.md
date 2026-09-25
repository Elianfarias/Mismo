# Build de Windows — 25/09/2026

Ejecutable: `Builds/2026-09-25/Mismo.exe`. Build Windows x64 con Unity 6000.6.0f1, sin Development Build. Arranca en MainMenu y carga el continente real mediante Nueva partida o Continuar.

Incluye la introducción en la cueva larga de cristal, el claro con Liria, el tutorial de interfaz y combate, las mejoras de iconos e inventario y el apoyo de los pies de los NPC.

## Verificación

- `build-summary.txt`: Succeeded, 0 errores, 499 advertencias, 760.477.335 bytes.
- `build-messages.txt`: detalle de advertencias de Unity, principalmente variantes de shaders de paquetes y recomendaciones de lectura/escritura o precálculo de colisiones de mallas. No se ocultaron advertencias ni se desactivaron las comprobaciones.
- `organization.txt`: ProjectOrganizationChecks.Run aprobado; 30 assets trasladados mediante AssetDatabase.MoveAsset, mismos GUID y dependencias preservadas para los 8 assets afectados.
- `asset-moves.json` e `importer-remaps.txt`: manifiesto de movimientos y modelos con referencias de texturas fijadas antes del traslado. Los archivos de arte y sus .meta trasladados conservaron sus bytes.
- `smoke-checks.txt`: prueba sobre el ejecutable final aprobada. Carga del menú, nueva partida en la cueva, Liria, pueblo, cinco NPC con NpcGrounding, inventario, guardado aislado y 1.034 materiales con shaders soportados. Los lugares se visitaron mediante teletransporte de prueba; no es una partida completa del tutorial.
- `Menu.png`, `World.png`, `Grove.png`, `Village.png`: capturas del ejecutable.

La compilación se realizó en una copia aislada del proyecto. Los perfiles de prueba están fuera de los guardados del jugador. Los logs completos y los binarios quedan en Builds, fuera del repositorio; las pruebas y este informe sí se versionan.

Para compartir el juego, enviar `Builds/Mismo-Windows-2026-09-25.zip`. Extraerlo completo y ejecutar Mismo.exe conservando sus carpetas y DLL junto a él.
