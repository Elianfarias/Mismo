# VFX de armas

Seis prefabs originales de partículas, con brillo suave y geometría facetada. No añaden daño, sonido, cámaras ni hit stop. Se reutiliza el ciclo de vida de `WeaponAbilityVfx`.

| Prefab | Aspecto | Integración |
| --- | --- | --- |
| BowCharge | Núcleo celeste, anillos y fragmentos convergentes | Preparación/carga de BowShot y BowPower |
| BowRelease | Onda celeste y fragmentos al disparar | Ejecución de BowShot y BowPower |
| BladeCharge | Concentración ámbar | Preparación de Rompeguardia y modificador Corte cargado de M1 |
| BladeRelease | Destello ámbar y onda breve | Estocada, Rompeguardia y liberación de Corte cargado |
| ParryHalo | Dos halos dorados | Fase activa de Parry |
| MasteryPowerFlare | Onda violeta y fragmentos | Potencia de BowPower y Rompeguardia desde rango 1; Entrada y remate de Estocada desde rango 0 |

BowShot es el ataque básico del arco: su efecto se ve sin desbloquear habilidades. Los otros efectos respetan los desbloqueos y la selección de modificadores existentes. Las partículas de carga se retiran al soltar; los destellos terminan en 0,4–0,45 s. Cancelar, morir o cambiar de arma limpia las instancias.

Los sockets se añadieron a los prefabs visuales del arco, la espada y la espada del guardián conservando sus GUID, mallas, materiales y poses. `bowstring` queda disponible para efectos sobre el arco; `blade` y `tip` identifican la hoja y punta de ambas espadas. Las habilidades compartidas funcionan con las dos espadas. Los anillos miran hacia la cámara para mantenerse legibles mientras gira el arma.

BowShot y BowPower ahora usan **Anchor → AboveHead** y **Face Camera**: la carga y liberación acompañan al personaje por encima de su altura, para evitar que el cuerpo tape el arco. El sistema también admite anclaje a la raíz del personaje (`Character`) y conserva `Weapon` como valor predeterminado. Los anclajes del personaje emiten una sola vez aunque se elija Both y no dependen de sockets. Esto no cambia el control de la cámara.

## Editar

- Prefabs: `Assets/Art/Prefabs/Weapons/AbilityVfx`.
- Materiales: `Assets/Art/Materials/Weapons/AbilityVfx`.
- Mallas: `Assets/Art/Meshes/Weapons/AbilityVfx`.
- Shader: `Assets/Art/Shaders/WeaponEnergy.shader`.
- Asignaciones: **Weapon Vfx** en los assets de habilidades y dentro de **Mastery Modifiers → power**.

Los prefabs contienen Particle Systems estándar: se pueden editar colores, cantidad, tamaño y forma desde el Inspector. El shader genera el brillo por coordenadas UV, sin texturas adicionales. Hay un máximo de 48 partículas por capa; cada efecto utiliza tres capas, sin luces dinámicas ni colisiones.

**Mismo → Armas → VFX → Crear e integrar efectos iniciales** crea sólo assets, sockets y asignaciones ausentes. No reemplaza ajustes de arte ya guardados. **Verificar efectos reales en Play Mode** usa una escena vacía temporal, no carga perfiles y restaura las escenas abiertas; se detiene si hay una escena sin guardar. **Verificar contenido compilado** compila scripts Windows y construye/carga un bundle siguiendo referencias de las habilidades a prefabs, mallas, materiales y shader.

Las imágenes de esta carpeta son renders anteriores de los modelos y partículas reales de Unity en una escena de previsualización. No son capturas de una partida ni muestran la nueva colocación sobre el personaje.

## Verificación realizada

La última pasada de espada y anclajes pasó 200 comprobaciones de componentes reales en Edit Mode (`output/progression-improvements/checks.txt`), incluyendo carga, ruptura de guardia, cadenas y VFX sobre el personaje. No se hizo una nueva revisión visual interactiva. Los informes siguientes corresponden a la integración inicial de los seis prefabs, antes del cambio de anclaje:

- `asset-checks.txt`: seis prefabs con materiales y mallas válidos; cinco habilidades con asignaciones a sockets existentes.
- `play-checks.txt`: 13 comprobaciones en Play Mode con los prefabs y habilidades reales. Carga del arco, emisiones, pausa a través de varios frames, liberación, expiración, Estocada, Parry, cancelación y muerte. Se restauró MainMenu y se retiró la escena temporal.
- `player-compilation.txt`: scripts de Windows compilados sin UNITY_EDITOR.
- `content-build.txt`: bundle Windows construido y cargado; las referencias de las cinco habilidades resuelven sus VFX, incluidos los modificadores, mallas, materiales y shader. Esto valida el contenido compilado; no es una build completa del juego.
- `organization-checks.txt`: el chequeo global sigue fallando por assets anteriores fuera de sus categorías. No informa problemas con los nuevos assets de VFX; no se reorganizó contenido ajeno.
