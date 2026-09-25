Dual Swords — validación

PASS: seis clips Humanoid de 60 fps y seis proyectos de poses editables.
PASS: reproducción, movimiento de ambas manos y conservación de poses al exportar.
PASS: revisión visual con el aventurero y los prefabs/agarres actuales de ambas armas.
PASS: dos clips de SwordCombo y cuatro habilidades referenciados en DualSwordsCombatAnimations.
PASS: fases de reproducción y máscara Full Body de Torbellino.
PASS: compilación completa del assembly de editor contra las referencias actuales del proyecto.
PASS: build de contenido Windows y carga de los seis clips a través de WeaponAnimationSet.
PASS: comprobaciones del creador Humanoid, IK y persistencia tras corregir la carga de poses.
PASS: Sword.asset, SwordCombo.asset y el proyecto Ataque_Humanoid_Dual.asset conservan sus hashes.

Alcance: reproducción, carga serializada y build de contenido ejecutadas en un proyecto Unity aislado. WeaponAnimationSet y los assets son copias del proyecto; servicios de gameplay ajenos a animación usan fixtures. No es una build completa del juego ni una prueba de combate en Play Mode. ProjectOrganizationChecks.Run global no se ejecutó en el editor principal; está disponible desde el menú de comprobaciones del proyecto. No se modificaron escenas abiertas.
