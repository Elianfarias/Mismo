# Assets de la pantalla de recetas

Piezas originales del concept aprobado, en `Assets/Art/UI/Recipes`. Son PNG RGBA independientes, sin texto ni cantidades incrustadas. El generador reproducible es `Assets/Scripts/Editor/GenerateRecipeUI.py` (Python + Pillow). Conserva los GUID y los identificadores de sprites al regenerar. No modifica los marcos anteriores.

## Componentes

| Archivo | Uso | Importación / borde izquierdo, inferior, derecho, superior |
| --- | --- | --- |
| WindowFrame | Contorno de la ventana, transparente por dentro | 32×32, nueve cortes 9,9,9,9 |
| WindowSurface | Fondo oscuro sobre el mundo | 4×4, estirar; alpha base 220/255 |
| ControlNormal, ControlHover, ControlSelected, ControlPressed, ControlDisabled | Filtros y botón Fabricar; compartir los mismos sprites | 32×32, nueve cortes 9,9,9,9 |
| RowHover, RowSelected | Resaltado de la fila; selección dorada con marca izquierda | 32×32, nueve cortes 4,4,4,4 |
| RowSeparator | Línea sutil entre recetas | 16×3, nueve cortes 2,0,2,0; conservar alto 3 |
| SectionDividerHorizontal | Separación con materiales | 24×5, nueve cortes 4,0,4,0; conservar alto 5 |
| SectionDividerVertical | División lista/vista previa | 5×24, nueve cortes 0,4,0,4; conservar ancho 5 |
| ScrollTrack, ScrollThumb | Lista desplazable | 4×16 y 8×16; nueve cortes 1,3,1,3 |

Son 14 piezas de interfaz. El generador solo produce marcos, fondos, filas, separadores y scroll; no dibuja iconos. Los marcos ya contienen su color; usar tint blanco en ellos. Point, Clamp, sin mipmaps ni compresión en estas piezas. No usar Tight mesh con marcos de nueve cortes. Mantener los controles por encima de 18×18 unidades visuales para no comprimir las esquinas.

`RecipeUIManifest.json` enumera las 14 piezas y sus cortes, y remite al mapa de iconos. No carga contenido en ejecución. Las láminas `Docs/Previews/RecipeUIAssets.png` y `Docs/Previews/RecipeUIIcons.png` muestran las piezas y los iconos reales.

## Iconos de Flaticon

Reutilizar exactamente `all`, `weapons`, `consumables`, `materials` y `close` de `Assets/Data/UI/InventoryUIIcons.asset`. Los cuatro filtros apuntan a `border-all.png`, `role-play.png`, `flask-potion.png` y `diamond.png` en `Assets/Art/UI/WhiteAndBlackGUI`. También se reutilizan las flechas arriba/abajo del proyecto. No copiar ni reemplazar estos archivos: el mapa conserva sus rutas y las referencias del inventario. Los 15 iconos dibujados anteriormente fueron retirados después de comprobar que no tenían dependencias.

Se descargaron los ocho símbolos faltantes de la distribución oficial `@flaticon/flaticon-uicons` 3.3.1, estilo Solid Rounded. Sus siluetas originales se rasterizan desde el webfont oficial, sin redibujarlas: mano (`hand-paper`), mesa (`tools`), fabricar (`hammer`), recetas (`book-bookmark`), ubicación (`marker`), árbol (`tree`), candado (`lock`) y disponible (`check`). PNG transparentes 512×512 en `Assets/Art/UI/Flaticon/Recipes`, blancos para tintado, Bilinear para reducción suave, sin mipmaps ni compresión.

`Assets/Art/UI/Flaticon/Recipes/Sources.json` documenta cada ruta, campo compartido, glifo, versión y hashes de procedencia. El font fuente se conserva en `Assets/Art/Fonts/Flaticon`. La licencia oficial está en `Assets/Documentation/FlaticonUicons-LICENSE.txt`. Crédito para la integración: **Uicons by Flaticon — https://www.flaticon.com/uicons**.

Reproducción: descargar el paquete indicado en Sources.json, ejecutar `ImportFlaticonRecipeIcons.py <archivo.tgz>` y luego `RenderFlaticonRecipeIcons.cjs` con Node y Playwright en NODE_PATH. La segunda herramienta convierte los glifos oficiales a PNG y arma la lámina; no requiere sesión de Flaticon ni ejecuta scripts del paquete descargado. Los scripts conservan los GUID existentes. La vía de distribución está documentada por Flaticon en https://www.flaticon.com/uicons/get-started.

## Implementación

