# Bestiario, reconocimiento y monturas

## Diseño vigente

Esta entrega reemplaza la propuesta de alimentos y confianza del instructivo, y también la propuesta intermedia de desbloquear por cantidad de bajas. **Cada victoria tiene una probabilidad de reconocimiento**, configurable por especie. Solo se sortea si esa especie admite domesticación y montura y el jugador todavía no tiene una criatura propia. El sorteo se conserva durante reintentos de guardado. La recompensa normal y el vínculo se guardan en la misma operación.

El jabalí está habilitado inicialmente con **10 %**. Araña, goblin, gólem y guardián tienen fichas, pero no están habilitados para domesticación. Cambiar flags y probabilidad en los assets para ampliar el contenido después de ajustar una pose y comprobar su movimiento.

## Controles

- **B → BESTIARIO**: libro con páginas para especies descubiertas.
- **V** cerca del compañero: montar o desmontar.
- **H** cerca del compañero: seguir o esperar.
- Mientras se monta: movimiento habitual, sin ataques ni salto montado en esta versión. El jugador sigue siendo vulnerable; desmontar requiere suelo navegable y espacio libre.
- Una nueva posibilidad de reconocimiento no reemplaza la criatura actual.

## Descubrimiento

El libro empieza vacío. Una especie se registra cuando está delante de la cámara, dentro de 40 m y sin obstáculos entre cámara y criatura. No se descubre por estar cargada en un chunk ni por sumar una baja fuera de vista. Ver un ejemplar, incluso su cuerpo visible, descubre la ficha de esa especie; sus variantes comparten entrada. También se puede descubrir una criatura propia al verla.

El progreso se guarda por mundo en el mismo perfil del inventario. Los campos son opcionales y las partidas v5 anteriores se cargan con el bestiario vacío. Se conserva especie y variante de la montura, identidad del individuo y orden seguir/esperar. La criatura se reconstruye cerca del jugador en un punto navegable, siempre desmontado al cargar o reaparecer. El individuo vencido permanece registrado como derrotado y no se recrea como una copia salvaje.

## Assets y taller

- `Assets/Resources/BestiaryBook.asset`: colección de páginas y colores del papel/tinta.
- `Assets/Resources/Bestiary/*.asset`: nombre, descripción, prefabs que pertenecen a la especie, flags de domesticación/montura, porcentaje, velocidades y pose.
- `Assets/Data/RiderPoses/boar.asset`: asiento y pose inicial del jabalí. Ajustable en **Mismo → Criaturas → Taller de poses de montura**.
- El taller muestra personaje y criatura en una escena de previsualización aislada. Permite mover/rotar el asiento, cambiar escala, muestrear un clip y ajustar rotaciones de huesos. **Guardar pose** escribe el asset; no modifica los prefabs del personaje o enemigo.
- **Mismo → Criaturas → Preparar bestiario y efectos** genera las fichas iniciales que faltan y recalcula colores. Conserva los parámetros existentes de las especies y poses ya configuradas; actualiza sus referencias de prefabs y las páginas del libro.

## Fragmentos al recolectar

Los golpes generan cubitos que toman sus colores de una paleta extraída de las texturas y UV del modelo. Hay paletas en 57 prefabs de recursos. `ResourceImpactPalette` permite cambiar colores, cantidad, tamaño y duración. Los fragmentos son visuales, sin colisión, y desaparecen automáticamente. La paleta se prepara en Editor; no necesita texturas legibles en memoria durante la partida.

## Alcance y pruebas

- 92 comprobaciones del núcleo de inventario/guardado.
- 21 comprobaciones en Play Mode aislado de libro vacío, descubrimiento con/sin obstáculos, reconocimiento transaccional, reintentos, especies no habilitadas, conservación de la montura, montaje/desmontaje y fragmentos.
- Compilación de Player, Enemies y Player.Editor contra Unity 6000.6.
- Capturas en `output/creatures`: bestiary.png, mount.png y fragments.png.

La primera montura no tiene combate aliado ni vida propia: el daño sigue dirigido al jugador. No incluye reemplazo/colección de monturas, salto montado, vuelo ni recuperación automática de atascos. Seguir usa la navegación existente. Al cargar, la orden se conserva pero el punto de espera se reubica cerca del jugador. Falta balancear probabilidades, velocidad y poses en recorridos reales con distintas pendientes; las pruebas aisladas no sustituyen esa validación.
