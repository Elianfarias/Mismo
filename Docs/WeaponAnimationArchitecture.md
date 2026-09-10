# Animaciones por familia de armas — primera etapa

## Responsabilidades

- **Personaje:** locomoción, estados de suelo/aire, reproducción y mezcla de animaciones.
- **Familia:** habilidades predeterminadas y correspondencia entre cada habilidad y sus clips de combate.
- **Modelo de arma:** apariencia y perfil de agarre, posición guardada y estela.
- **Combate:** preparación, carga, acción, recuperación, cancelación, costes e impactos. Sigue siendo la autoridad del tiempo.

`WeaponDefinition.family` referencia un `WeaponFamilyDefinition`; éste referencia un `WeaponAnimationSet`. La espada normal y la recompensa del jefe comparten la familia `OneHandSword`. El arco tiene una familia distinta. Los perfiles de pose ya ajustados se conservan.

Por defecto se usan las habilidades de la familia. `Override Family Abilities` permite una excepción que utiliza el arreglo del arma. Las habilidades siguen siendo assets compartidos: duplicar una familia no duplica ni cambia sus reglas de daño.

## Reproducción

`AbilityRunner.TryGetAnimationFrame` entrega una lectura de habilidad, fase, progreso, etapa del combo e identidad de ejecución. No avanza el combate. La carga mantenida retiene el final de preparación; la fase activa comienza en el instante real de liberación.

`PlayerAnimationDriver` decide la locomoción y busca el clip en el conjunto de la familia. `WeaponActionPlayback` usa Playables para mezclar el controlador común y los clips de acción. Una acción nueva con clip no necesita una entrada nueva en `CharacterMotion` ni un estado nuevo en el Animator. También mezcla cambios entre clips consecutivos.

El conjunto admite `Action Mask`: sin máscara, la acción afecta al cuerpo completo; con máscara, sólo reemplaza los huesos seleccionados y deja el resto a la locomoción. El arco usa `BowUpperBody.mask`, que incluye desde la columna hasta cabeza y brazos, y excluye raíz, cadera y piernas. Así se mantiene la caminata durante el tensado, sin dividir el personaje en objetos distintos. La máscara contempla tanto las partes Humanoid como las rutas del rig Generic actual. Si cambia el esqueleto, hay que revisar esas rutas.

La implementación usa [capas con máscaras de Unity](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Animations.AnimationLayerMixerPlayable.SetLayerMaskFromAvatarMask.html). Las máscaras se asignan en **Familias y animaciones > Action Mask** y se pueden reutilizar entre familias compatibles.

### Excepciones por acción

En **Familias y animaciones > Actions**, cada acción tiene `Mask Mode`:

| Modo | Comportamiento |
| --- | --- |
| Inherit Family | Usa `Action Mask` de la familia. Es el valor predeterminado, también para las acciones existentes. |
| Full Body | Ignora la máscara de familia: el clip controla el cuerpo completo. Por ejemplo, una voltereta. |
| Custom | Usa `Custom Mask` sólo para esa acción. Si falta, el editor muestra una advertencia y se hereda la máscara de familia. |

La elección se aplica al clip o a los clips de combo de esa acción; no modifica los archivos de animación ni otras acciones de la familia. Cambiar de máscara conserva el reloj de locomoción. Se descarta la mezcla del clip saliente para que no afecte huesos que su máscara anterior excluía, y la nueva acción entra con su duración de mezcla configurada. Al cancelar se conserva la máscara saliente durante la salida; la acción siguiente resuelve su propia máscara.

Cada binding permite un clip de acción completo o una lista de clips de combo. `Active Starts At` y `Recovery Starts At` indican dónde empiezan esas fases dentro del clip. La animación sigue el tiempo del combate, no determina cuándo hace daño. La lista de clips de combo no tiene un límite de tres: debe coincidir con las etapas que realmente configure el sistema de combate.

Las animaciones actuales de espada se migraron a la familia. El movimiento común sigue en el controlador existente. Un override antiguo del perfil de pose tiene precedencia sobre el override de locomoción de familia para preservar configuraciones previas; **los clips de acción nuevos se editan en la familia**.

## Uso en Unity

Abrir **Mismo > Armas > Familias y animaciones**, o el botón correspondiente del taller de poses.

1. Seleccionar el arma.
2. Asignar una familia existente; **Tomar familia de** permite compartir la de otra arma.
3. Editar habilidades y clips en la familia. Los cambios alcanzan a todas las armas que la comparten.
4. Para una variante, **Duplicar familia y animaciones para esta arma** crea otra familia y otra configuración de bindings; los assets de clips, habilidades y override de locomoción referenciados siguen compartidos.
5. Ajustar el modelo y su agarre en **Taller de poses**, independientemente de la familia.

## Compatibilidad y trabajo restante

Esta es la primera etapa funcional, no la eliminación de todo el sistema heredado.

- `LegacyCombatAnimationAdapter` concentra la traducción antigua de parry, estocada, giro y combo para habilidades sin clip configurado. El camino nuevo lo omite cuando encuentra un clip válido.
- El arco usa los clips de tensado/reacción que configuró el usuario, limitados al torso mediante su máscara. Las habilidades sin clip conservan la ruta de compatibilidad.
- Las reglas de combo e impacto existentes aún residen en sus componentes de combate. Extraer secuencias y ventanas a datos por familia es una siguiente etapa distinta del desacoplamiento visual realizado aquí.
- Los overrides guardados dentro de perfiles de pose siguen soportados. Su migración definitiva a locomoción de familia requiere revisar variantes que ya haya configurado el usuario.

## Referencia consultada

El [módulo de personajes de Veloren](https://gitlab.com/veloren/veloren/-/blob/master/voxygen/anim/src/character/mod.rs) separa implementaciones de movimiento y acciones. Su [animación de acción básica](https://gitlab.com/veloren/veloren/-/blob/master/voxygen/anim/src/character/basic.rs) recibe habilidad, fase, orientación y velocidad, y calcula transformaciones de huesos mediante código. Se tomó la separación entre estado de combate y presentación como referencia; no se copió su implementación procedural ni se asumió que carezca de casos específicos por arma.

## Validación

`WeaponFamilyChecks.RunBatch` comprueba en Play Mode familias compartidas, clips arbitrarios fuera del enum heredado, evaluación real sobre un transform del personaje, fases y carga, cancelación, independencia del reloj de combate, una quinta entrada de combo y cambios de equipamiento. `InventoryChecks.RunBatch` comprueba la regresión de inventario, equipamiento y disparo básico/cargado.

`WeaponLayerChecks.RunBatch` comprueba ocho orientaciones del arma guardada con raíz física inmóvil, exclusión de curvas de piernas en la capa de acción, continuidad de locomoción y retención del tensado. También evalúa el rig y el clip reales del arco para verificar que cambia el brazo mientras la pierna sigue caminando.