- Una ventana común: filtros arriba, lista a la izquierda, vista del objeto a la derecha y materiales abajo. `RecipeBookView` dibuja sobre un canvas lógico 1280×800, ventana x=28, y=20, ancho=1224, alto=760, con escala uniforme según la pantalla. El editor de posiciones del HUD se conserva.
- Superponer WindowFrame al fondo WindowSurface con un margen interior mínimo de 7 unidades para que el fondo no sobresalga por las esquinas. Evitar duplicar opacidades entre el fondo y la ventana.
- En Canvas, marcos con `Image.Type.Sliced`, `fillCenter=true` y escala que mantenga el borde. En IMGUI usar `GUIStyle.border` con los cortes del manifiesto: RectOffset usa el orden izquierda, derecha, arriba, abajo. `GUI.DrawTexture` por sí solo deforma las esquinas.
- Las filas normales solo llevan separador. Dibujar RowHover o RowSelected detrás de icono y nombre; no alterar la legibilidad de recetas bloqueadas.
- Los filtros y Fabricar comparten los cinco estados Control*. El icono y el texto son elementos independientes. Para selección + hover conservar el estado seleccionado; la pulsación usa ControlPressed.
- Iconos pequeños entre 24 y 32 unidades, vista principal del objeto aproximadamente 250–300. Las miniaturas y el objeto grande proceden de las definiciones/modelos reales del juego: estos assets de UI no crean un modelo 3D de ungüento ni nuevos objetos de inventario.
- Materiales: icono, nombre, cantidad disponible/necesaria y origen. Texto normal marfil `#EAE8D3`, selección `#EFCE7F`, disponible `#A9D7B5`, faltante `#F0A993`; acompañar siempre el color con cantidades o un motivo.
- Consulta: ocultar Fabricar conservando el espacio de detalle. En el mundo: mostrar fabricación solo cuando la receta lo permita. En mesa: misma pantalla y botón con IconCraft; indicar requisitos incumplidos.
- El radial actual tiene ocho casillas con los marcos compartidos del HUD; Recetas corresponde al índice 2. `InventoryUIIcons.radialRecipes` usa el libro con marcador de Flaticon y permite sustituirlo desde el Inspector.

## Estado y verificación

La pantalla ya está integrada. **B → Recetas** abre el catálogo completo con filtros Todos, Armas, Consumibles, Materiales y Mejoras. **G**, cerca de una mesa, abre la misma vista limitada a las recetas de esa estación. Lista, vista previa, materiales, cantidades y orígenes proceden de las definiciones reales del juego. El botón Fabricar/Mejorar tiene icono y muestra el motivo cuando no está disponible. El scroll utiliza las piezas escalonadas del paquete.

`CraftingRecipe.craftInWorld` habilita fabricación fuera de una estación. Inicialmente solo el ungüento de hierbas la tiene activa. Las recetas de estación siguen visibles en el catálogo, pero no permiten fabricar allí; las mejoras siempre requieren mesa. Las transacciones verifican materiales, espacio, combate, distancia a la estación y registro de la receta, y conservan el inventario si falla el guardado.

`Assets/Data/UI/RecipeUITheme.asset` reúne las referencias serializadas y se registra con una sola clave `UI/Recipes` en el catálogo existente. Los iconos compartidos se leen directamente de `InventoryUIIcons`, sin duplicarlos. No se modifican escenas ni prefabs para integrar esta pantalla. La vista 3D se prepara y renderiza en LateUpdate, fuera de OnGUI.

`RecipeUIIntegration.Register` registra el tema y verifica los assets. `RecipeUIIntegration.Build` compila Windows con las escenas principales. `RecipeUIFlowChecks.Run` prueba el flujo en la copia aislada `.validation/Organization`, con inventario en memoria, sin acceder a la partida del usuario. El comprobador optativo de build `-mismo-pixel-ui-check <carpeta>` también revisa las dependencias y captura radial, recetario y mesa.

Menú de editor: **Mismo → UI → Verificar assets de recetas** (`RecipeUIAssetChecks.Run`). Comprueba nueve cortes e importación de los marcos, los 15 iconos, coincidencia exacta con los campos del inventario, importación de los PNG oficiales y ausencia de iconos generados. Ejecuta también `ProjectOrganizationChecks.Run`.

Verificación de integración (2026-09-20): 44 comprobaciones de flujo en Play Mode aprobadas, incluyendo filtros, fabricación a mano y en mesa, costes, rollback de guardado, mejoras, sectores del radial, exclusión de pantallas simultáneas y cierre al destruir una estación. Sin errores de ejecución/GUI. Se conserva en el log un error ajeno del indexador de búsquedas del editor de Unity, excluido explícitamente por su stack de editor.

Build de Windows: `RecipeUIIntegration.Build` finalizó correctamente (725638708 bytes) y volvió a pasar `ProjectOrganizationChecks.Run`. El ejecutable cargó las referencias y abrió las pantallas solicitadas; su capturador automático devolvió imágenes vacías y errores de captura, tanto con batchmode como con renderizado normal oculto. La revisión visual se realiza mediante las capturas de Play Mode; no se considera aprobada la captura visual del ejecutable.
