# Verificación de misiones — 21/09/2026

- Unity 6000.6.0f1, copia aislada `.validation/Organization`.
- `QuestFlowChecks.Run`: **64 comprobaciones aprobadas**. Aceptación, requisitos, materiales previos, consumo al entregar, recompensas únicas, receta aprendida y fabricada, caza posterior al encargo, fuentes de señales únicas, orden de descubrimiento/puzzle, resolución cardinal, restauración del altar resuelto, migración v5 → v6, copia independiente y rechazo de datos duplicados.
- Fallos de escritura inyectados: aceptación, entrega, fabricación, solución de puzzle y seguimiento conservan el estado anterior. La mochila llena conserva materiales, monedas y entrega pendiente.
- Eventos: inicio durante combate y avance mediante señal; entregar premios en combate continúa bloqueado.
- UI: navegación de los ocho sectores y acceso al diario, material visible con contador, textos del NPC, pista y seguimiento. Sin errores de runtime/GUI en la prueba. Unity produjo una excepción de su indexador de búsqueda de editor al iniciar; está separada de los errores de juego y conservada en el log.
- `ProjectOrganizationChecks.Run`: aprobado durante preparación y build.
- Build Windows: **aprobada**, 728319202 bytes. Ruta: `.validation/Organization/output/quest-build/Mismo.exe`.
- Ejecutable Windows con `-mismo-quest-check`: **aprobado**. Las siete misiones, cinco perfiles de NPC, íconos de materiales, ocho sectores del radial, recetas premiadas y teclas **J/T** cargan desde la build. Ver `PlayerCheck.txt`.
- `git diff --check`: sin errores.

Las capturas corresponden a una escena de prueba aislada. La captura del HUD incluye el aviso esperado de una escritura fallida inyectada. Los materiales y recompensas iniciales son ejemplos ajustables. El altar no se colocó en las escenas principales, y asaltos/anomalías esperan las señales de sus futuros controladores. No se añadieron modelos de NPCs ni tiendas.

Configuración y uso: `Assets/Documentation/QuestSystem.md`.
