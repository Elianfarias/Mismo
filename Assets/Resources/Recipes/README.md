# Crafting de expediciones

El banco separa las recetas en Preparación, Mejoras y Equipo básico. Las recetas son assets; no hace falta modificar el controlador para cambiar ingredientes, cantidades o resultados.

## Recetas iniciales

| Receta | Ingredientes | Resultado |
| --- | --- | --- |
| Ungüento | 2 rosas medicinales | Recuperación gradual de 40 de vida en 5 s fuera de combate; el daño cancela el efecto |
| Poción de curación | 3 rosas + 1 cristal arcano | 35 de vida inmediata, utilizable en combate; 20 s entre usos |
| Piedra de afilar | 3 piedras + 1 hierro | +15 % de daño al ejemplar activo durante 180 s; se aplica fuera de combate y no se acumula |
| Mejora inicial | 2 maderas + 3 piedras + 1 componente de monstruo | T1 a T2, receta anterior conservada |
| Mejora de hierro | 3 maderas + 5 hierros + 2 componentes | T2 a T3 |
| Mejora arcana | 4 hierros + 4 cristales + 3 componentes | T3 a T4 |
| Espada básica | 2 maderas + 4 hierros | Ejemplar nuevo T1 equilibrado |
| Arco básico | 5 maderas + 2 hierros + 1 componente | Ejemplar nuevo T1 equilibrado |

Son valores de prueba editables. No se añadió refinado intermedio, comercio ni fabricación de recompensas exclusivas del boss.

## Materiales y distribución

Piedra (MAT-03) continúa en las rocas comunes. Hierro (MAT-06) y cristal arcano (MAT-07) tienen objetos e iconos propios y tablas independientes en las menas existentes. El cristal tiene menor peso de distribución. `distributionWeight` permite cambiar frecuencia o usar cero para desactivar una variante. Recompensa, cantidad, tiempo de extracción y regeneración siguen en cada definición. Cualquier enemigo puede usar estos mismos objetos en su tabla de botín.

La migración inicial clasifica las variantes con colores azules en su paleta como cristal y el resto como hierro. Esa asignación es un punto de partida editable, no una regla de runtime. El importador de expansión solo cambia las tablas originales de piedra; conserva las ya configuradas en ejecuciones posteriores.

## Consumo y persistencia

El botón Usar aparece en objetos consumibles de la mochila y muestra el motivo cuando está bloqueado. La poción comparte su plazo de recuperación con otros consumibles que configuren `useCooldownSeconds`. Vida llena no consume una unidad. El ungüento mantiene su comportamiento anterior.

El afilado se vincula al ID del ejemplar activo, no a toda la familia ni a la segunda ranura. Se conserva al cambiar de arma, pero solo aporta daño al ejemplar preparado. No se puede renovar ni aplicar otra piedra mientras dura el efecto. El tiempo de efecto y de recuperación usa el reloj de partida: pausa real y juego cerrado no lo adelantan; el vínculo se conserva al cargar y al reaparecer.

La fabricación valida ingredientes, elegibilidad y espacio después de gastar los ingredientes. Cada arma creada recibe identidad nueva y no se equipa automáticamente. Un fallo de guardado no entrega producto o bonificación ni descuenta ingredientes o consumibles. Los proyectiles y habilidades ya iniciados conservan su multiplicador capturado al ejecutarse.

## Configuración

`CraftingRecipe.weaponResult` fabrica un arma T1. `upgradeWeapon` mantiene las transiciones de tier; `result` fabrica un material o consumible. Se usa un único tipo de resultado por receta. `MaterialDefinition` configura curación instantánea o gradual, uso en combate, recuperación entre usos y bonificación temporal. Si se cambian valores, actualizar también las descripciones en las tablas español e inglés.

**Mismo → Crafting → Add expedition recipes** agrega contenido faltante y conserva assets existentes. **Create surface defaults** es una herramienta histórica de restablecimiento: vuelve a dejar únicamente las dos recetas de ejemplo en GatheringSettings; no usarla para actualizar esta expansión.

## Verificación

97 comprobaciones del núcleo de inventario y guardado y 33 comprobaciones de la expansión en Play Mode aislado aprobadas. Incluyen fabricación de consumibles y armas, transiciones de tier, fallo de guardado sin consumo, poción en combate, cooldown persistente, afilado sin acumulación y sin bonificar la secundaria, vencimiento y capturas de las tres categorías. Player y Player.Editor compilan contra Unity 6000.6. Las capturas están en `output/crafting`. Falta balancear costos y frecuencias recorriendo el mundo real.
