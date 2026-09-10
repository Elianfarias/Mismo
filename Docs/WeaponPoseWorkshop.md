# Taller de poses de armas

Abrir **Mismo > Armas > Taller de poses**, fuera de Play Mode.

Las armas migradas a una familia ahora configuran sus clips de combate en **Mismo > Armas > Familias y animaciones**. El botón del taller abre esa ventana. El agarre continúa en este taller. Ver [arquitectura y uso de familias](WeaponAnimationArchitecture.md).

1. Seleccionar el prefab del personaje y un `WeaponDefinition` (Sword, Bow o un arma nueva). El personaje actual viene preseleccionado.
2. Asignar un `Visual Prefab` que contenga únicamente el arma en su definición. Crear un perfil desde la ventana o asignar uno existente. Un perfil compartido afecta a todas las armas que lo usan; duplicarlo para variantes independientes.
3. Abrir **Vista de ajuste aislada**. En Scene se muestra una copia; la escena y el prefab originales no reciben las poses de previsualización.
4. Elegir un hueso con **Elegir hueso de anclaje**. En rigs Generic se guarda la ruta relativa al Animator. En Humanoid se pueden usar Right Hand / Left Hand. Character usa la raíz del personaje.
5. Usar **W** para desplazar y **E** para rotar el arma en Scene. También se pueden editar Offset, Rotation y Scale en el perfil. Los offsets son metros orientados por el hueso, independientes de su escala importada. Undo funciona sobre el perfil.
6. Seleccionar un clip y mover su deslizador de tiempo para comprobar el agarre. Se muestrea la animación corporal; esta herramienta no crea keyframes ni reemplaza un editor de animaciones/IK.
7. Activar **Editar arma guardada** para configurar el segundo anclaje. Ocultar las mallas de armas integradas en el personaje desde **Ocultar/mostrar malla integrada** si aparecen duplicadas. Esta selección también se usa al jugar.
8. Para armas con familia, asignar los clips de combate desde **Editar familia y animaciones de combate**. Los overrides del perfil siguen disponibles para variantes del movimiento común y para compatibilidad con armas sin familia. En estas últimas se utiliza un `AnimatorOverrideController` basado en el controlador del personaje; mantiene estados, transiciones y parámetros.
9. **Guardar arma y perfil**, cerrar la vista y probar en Play Mode. La referencia asignada al arma se guarda en el asset, no en la partida del jugador.

Para armas de contacto, activar `Melee Trail` y ajustar `Trail Tip` en coordenadas locales del visual. La estela sigue esa punta y las ventanas de impacto existentes; ya no sigue la espada integrada del modelo. Desactivarlo para armas que no requieren estela.

El anclaje `Character` usa la posición de la raíz y la orientación visual del personaje; por eso el arma guardada acompaña sus giros aunque el motor no rote la raíz física. El editor usa la misma orientación al dibujar los controles. Para seguir además la torsión y la inclinación animada del pecho, seleccionar ese hueso mediante `Bone Path` y ajustar la pose guardada.

## Alcance y compatibilidad

Los perfiles nuevos controlan anclaje, offsets, orientación, escala, mallas ocultas y reemplazos de animación. Una postura de brazos distinta debe venir de los clips; el perfil desactiva el IK de arco antiguo para que no sobreescriba las animaciones elegidas. No modifica daño, hitboxes, proyectiles ni duración de habilidades.

Las armas existentes sin perfil conservan su presentación anterior. No se migraron automáticamente: la espada actual tiene geometría integrada en el rig y el arco usa IK procedural; reemplazarlos requiere autorizar visualmente el agarre y los clips apropiados. Asignar un perfil opta por el sistema nuevo. No constituye una eliminación completa de todo el código heredado.

Las rutas de huesos son específicas del rig; un perfil con rutas Generic se comparte entre armas que usan ese mismo esqueleto. Un anclaje inválido se advierte en la ventana y oculta el visual al jugar.

La vista usa [PreviewSceneStage de Unity](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/SceneManagement.PreviewSceneStage.html). Las comprobaciones automatizadas se ejecutan con `Mismo.Gameplay.Player.Editor.WeaponPoseChecks.RunBatch` y comprueban aislamiento de escena, scripts desactivados, anclajes, rotaciones y escala del rig.
