# Presentación del combate

Abrir `Assets/Scenes/VoxelRegion_7319.unity` y entrar en Play. No hace falta regenerar la región.

- HUD: vida y stamina numéricas, teclas y estados de combo, Q, E, R y C; cooldowns reales y aviso de stamina insuficiente para estocada y giro.
- Minimap local de 90 m orientado al norte, con dirección del jugador. Renderiza a 256 px cinco veces por segundo. No es un mapa global ni un sistema de descubrimiento.
- Espada: corrige la escala heredada de la mano importada, diferencia cortes alternados y golpe descendente, y sincroniza el barrido con las ventanas de impacto. Estela solamente durante acciones activas; desaparece al cancelar.
- Enemigos: barras en pantalla por proximidad y visibilidad, identificación del Elite, barra destacada de boss y avisos de preparación. Se ocultan los antiguos billboards. Se agregan cinturón, hombreras, pupilas y cejas a los prefabs existentes; los tintes conservan el detalle de los materiales.

El HUD lee los componentes existentes. Las animaciones siguen siendo procedurales; no se incorporaron clips de captura de movimiento ni un rig nuevo. No cambiaron vida, daño, alcance ni costes de habilidades.

`CombatPresentationUpgrade.Apply` aplica detalles de enemigos de forma idempotente. `InitialRegionChecks.RunPresentationBatch` verifica HUD, render texture del minimapa, escala del arma, estela durante impacto y cancelación, y repite las comprobaciones de la región. Esas pruebas y la compilación Windows finalizaron correctamente en una copia temporal.

Las capturas de pantalla en segundo plano resultaron negras y no se consideran validación visual. Queda comprobar en juego el encuadre, las poses y la legibilidad a distintas resoluciones. El helper `CombatPresentationCapture` funciona solamente en builds de desarrollo con el argumento explícito `-presentation-capture`; no se activa en partidas normales.

## Corrección de escala y recursos Town (2026-09-07)
- La espada se separa de la mano importada (escala 100), mantiene escala mundial 1 y aplica poses en metros siguiendo la orientación del motor. Se verificaron capturas reales en reposo y durante impacto.
- EnemyNameplate oculta las referencias serializadas de Status de GoblinPresentation y BossPresentation; elimina la búsqueda incorrecta por nombre Billboard.
- Town contiene diez OBJ con una paleta PNG idéntica ya incluida. TownVoxelPalette conecta esa textura, sin mipmaps ni filtrado bilineal, a todos los modelos mediante sus importadores. No se necesitan imágenes generadas.
- TownMaterialChecks verifica los materiales de los diez modelos y renderiza una vista conjunta. Los modelos quedan listos para colocar; esta corrección no cambia la distribución del pueblo.
- Capturas: Validation/sword-rest.png, Validation/sword-impact.png, Validation/town-materials.png.

## Integración procedural de Town (2026-09-07)
- Las nuevas regiones llaman a TownVillageBuilder: cuatro casas importadas sustituyen las casas de bloques, con 26 elementos auxiliares de Town. Los modelos conservan sus materiales y vínculo al OBJ.
- Tamaño uniforme calculado desde la geometría, pivote apoyado en el terreno y colisiones simples para casas y objetos sólidos. Vegetación sin colisión.
- La semilla determina variantes de cajas, tamaños y rotaciones de decoración. Los cuatro lotes y el corredor central permanecen controlados para preservar navegación y Scope 0. Las casas son exteriores, sin interiores nuevos.
- La escena VoxelRegion_7319 ya contiene el pueblo integrado. Para otras escenas existentes: Mismo > World > Update Town In Current Region; guardar la escena después. Las nuevas regiones lo incorporan automáticamente.
- Validación en copia temporal: cuatro casas y 30 instancias totales, textura asignada, calle central despejada, aproximaciones accesibles, navegación, recompensa, boss y respawn correctos. Vista revisada: Validation/town-integrated.png.
