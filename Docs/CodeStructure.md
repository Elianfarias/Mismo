# Estructura de código

Esta estructura cubre la milestone de movimiento y su equipamiento mínimo.

## Carpetas

- `Assets/Art`: assets visuales y de audio, sin lógica de gameplay.
- `Assets/Data`: instancias configurables, incluidos los ScriptableObjects.
- `Assets/Prefabs`: prefabs listos para utilizar en escenas.
- `Assets/Scenes`: escenas del proyecto.
- `Assets/Scripts/Gameplay/Player`: código runtime de la milestone.
- `Assets/Scripts/Gameplay/Player/Editor`: herramientas exclusivas del editor.

Dentro de `Player`, el código se agrupa por responsabilidad:

- `Input`: traduce Input System a intención del jugador.
- `Movement`: locomoción, rotación, salto, gravedad y solicitudes de desplazamiento controlado.
- `Dash`: contrato de dash y comportamiento del cinturón.
- `Equipment`: contratos mínimos de arma y cinturón, definiciones de datos y loadout equipado.
- `Combat`: salud, recepción/aplicación de daño, invulnerabilidad, combo básico, ventanas de hitbox y reacción de muerte desacopladas.
- `Cooldowns`: servicio genérico por clave para reutilizar temporizadores entre armas, habilidades y cinturones.
- `Camera`: seguimiento y órbita en tercera persona.
- `Presentation`: presentación y feedback provisional de movimiento y combate que leen el estado del jugador.

## Assemblies

`Mismo.Gameplay.Player` compila el runtime de la milestone y referencia explícitamente `Unity.InputSystem`.

`Mismo.Gameplay.Player.Editor` compila solamente dentro del editor y puede depender del assembly de runtime. Las herramientas de editor no entran en una build del juego.

Los builders temporales de escenas también viven en este assembly. Deben poder ejecutarse desde el menú `Mismo/Prototype`, evitar duplicados, registrar sus cambios en `Undo` y ofrecer una forma simple de eliminar lo generado.

Los sistemas futuros obtendrán su propio assembly cuando exista una dependencia real. No se crearán assemblies vacíos para combate, inventario, enemigos o networking.

## Namespaces

El namespace raíz del runtime es `Mismo.Gameplay.Player`. Cada carpeta funcional agrega su segmento correspondiente.

Ejemplos:

```csharp
namespace Mismo.Gameplay.Player.Movement { }
namespace Mismo.Gameplay.Player.Dash { }
namespace Mismo.Gameplay.Player.Camera { }
namespace Mismo.Gameplay.Player.Equipment { }
```

El código exclusivo del editor utiliza `Mismo.Gameplay.Player.Editor`.

Las referencias deben apuntar desde presentación y coordinación hacia contratos o componentes de gameplay. Ningún comportamiento concreto de cinturón será conocido directamente por `PlayerController`.
