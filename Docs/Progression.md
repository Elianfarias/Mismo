# Niveles maestría y tiers

Primera implementación del bloque de progresión del GDD v0.5, integrada con el inventario single-player. Abrir **I → Niveles y maestría** para gastar puntos fuera de combate. Seleccionar un arma en la pestaña Armas permite consultar su familia.

## Progresión jugable

Derrotar enemigos otorga experiencia de personaje. Cada nivel concede un punto para elegir vida máxima, ataque o armadura. La maestría tiene su propio nivel y puntos para daño o velocidad; no aumenta alcance. La espada inicial y la recompensa del boss comparten familia.

La maestría se reparte al derrotar al enemigo según daño efectivo y parries exitosos. Un parry agrega una contribución acotada, sin otorgar experiencia antes de la victoria. El pool total por enemigo es fijo: repetir defensas no multiplica la recompensa. Llevar un arma guardada o esquivar con el cinturón no le concede maestría.

Los goblins pueden dar armas con tier y variante. En esta etapa se incorporan directamente al inventario y se anuncian en pantalla. El pickup único del boss sigue existiendo y sus ejemplares nuevos son T2 Guardián. Morir conserva experiencia, puntos, maestrías y armas.

## Balance configurable

Editar `Assets/Data/Progression/ProgressionRules.asset`. Todos los valores son iniciales para playtest.

| Sistema | Valor inicial |
| --- | --- |
| Nivel máximo del personaje / maestría | 50 / 20 |
| EXP al siguiente nivel | 60 + 30 × (nivel − 1) |
| EXP a la siguiente maestría | 40 + 20 × (nivel − 1) |
| Goblin | 25 EXP de personaje y 20 de maestría a repartir |
| Boss | 200 EXP de personaje y 120 de maestría a repartir |
| Punto de personaje | +5 vida, +2% daño o +2 armadura |
| Punto de maestría | +2,5% daño o +1,5% velocidad |
| Velocidad final | Entre 0,5× y 1,4× |
| Probabilidad de arma por goblin | 25% |
| Pesos T1 a T5 | 70, 25, 5, 0, 0 |

Cada tier por encima de T1 aporta +8% de daño. Los modificadores de variante se multiplican por `1 + 0,25 × (tier − 1)`.

| Variante | Modificadores en T1 |
| --- | --- |
| Equilibrada | Sin modificadores adicionales |
| Coloso | +10% daño, +10 vida, −8% velocidad |
| Duelista | +10% velocidad, −6 armadura |
| Guardián | +8 armadura, −5% daño |

El daño suma contribuciones de personaje, ejemplar y maestría, con un mínimo de 0,1×. La armadura positiva usa `daño × 100 / (100 + armadura)`; la negativa aumenta daño proporcionalmente. El divisor es configurable. El tier aporta un beneficio que puede compensar parte de la penalización de una variante.

## Combate y equipamiento

Vida y armadura suman los efectos de las dos armas equipadas y permanecen al alternar. Cambiar equipo o subir vida máxima conserva la vida actual, limitada al nuevo máximo si disminuye: no cura gratuitamente. Al comenzar un intento se aparece con la vida máxima efectiva.

Daño y velocidad pertenecen al ejemplar que ejecuta la acción y se capturan al iniciarla. Flechas y áreas conservan daño y familia de origen al alternar. La velocidad acelera fases ofensivas y ventanas de combo, y ajusta el cooldown básico. No acelera cooldowns de habilidades, parry, dash, vuelo de proyectiles ni pulsos de áreas. Las animaciones siguen el reloj del combate. No hay modificaciones de alcance.

## Persistencia

El perfil pasa a versión 2. Una versión 1 válida migra conservando IDs, ejemplares, ranuras y recompensas. Sus armas pasan a T1 Equilibrado y la progresión comienza en nivel 1. Los formatos futuros desconocidos siguen rechazándose.

El perfil guarda niveles, experiencia, puntos y rolls por ejemplar. `WeaponFamilyDefinition.progressionId` identifica la maestría; no cambiar después de distribuir guardados. Sin identificador explícito se usa el ID del arma como compatibilidad. Los ScriptableObjects contienen diseño, nunca puntos del jugador.

La victoria guarda experiencia y loot en una transacción. Si falla, no aplica ninguna parte y reintenta la misma recompensa mientras exista la escena; no vuelve a sortearla. Las recompensas no confirmadas no sobreviven al cierre de escena. Si el inventario está lleno conserva la experiencia y avisa que no agregó el arma.

## Próximos bloques

Esta entrega cubre niveles, maestría, tiers, atributos y guardado. Siguen pendientes el escalado persistente por región, streaming de chunks, heridas, contraataque tras parry, gemas y transmog. La tienda de skins requiere definir plataforma y catálogo.

## Pruebas

`Tools/InventoryCoreChecks` verifica integridad, recuperación, migración y límites del perfil. `ProgressionChecks.RunBatch` verifica integración jugable y captura las pantallas de progresión y armas con un perfil de prueba. `InventoryChecks.RunBatch` mantiene la regresión de inventario.
