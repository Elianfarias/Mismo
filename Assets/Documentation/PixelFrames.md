# Marcos pixelados de Mismo

Sprites originales en `Assets/Art/UI/PixelFrames`. Casillas cuadradas y paneles rectangulares con lados rectos, esquinas escalonadas y borde claro entre contornos oscuros.

- `Frame` y `Panel`: marco transparente y panel con fondo; nueve cortes con **9 píxeles por lado**, filtro Point, sin mipmaps ni compresión. Usar dimensiones de al menos 18 píxeles.
- `FrameHover`, `FrameSelected`, `FrameDisabled`: variantes para componentes que necesiten sprites separados.
- `BarFrame`, `HealthFill`, `StaminaFill`, `ManaFill`: piezas para barras. El HUD actual reutiliza `Panel` y dibuja el relleno con el color de cada estadística.
- `Crosshair`, `CrosshairPrecise`: miras de 25 × 25; no aplicar nueve cortes.
- `Surface`, `CooldownOverlay`: fondos planos para tintado y recargas.

`FantasyUI` comparte los marcos entre IMGUI y el menú Canvas. Solo `Frame`, `Panel` y `Crosshair` se registran en `Assets/Data/System/RuntimeAssetCatalog.asset`, mediante referencias explícitas y claves `UI/PixelFrames/...`. Las demás variantes quedan disponibles para edición sin precargarlas en el juego.

Se mantienen los offsets, tamaños de casillas, orientación, opacidades configurables y controles del editor para arrastrar el HUD. La mira y el rayo de apuntado comparten el punto de viewport `(0.5, 0.65)`: centrado horizontalmente y un 15 % de la altura de pantalla por encima del centro, para despejar la silueta del personaje. Las imágenes anteriores permanecen disponibles para no romper dependencias de otros contenidos.

Focus se muestra en el perímetro turquesa de cada habilidad activa con costo mayor que cero: `Clamp01(Focus disponible / costo de la habilidad)`. Empieza arriba y avanza en sentido horario por las esquinas escalonadas; no hay barra global ni objetivo de arrastre independiente. Las habilidades gratuitas y pasivas conservan el marco normal. El enfriamiento sigue dentro de la casilla y la falta de stamina mantiene su aviso. Los valores guardados del antiguo desplazamiento de Focus se conservan por compatibilidad, pero ya no se usan.

Generador: `Assets/Scripts/Editor/GeneratePixelUI.py` (Python y Pillow). Conserva los GUID existentes. `--preview-only` genera solamente `Docs/Previews/PixelFrames.png`.

## Radial de ocho opciones

El radial reutiliza `FantasyUI.Panel` y `FantasyUI.Frame`, los mismos assets de nueve cortes del HUD y el inventario. Las ocho casillas de 88×88 se distribuyen a radio 176 alrededor de un panel central de 128×128. No se dibujan sectores curvos rasterizados. La selección usa el mismo borde tintado en dorado y los iconos existentes, incluyendo Recetas y Misiones.

Mantener B, apuntar y soltar conserva la navegación por dirección. El centro completo cancela, incluidas las esquinas del panel. El nombre seleccionado permanece dentro del panel central debajo de la X. Se conservan opacidad y sonido configurables. No cambia el editor de posiciones del HUD.

Los PNG de sectores antiguos y sus referencias se conservan por compatibilidad con herramientas anteriores, pero `DrawMenu` ya no los dibuja. `GeneratePixelRadial.py` reproduce únicamente ese arte anterior; no define el diseño actual. No se añaden assets ni claves de catálogo para este cambio visual.

Registro y verificación: **Mismo → UI → Registrar marcos pixelados** ejecuta también `ProjectOrganizationChecks.Run`. `PixelFrameIntegration.Build` genera una build de Windows de validación. El parámetro opcional `-mismo-pixel-ui-check <carpeta>` comprueba las referencias y captura menú, HUD, inventario, personaje y habilidades usando una partida aislada.

Verificación del radial compartido (2026-09-21): compilación y ProjectOrganizationChecks correctos; QuestFlowChecks pasó 64 comprobaciones, incluidas las ocho direcciones y la apertura de Misiones, sin errores de ejecución/GUI. Captura revisada en Docs/Previews/RadialImplemented.png.

## Color, degradado y desenfoque de menús

Configurar `Assets/Data/UI/InventoryUIIcons.asset`, sección **Fondo global de menús — cambios en vivo**:

- `menuBackgroundColor`: color del fondo, independiente de textos, iconos y bordes.
- `menuCenterOpacity` y `menuEdgeOpacity`: alfa central y de los bordes.
- `menuEdgeFade`: amplitud de la transición suave hacia el borde.
- `menuBackgroundBlur`: activa/desactiva el desenfoque del escenario al abrir pausa, inventario, radial, recetas, misiones o mapa.
- `menuBlurRadius`: intensidad del desenfoque gaussiano de URP (0,5–1,5).

`FantasyUI.PanelTexture` ahora genera una textura de color y alfa en memoria, compartida y actualizada solo al cambiar estos parámetros. El antiguo PNG `Panel.png` se conserva, pero ya no define el color del fondo compartido. Los marcos siguen usando `Frame.png`. Las opacidades particulares del inventario, HUD y misiones multiplican este alfa; la opacidad del borde decorativo es independiente del desvanecido del fondo.

Pausa y su editor de HUD reutilizan `FantasyUI.Panel`, sin velo negro sobre toda la pantalla. Recetas y misiones usan la misma superficie manteniendo sus marcos. El menú Canvas usa el degradado compartido.

El desenfoque utiliza un volumen temporal de profundidad de campo gaussiana, aplicado a la cámara principal antes de dibujar IMGUI. Los menús quedan nítidos. Al cerrar los menús o desactivar la opción se restaura la configuración previa de la cámara. No se implementa una copia exacta del material de Enshrouded. Cambios entregados sin ejecutar pruebas por pedido del usuario.
