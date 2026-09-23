# Contacto melee con la hoja

SwordCombo usa Shape=Blade en sus golpes, radio 0.035 m. Los puntos Base y Punta del perfil de arma definen la zona real de hoja; la segunda pieza usa sus propios puntos si es dual wield. No depende de que el VFX de trail esté activado.
AttackHitbox corre a orden 275, después de WeaponPresentation (250) y antes del trail (300). Mantiene historia de los extremos en el mundo y consulta cápsulas entre ellos, subdividiendo el avance durante la ventana de impacto. Box y Sphere siguen disponibles explícitamente para otras habilidades. Si falta visual/perfil, Blade no aplica daño frontal como fallback.
El radio es una tolerancia de contacto alrededor de la línea de hoja, no una colisión por cada triángulo del modelo. Se detecta contra los colliders de daño del enemigo. La interpolación entre muestras aproxima el movimiento a bajo framerate y está limitada a 256 subdivisiones por pieza/frame.
Un enemigo recibe como máximo un impacto por golpe. Cancelar descarta la historia. Los extremos deben excluir mango/pomo si no se desea que hagan daño.

El punto de impacto se calcula sobre el collider tocado y el segmento de hoja. Si la muestra ya penetró, se recupera la superficie de entrada en la dirección del movimiento. DamageInfo.HitPoint llega a CombatImpactPool; el prefab VFX se centra en ese punto ignorando su posición guardada, conservando orientación/escala y ajustes artísticos del cue.

El trail no incorpora nuevas muestras si el reloj está congelado. Una inversión brusca de movimiento o de orientación de hoja inicia otra tira, evitando quads plegados entre poses incompatibles. Estos cambios cubren causas detectadas por revisión; no se reprodujo visualmente la escena exacta del usuario.

Validación: compilación runtime/editor completos correcta. 15 checks de Unity aislado con AttackHitbox/BasicSwordCombo/WeaponTrailRibbon reales y servicios auxiliares sustituidos: barridos en ambos sentidos, enemigo frontal fuera del recorrido sin daño, ventana activa, un impacto por golpe, cancelación, superficie de contacto entregada a VFX, tiempo congelado y cambio de dirección. No es una prueba de la escena principal ni de todos sus prefabs VFX.
