# Combate v0.2 implementado

Prototipo basado en Mismo Combat Design Addendum v0.2. Los cambios están activos tanto en la región como en GoblinEliteArena. No se modificaron el documento original ni los archivos del otro proyecto.

## Cómo probar

Abrir Assets/Scenes/GoblinEliteArena.unity para repetir el encuentro, o iniciar normalmente desde MainMenu.

- Espada: los primeros dos golpes admiten ramificación hacia Q, E o C en su tramo final (desde 65 % de la etapa). La intención se conserva durante 0,14 segundos. El último golpe mantiene su recuperación; no se puede cambiar de arma durante ataque ni recuperación.
- Arco básico: un toque completa una tensión mínima de 0,4 s. Mantener carga hasta 1,2 s y después libera automáticamente. Soltar antes del máximo libera al cumplir el mínimo. La carga aumenta daño y Posture mediante curvas del asset de habilidad. Se puede ajustar la mira durante la preparación.
- Q del arco: requiere 20 Focus, prepara durante 0,65 s y aplica 45 de daño base a Posture además del daño de vida. Focus se consume una sola vez al iniciar.
- E del arco: preparación de 0,45 s antes del disparo y retirada.
- R del arco: casteo de 0,7 s y demora de caída posterior. Permanece en el suelo al cambiar de arma.
- Durante preparación ranged, la movilidad solicitada baja al 40 %. Recibir daño válido interrumpe antes de liberar. El dash puede cancelar esa preparación, pero no un impacto activo ni una recuperación bloqueada.
- Cancelar o ser interrumpido conserva el costo y el cooldown ya pagados. La cancelación no borra flechas o áreas ya liberadas.

## Oportunidades y recursos

La barra dorada bajo la vida enemiga representa Posture restante. Goblins comienzan con 70 y el boss con 150. Regenera 9/s después de 3 s sin daño de Posture. Al llegar a cero, entra en una ventana de vulnerabilidad de 2 s; recibir más daño de Posture no prolonga esa ventana. Al terminar, recupera la barra. Este mecanismo reemplaza el stagger automático por umbral de daño de vida en esos enemigos. Un parry que no rompe Posture solo interrumpe brevemente la ofensiva.

Perfect Parry: los primeros 0,09 s de una defensa frontal válida. Otorga 20 Focus y aplica 35 de Posture al atacante. Un parry válido posterior aplica 12 de Posture y no otorga la recompensa perfecta.

Perfect Dodge: contacto real con un ataque durante los primeros 0,085 s de la esquiva. Otorga 20 Focus una vez por esquiva. Pulsar dash sin amenaza o estar invulnerable por un golpe anterior no da Focus. Feedback de tiempo: 0,12 s reales al 40 % de velocidad, con separación mínima de 1,5 s y restauración posterior.

Focus pertenece al combatiente, tiene tope 100 y permanece al alternar armas. Golpear por espalda otorga 8 y castigar recovery/Posture Break otorga 6 (se prioriza espalda, sin sumar ambas recompensas). Curarse a vida completa no borra Focus. Morir lo reinicia.

Los enemigos reciben 85 % del daño frontal, 100 % lateral y 120 % por espalda. Espalda multiplica Posture por 1,5 y una apertura por 1,4. Los proyectiles tienen una penalización de daño del 20 % hasta 2 m que desaparece gradualmente a los 10 m. Se mantiene además hasta +7,5 % de bonus por distancia, con tope a 18 m. Las áreas verticales no reciben multiplicador por espalda ni distancia.

El Elite observa una preparación ranged al decidir su siguiente acción: prioriza su carga si está en rango y disponible, conservando el aviso y la dirección fijada. También cierra más distancia antes de posicionarse. No obtiene daño ni velocidad adicionales por el arma del jugador.

## Arquitectura

DamageInfo transporta identificador de ejecución, daño de vida/Posture, autor, origen, dirección y propiedades de proyectil/área/parry. DamageReceiver.Resolve produce HitResult y emite Resolved. El contrato bool anterior se conserva como adaptador para las fuentes existentes. Los contactos repetidos de un ataque no vuelven a resolver daño o recompensas.

CombatState conserva Posture, Focus y la condición de apertura. DefenseWindow resuelve defensas independientemente de una espada concreta. SwordParry conserva la presentación y compatibilidad de escenas anteriores. AbilityRunner coordina carga, buffer, costos, cancelaciones e interrupción; AbilityExecution guarda estado por uso. PlayerMotor sigue siendo la única autoridad de movimiento.

Assets/Resources/CombatRules.asset contiene multiplicadores direccionales, bonificación de distancia, ventanas perfectas y recompensas. Los assets de habilidades contienen preparación, costo de Focus, carga, curvas y compromiso. Los máximos iniciales de Posture se asignan al integrar Goblin/Boss; los demás parámetros de Posture pertenecen a CombatState.

## Validación y límites

42 comprobaciones nuevas aprobadas en Play Mode, incluyendo Elite real, más 31 de regresión de armas. Resultados en Docs/Validation/CombatV2-checks.txt y WeaponSystem-checks.txt.

Es una base funcional para evaluar el combate, no un balance final. No incluye weakpoints anatómicos, progresión de armas, martillo, PvP ni networking. La breve ralentización es global y está pensada para el prototipo individual. El ZIP de playtest anterior conserva la versión de combate con la que fue compilado; estos cambios se entregan en el proyecto Unity.


## Ajuste de arco y feedback del 8 de septiembre

Daño base antes de modificadores: básico 8, Power Shot 24, Retreat Shot 6 y lluvia 5 por descarga. El básico tiene 0,4 s de preparación mínima, 0,35 s de recuperación y 0,85 s de cooldown desde inicio; la tensión máxima tarda 1,2 s. Retreat tiene 0,45 s de preparación y 0,3 s de recuperación. Tanto el proyectil como la retirada esperan hasta terminar el startup.

Perfect Parry ahora activa la misma ralentización breve que Perfect Dodge, respetando el límite común para evitar encadenarla. Parry normal confirma con el texto PARRY; no concede la ralentización ni Focus de una defensa perfecta. Los valores del generador de armas también se actualizaron para conservar este balance al regenerar los assets.

Validación del ajuste: 49 comprobaciones aprobadas, incluyendo penalización cercana y tope lejano, tiempos de básico/E y confirmación de parry normal/perfecto.
