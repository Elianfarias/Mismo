# Human Melee: adaptación para Mismo

Origen: **Human Melee Animations FREE**, Kevin Iglesias.
https://assetstore.unity.com/packages/3d/animations/human-melee-animations-free-165785
Los originales y su documentación permanecen en
`Assets/Art/Animations/WeaponCombat/Human Animations`.

## Integración

- Los clips fueron adaptados a los rigs Generic del jugador y del goblin.
- Se conservan las proporciones del rig y el movimiento horizontal del motor.
- Las familias OneHandSword, DualSwords y SwordShield usan los nuevos ataques
  de combate. Las dos espadas alternan derecha e izquierda; DualSequence y
  DualCross combinan esos dos ataques, no son clips originales del paquete.
- Las acciones usan una máscara de torso/brazos. El jugador conserva su
  reposo, caminar, correr, saltar e inclinación originales, también con armas melee.
  No se superpone la postura del paquete sobre la locomoción ni la altura de los pies.
- El jugador y el goblin reproducen CombatDamage ante daño real. La reacción
  dura 0,36 s, tiene prioridad visual y no modifica los tiempos de daño de la IA.
- Las reacciones y máscaras están en `Assets/Resources/CombatPresentation`.
- El arco conserva sus animaciones previas. No se cambiaron daños, cooldowns,
  ventanas de impacto, movimiento del motor ni perfiles de guardado.

La versión gratuita no incluye una animación exclusiva para cada habilidad,
un combo doble completo ni todos los movimientos especiales. Las habilidades
reutilizan los movimientos disponibles. El clip de muerte y los clips de dos
manos/lanza quedan disponibles; no se sustituyó el efecto de muerte en cubos
ni se crearon familias de armas nuevas.

## Verificación

Se adaptaron 17 clips (15 conversiones y 2 combinaciones) con curvas finitas a
60 muestras por segundo. Se renderizaron poses para revisión visual.
Pasaron 20 comprobaciones de reproducción, rutas, máscaras y prioridad de
reacción en un proyecto aislado de Unity 6000.6.0f1 usando el código real de
WeaponActionPlayback y EnemyActionPlayback. El proyecto principal compila.

Informes locales: `output/human-animations/bake-result.txt`, `checks.txt` y
`mapping.txt`. Falta la revisión jugable final en la escena principal, con
equipamiento, enemigos y cámara de juego.
