# Día, tarde y noche

El mundo aplica automáticamente los cinco cubemaps EXR (tiras de seis caras) de Art/Texture/Skybox/Textures. El cielo se mezcla gradualmente entre las fases; la luz solar, la luz nocturna, el ambiente y el color de la niebla acompañan el horario.

## Configuración en Unity

Seleccionar `Assets/Data/World/DayNightSettings.asset` en Project y editar en Inspector:

- **Running**: activa o detiene el reloj.
- **Cycle Minutes**: minutos reales por día completo (30 por defecto).
- **Starting Hour**: hora inicial para partidas nuevas o guardados antiguos sin hora (9 por defecto).
- **Sky Rotation**: gira los cubemaps.
- **Sun Azimuth**: orientación del recorrido de la luz solar.
- **Moon Intensity / Moon Color**: iluminación nocturna.
- **Phases**: horarios, textura, exposición, intensidad y colores de cada fase. Los horarios usan 0–24; usar horas distintas y al menos una textura válida.

Las fases predeterminadas incluyen noche, amanecer, mañana, día, mediodía, tarde y atardecer. Se utilizan Deep Midnight, Cotton Candy Morning, Warm Sunrise Glow, Cloudy Bright Day y Tropical Noon.

En Play, el componente `DayNightCycle` del objeto `Voxel Region…` permite cambiar **Hour** para probar rápidamente otra hora; desactivar Running para mantenerla fija. No es necesario asignar un skybox manualmente en Lighting.

La hora se guarda con los checkpoints del mundo, incluso permaneciendo quieto, y se recupera al Continuar. El tiempo no avanza mientras la partida está cerrada. Los guardados existentes siguen siendo compatibles. El ciclo visual no modifica las reglas de aparición de monstruos.

El menú `Mismo > World > Configure day and night` reconfigura los importadores y crea el recurso si falta; conserva las fases ya editadas.

## Validación

Unity 6000.3.11f1, copia aislada del proyecto: compilación, 96 muestras de interpolación, vuelta por medianoche, reloj detenido, compatibilidad de guardados sin hora, serialización de hora y rechazo de valores inválidos. Capturas de cuatro fases en `Docs/Validation/DayNight`.

