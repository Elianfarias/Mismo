# Validación del efecto de loot

Unity 6000.6.0f1, copia aislada `.validation/AdventureCheck`.

- Scripts compilados y shader URP renderizado sin errores.
- Cinco colores de tier y clasificación de componentes/consumibles comprobados. Un drop mixto con arma conserva la prioridad de su tier.
- Captura revisada visualmente: de izquierda a derecha T1, T2, T3, T4, T5, componente y consumible.
- Efectos sin colliders activos.
- Bundle Windows construido y recargado con InventorySettings, material y shader referenciados. No equivale a una build completa del juego.
- ProjectOrganizationChecks.Run ejecutado: la copia parcial informa referencias a arte/audio que no se copiaron. No es una aprobación global de organización del proyecto principal.
- No se modificaron escenas ni partidas. Falta comprobar la presentación dentro de la escena real del usuario.

Fuentes de diseño: https://playwonderlands.2k.com/game-guide/gear-loot/rarity/ y https://news.blizzard.com/en-us/article/23574266/itemization-in-diablo-immortal
