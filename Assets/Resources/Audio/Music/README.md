# Musica de Mismo

Dos composiciones instrumentales originales generadas con `Tools/Music/compose.py`:

- `Exploration.wav`: 76 BPM, 50.53 segundos; acordes suaves y arpegios.
- `Combat.wav`: 120 BPM, 32 segundos; arpegios rapidos y percusion.

Ambas pistas son estereo y tienen colas circulares para repetirse sin cortes.
Para cambiar las pistas, abrir Mismo > Audio > Configurar sonidos, sección Música.
Estas dos composiciones quedan como pistas generales iniciales; los biomas y pueblos
usan las asignaciones del catálogo GameSounds.

`PlayerMusic` se agrega al jugador automáticamente. La pista de combate se activa solo cuando un BossController vivo y activo está peleando con ese jugador. Al derrotarlo o abandonar la pelea vuelve la música de la zona. Los enemigos comunes mantienen la música del bioma o pueblo. Las transiciones duran 1,5 segundos y usan el grupo Music del mezclador.
