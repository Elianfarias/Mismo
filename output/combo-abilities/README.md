# Combos configurados por habilidad

## Configuración

Abrir Assets/Data/Weapons/Sword/SwordCombo.asset, sección «Golpes del combo».
También: Mismo > Armas > Familias y animaciones > seleccionar espada > «Editar habilidad básica: tiempos, combo e impactos».

Cada golpe contiene duración (segundos), ventanas de impacto/encadenado/cancelación (0–100 %), transición, recuperación, daño y volumen de detección (caja o esfera, centro relativo al personaje, tamaño/radio).
La espada inicial tiene duraciones 0.8 / 0.8 / 1 s, impacto 35–65 %, daños 10 / 12 / 20. Es un ajuste inicial editable, no una calibración artística final de cada clip.
Las armas/familias siguen referenciando AbilityDefinition. Duplicar la habilidad permite configurar otra variante; compartirla comparte los datos. Los clips y su desplazamiento continúan en WeaponAnimationSet.

## Ejecución

BasicSwordCombo obtiene los pasos de la habilidad seleccionada. El Player ya no serializa tiempos ni ventanas/daños del combo.
AbilityRunner aplica la velocidad de ataque al único reloj del combo. El progreso dirige animación e impacto. Transición y recuperación conservan progreso 1, evitando rebobinar el clip.
AttackHitbox recibe el progreso y evalúa en LateUpdate, después de OnAnimatorMove. Recorta el trayecto al intervalo activo y combina overlaps con barridos de caja/esfera; subdivide rotaciones cada 5 grados. Deduplica por receptor y por golpe. Cancelación descarta segmentos pendientes; completar procesa el último segmento antes de cerrar.
Los colliders físicos del antiguo hitbox permanecen desactivados; los datos del ScriptableObject definen las consultas.
Se conservan multiplicadores, pasiva de broquel y DamageDealer. No se implementó red.

## Validación

Compilación completa del assembly runtime y su editor: correcta; solo warnings existentes (ver logs).
28 comprobaciones en Play Mode de un proyecto Unity aislado: ventanas normalizadas, daño después del avance, barridos box/sphere entre extremos, una ventana atravesada en un frame, cancelación (incluida desde recepción de daño), deduplicación de varios colliders, reinicio, combo encadenado y estado independiente entre personajes que comparten datos.
La integración usa el FBX real del aventurero, el primer clip real de espada, WeaponActionPlayback, PlayerAnimationMotion y PlayerMotor: un enemigo inicialmente fuera de alcance recibe exactamente un golpe durante el avance, al 35.42 % del clip.
Los servicios ajenos a la prueba (inventario, audio, pasivas y envoltorio de AbilityDefinition) se sustituyen en el proyecto aislado; el código completo del proyecto se verificó con compilación, no mediante una partida de la escena principal. Los archivos fuente de las pruebas y resultados se conservan aquí.
No se cambiaron rutas/cargas ni contenido incluido en builds.
