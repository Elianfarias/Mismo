# Soul Eater: polvo de impacto

Integrado el prefab existente `DustExplosion` sin modificar el original ni sus materiales.

## Perfiles

Editar `Assets/Data/Enemies/DragonBosses/SoulEater_PhaseOne.asset`, apartado **Polvo de impacto · DustExplosion**.

| Evento | Escala del prefab | Densidad |
| --- | ---: | ---: |
| Picada e impacto de invocación | 2.2 | 1.5 |
| Aterrizaje después del camino de fuego | 1.2 | 1.0 |
| Apoyo de patas delanteras tras la carga | 0.55 | 0.45 |

También se pueden editar el límite por emisor (96), duración máxima (5 s), separación del suelo (0.08 m), opacidad de la onda (0.45) y umbrales de altura para detectar el apoyo real de las patas.

La carga emite al volver a apoyar una pata después de levantar ambas, no en cada zancada. Las caídas emiten una vez, después de actualizar la altura del terreno. Omitir la invocación ejecuta el mismo impacto sin duplicarlo.

## Rendimiento y presentación

Dos instancias reutilizables para combate y una para la llegada. Emisión única, sin bucles, luces ni colisiones de partículas. Las nubes quedan en el punto del impacto y se limpian al terminar o cancelar. La densidad se aplica después de limitar el gran burst de arena del prefab.

Los materiales se copian en memoria una vez por pool para neutralizar los parámetros de desvanecimiento que ocultaban el humo sin textura de profundidad. Se conservan los shaders y sus variantes. La onda se orienta sobre el suelo y tiene opacidad configurable. No se midieron FPS comparativos en la partida del usuario.

## Validación

Proyecto aislado `.validation/DragonRuntime`, sin intervenir en la sesión principal de Unity.

- 98 comprobaciones de fase 1, pool, partículas y contacto de patas: aprobadas.
- 44 comprobaciones de fase 2, incluidos polvo de picada y aterrizaje: aprobadas.
- 51 comprobaciones del arco/tutorial, incluida llegada normal y con Omitir: aprobadas.
- Capturas finales `Dive.png`, `Charge.png` y `Landing.png`: revisión visual del VFX. Son vistas de prueba; los disparadores reales se verificaron por separado.
- Build Windows generada: `Succeeded`. El informe conserva un error de validación de organización y 510 advertencias. Los 117 hallazgos de organización son los mismos del informe anterior, sin hallazgos nuevos.
- La última corrección posterior a la build solo restaura acentos en los textos del Inspector; no cambia código ejecutable ni parámetros.

Informes: `phase-one.txt`, `phase-two.txt`, `world.txt`, `build.txt`, `organization.txt` y `charge-contact.txt`.
