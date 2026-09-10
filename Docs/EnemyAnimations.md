# Animaciones de ataques enemigos

Los ataques de goblins y del primer boss admiten clips individuales en sus ScriptableObjects de configuración. No requieren nuevos estados en el Animator.

## Configuración en Unity

1. Seleccionar el enemigo y abrir su asset **Settings** desde `GoblinController` o `BossController`.
2. Expandir el ataque: **Slash / Charge** para goblins; **Front Slash / Overhead Smash / Straight Charge** para el boss.
3. Expandir **Animation** y asignar un `AnimationClip` compatible con el rig del enemigo.
4. Ajustar **Active Starts At** y **Recovery Starts At** entre 0 y 1. Indican el comienzo del golpe y de la recuperación dentro del clip, no segundos de combate.
5. **Blend Seconds** controla la mezcla de entrada y entre clips. **Mask** es opcional: vacío usa cuerpo completo; una `AvatarMask` limita los huesos afectados.

Por ejemplo, para un clip de un segundo con impacto desde 0,25 s y recuperación desde 0,75 s, usar 0,25 y 0,75. `Windup`, `Active` y `Recovery` del ataque siguen definiendo cuánto dura cada fase real. El clip se adapta a esas duraciones. El movimiento de cargas sigue controlado por la IA; no se aplica root motion al reproducir clips configurados.

Si **Clip** queda vacío se conserva la animación anterior del controlador. Los assets existentes no se modifican ni se les asigna una animación arbitraria. Dos enemigos que comparten Settings comparten la configuración; duplicar el asset permite una variante independiente.

## Reproducción y validación

Los clips del esqueleto `Armature_Humanoid` del aventurero se adaptan automáticamente al esqueleto `Goblin_Rig` usado por el goblin y el jefe. El editor guarda las copias en `Assets/Art/Animations/GoblinConcept/Compatible`, conserva el clip asignado y vuelve a generarlas cuando cambia el origen. También se pueden actualizar desde **Mismo > Enemigos > Actualizar clips compatibles**. La adaptación conserva las proporciones del enemigo y su desplazamiento controlado por la IA. Otros esqueletos requieren una adaptación propia.

La adaptación de estos ataques terrestres compensa también la altura del esqueleto usando los vértices reales de las suelas. Transferir únicamente rotaciones dejaba ambos pies suspendidos al flexionar las piernas. La compensación mantiene la suela de apoyo a la altura de reposo y no desplaza al agente ni su collider. Los futuros ataques aéreos necesitarán una política de apoyo distinta.

`EnemyGroundSupport` ajusta además la posición del contenedor visual contra los colliders del suelo en cada `LateUpdate`, también durante idle y locomoción. Así la presentación no depende de que el NavMesh coincida exactamente con la superficie física. Ignora triggers, al propio enemigo y a otros personajes; sin suelo cercano no aplica corrección. No modifica el agente ni su collider y se desactiva para la pose de muerte. `Sole Clearance` es la separación de la suela en reposo del modelo Concept Goblin (0,0325 unidades antes de escala); debe recalibrarse si se cambia el modelo. No implementa IK independiente por pie sobre escalones.

`EnemyRigChecks.RunBatch` pasó 39 comprobaciones sobre los prefabs reales: los cinco ataques tienen curvas que encuentran los huesos, el reproductor evalúa esas poses y brazos y torso cambian entre cuadros. Comprueba además 121 muestras por ataque contra la altura de las suelas en reposo: la desviación máxima fue de 3,5 mm. También verifica el nombre visible «Arma a una mano» y que el identificador de maestría se conserve. La calidad artística de cada pose y el apoyo sobre terreno irregular deben revisarse en Play.

`EnemyAttackAnimation` contiene solamente datos serializados. Ambos drivers consultan `CurrentAttack` y `StateProgress`; `EnemyActionPlayback` reutiliza las capas de `WeaponActionPlayback`. El Animator base conserva locomoción y reacción a impactos. Al interrumpirse el ataque, entrar en stagger o morir, se libera inmediatamente la capa de ataque. Deshabilitar el driver destruye su grafo.

`EnemyAnimationChecks.RunBatch` valida fases, límites, serialización, independencia de ataques, evaluación real de clips, cambio de acción, fallback, interrupción y recreación del grafo. No sustituye la revisión artística de clips y máscaras sobre cada rig.
