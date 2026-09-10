# Inventario single-player

El inventario conserva armas propias, dos ranuras y la progresión entre intentos y sesiones. Se activa en los jugadores con `RegionRespawn`; las arenas de prueba permanecen independientes del perfil. La ampliación de niveles, maestría y tiers se describe en [Progression.md](Progression.md). No incluye materiales, pociones, comercio ni conexión con PvP.

## Uso

- `I` abre o cierra el inventario. `Escape` o el botón Cerrar también lo cierran.
- Se abre cuando no hay un ataque o dash en ejecución. Durante combate permite consultar las armas, pero los botones de equipamiento quedan deshabilitados. El mundo continúa y la gravedad sigue funcionando; morir cierra el panel.
- Seleccionar un arma muestra su kit. Los botones asignan el ejemplar a una de las dos ranuras. Si ya ocupa la otra ranura, se intercambian ambas asignaciones.
- `Tab` conserva su función de alternar el arma activa durante el juego.
- Derrotar al boss genera una Espada del guardián cerca de su posición. Se recoge por proximidad y una sola vez por perfil. Comparte familia con la espada inicial y sus ejemplares nuevos son T2 Guardián.
- Morir reinicia la región, pero conserva la colección, las ranuras y la recompensa reclamada. Volver a abrir la partida recupera esas pertenencias al comenzar desde el pueblo. No se guarda el estado completo del mundo.

## Responsabilidades

`WeaponDefinition` contiene los datos compartidos. `ItemCatalog`, en Resources, mantiene referencias a las definiciones incluidas en la build y a la recompensa inicial. Sus identificadores deben permanecer estables.

`OwnedWeapon` identifica un ejemplar mediante un GUID, una definición, un tier y una variante. `InventoryProfile` contiene colección, ranuras, recompensas y progresión de personaje y familias. No contiene objetos de escena, Focus, vida actual ni cooldowns. Tampoco modifica ScriptableObjects.

`PlayerInventory` coordina propiedad, validación y persistencia. Construye una copia del perfil, intenta guardarla y solo entonces aplica el cambio. Un error de escritura deja intacto el estado previo. La adquisición marca la recompensa y agrega el ejemplar en una misma escritura; el pickup no desaparece si falla.

`EquipmentLoadout` sigue resolviendo si se puede equipar o alternar y continúa exponiendo `ActiveDefinition` a habilidades y presentación. Cuando existe inventario, sus operaciones públicas delegan en él para impedir equipar objetos no poseídos. Las habilidades compartidas conservan sus cooldowns al reequipar.

`InventoryPanel` muestra información y solicita operaciones. Consume los controles del jugador y libera el cursor mientras está abierto; al cerrarse consume también ese frame para evitar que el clic se convierta en un ataque. No modifica la escala temporal.

`IProfileRepository` es el límite de almacenamiento. La implementación actual es `ProtectedProfileRepository`; una futura implementación remota no necesita introducir consultas HTTP en la interfaz o en el combate.

## Formato y recuperación

Ubicación: `Application.persistentDataPath/Profiles/single-player.mismo`. En Windows, Unity resuelve la raíz según Company Name y Product Name. No se escribe en Assets ni se guarda el perfil en el repositorio.

El perfil JSON versión 2 se almacena dentro de un contenedor binario autenticado: cabecera y versión, IV aleatorio de 16 bytes, contenido cifrado con AES-256-CBC/PKCS7 y HMAC-SHA256 de cabecera, IV y contenido cifrado. Se emplean claves distintas para cifrado y autenticación. El MAC se verifica antes de descifrar. La versión del contenedor criptográfico no cambia.

La clave de aplicación se deriva dentro del cliente para permitir portabilidad. Esto dificulta la edición casual y detecta cambios de bytes, pero no es una autoridad antitrampas: una persona que controle el proceso puede extraer la clave, cambiar código o sustituir un guardado por otro válido anterior. No se presenta como protección frente a un atacante con control total de su equipo.

Cada escritura usa un archivo temporal, vaciado a disco y reemplazo del archivo principal. `.bak` conserva el principal anterior validado. Si se recuperó un respaldo, el siguiente guardado no lo reemplaza por el principal corrupto. Un temporal incompleto no se carga como progreso confirmado.

Al cargar se verifican versión, tamaño, identificadores únicos, definiciones existentes, dos ranuras válidas, recompensas conocidas y límites de progresión. Un archivo inválido intenta recuperarse desde `.bak`. Si ninguno es válido, se conservan los archivos y se muestra un aviso; no se sobrescriben automáticamente con un perfil nuevo. Los perfiles v1 válidos migran explícitamente a v2; versiones futuras desconocidas se rechazan. La migración se detalla en Progression.md.

El perfil no es una fuente de estadísticas autorizadas para PvP. La futura instancia debe construir y validar su propio equipamiento normalizado, sin trasladar cantidades o mejoras locales como datos confiables.

## Validación

`dotnet run --project Tools/InventoryCoreChecks/InventoryCoreChecks.csproj` verifica cifrado, integridad, respaldo, rechazo de archivos inválidos y reglas de propiedad sin ejecutar Unity. Usa un directorio temporal exclusivo.

`Mismo.Gameplay.Player.Editor.InventoryChecks.RunBatch` ejecuta las pruebas de integración en una copia aislada de Unity: pickup físico, equipamiento, duplicados, combate, fallo de escritura, cursor, muerte, recarga y recuperación. No usa el perfil personal. Produce `Docs/Validation/Inventory-checks.txt` y una captura de la pantalla.

Validación del 8 de septiembre de 2026: 26 comprobaciones del núcleo y 40 de integración aprobadas en Unity 6000.3.11f1. Se verificó visualmente la captura del panel y se comprobó que no atraviesan los controles de movimiento, ataque y alternancia. Los reportes están en `Docs/Validation/Inventory-core-checks.txt` y `Docs/Validation/Inventory-checks.txt`.

