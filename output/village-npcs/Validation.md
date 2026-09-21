# NPCs del pueblo — 21/09/2026

- 14 modelos únicos de `people_unity` procesados por `WeaponVoxelizerWindow.ExportModel`, resolución 64, rig conservado. Los originales y sus exportaciones alternativas permanecen disponibles.
- Cinco habitantes configurados: Mara (`peasant_4`), Bruno (`peasant_1`), Elena (`peasant_6`), Iria (`rich_citizzens_2`) y Tomás (`city_dwellers_1`). Los primeros tres ofrecen los pedidos actualmente jugables; los demás conversan y están preparados para contenido futuro.
- Los personajes se instancian como hijos del pueblo inicial y mantienen tamaño humano aunque el pueblo se escale. Se incluyen los mundos que conservan el centro antiguo.
- Misiones aceptadas y recompensas conservan el guardado existente. El diario no acepta ni entrega pedidos. No se inicia una misión al entrar al pueblo, hablar o cruzar el trigger de ejemplo del altar.

## Comprobaciones

- `QuestFlowChecks.Run`: **74 comprobaciones aprobadas**, incluyendo diario vacío, conversación sin autoaceptación, distancia, paredes, cambios de marcador, requisitos, recetas, materiales, caza, puzzle, persistencia, fallos de guardado y pago único. Ver `QuestChecks.txt`.
- `VillageNpcChecks.Run`: **aprobado** con semillas 7319, 12 y 654321 y con el centro antiguo. Comprueba cinco habitantes, escala, terreno, caminos completos desde la entrada, prevención de duplicados y limpieza al descargar. Ver `WorldChecks.txt`.
- `QuestFlowChecks.RunVillagePreview`: **7 comprobaciones aprobadas** dentro del pueblo generado. Capturas `10-village-available.png`, `11-village-active.png`, `12-village-ready.png` y `13-village-dialogue.png`: marcadores `!`, `…`, `?`, aviso de conversación separado del cofre y diálogo de entrega. Ver `VillagePreviewChecks.txt`.
- `ProjectOrganizationChecks.Run`: aprobado en la copia aislada con todos los assets de NPCs y dependencias del juego.
- Build Windows final con Unity 6000.6.0f1: **aprobada**, 751197461 bytes. El ejecutable valida cinco prefabs con rigs/materiales, diálogos, catálogo, recetas y teclas J/T. Ver `PlayerCheck.txt`. Se ejecutó sin cargar ni escribir la partida del usuario.

## Organización del proyecto principal

Los traslados del paquete de NPCs se ejecutaron mediante `AssetDatabase.MoveAsset`, verificando que el GUID se conserve. Arte, mallas, animaciones, prefabs, textura, material, fuente `.tx` y licencia quedaron en sus categorías correspondientes. Los NPCs se incluyen por referencias serializadas desde `QuestCatalog.villageNpcs`; no hay cargas Resources nuevas.

La verificación global del proyecto principal detecta **10 imágenes de seis paquetes de Nature** recién importados fuera de `Art/Textures`, `Art/UI` o `Art/Sprites`. Ver `Organization.txt`. Son ajenas al paquete de NPCs y no fueron reorganizadas por esta tarea. El chequeo previo a una build del proyecto principal seguirá señalándolas hasta ordenarlas. Esos paquetes nuevos no forman parte de la copia aislada usada para validar esta implementación.

## Configuración

- `Assets/Data/Quests/VillageNpcSettings.asset`: habitantes, posiciones, orientación, distancia, línea de visión y marcadores.
- `Assets/Data/Quests/NPCs`: saludos, textos sin nuevos pedidos y diálogos de cada habitante.
- `Assets/Art/Prefabs/Quests/Village`: modelos, colisión, pose, movimiento suave y pedidos asignados.
- `Assets/Documentation/QuestSystem.md`: funcionamiento, controles y conexión de puzzles/eventos pendientes.

Los modelos originales no contienen clips; la pose de descanso y el movimiento suave se configuran en los prefabs, conservando el rig para futuras animaciones. El altar, las oleadas de asalto y las anomalías aún requieren colocación/controladores; sus misiones no se ofrecen como pedidos iniciales.
