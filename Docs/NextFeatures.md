# Próximo bloque de implementación

Estado: primera implementación de chunks de exploración, escalada de troncos con alto consumo de stamina y contrato de habilidad especial entregada. Ver [Exploration.md](Exploration.md). Biomas, encuentros exteriores, niveles mínimos por región y efectos especiales concretos siguen pendientes.

Solicitado después de corregir las animaciones del goblin y del jefe y los nombres visibles de familias de armas.

## Mundo procedural durante la exploración

Generar chunks en ejecución a medida que el jugador explora. Usar una semilla y coordenadas estables para que volver a un chunk no cambie su terreno; conservar por separado los cambios persistentes. Limitar la cantidad de chunks activos y distribuir el trabajo entre cuadros.

Al descubrir una región por primera vez, guardar su nivel mínimo como nivel de personaje en ese momento más un incremento configurable. No recalcular ese mínimo al cargar otros chunks de la misma región. El nivel de sus monstruos sigue siendo el máximo entre ese mínimo y el nivel actual del personaje.

## Árboles escalables

Confirmado: el personaje debe poder treparlos con un consumo alto de stamina. La primera implementación consume 35 puntos por segundo, incluso al quedarse agarrado, y obliga a soltar al agotarse.

## Habilidad especial intercambiable

Preparar una habilidad configurable mediante ScriptableObjects, independiente de su origen: todavía no está decidido si la otorga un cinturón, un alma o una mascota.

Conservar el dash como habilidad inicial. Separar activación, condiciones, duración, enfriamiento y finalización de la ejecución del efecto. Permitir incorporar efectos como escudo, robo de vida, flotación e invisibilidad sin que todos deban comportarse como un desplazamiento. Cambiar la fuente o morir debe limpiar los efectos temporales.

La incorporación y el balance de esos efectos se hará al definir cada habilidad; esta lista no los da por implementados.
