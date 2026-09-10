# Menú y audio para playtest

La escena inicial es `Assets/Scenes/MainMenu.unity`. Comenzar carga `VoxelRegion_7319`; Opciones muestra Música, Efectos e Interfaz; Salir cierra el ejecutable. Los controles se pueden editar en la jerarquía del Canvas. El texto MISMO es provisional.

Se incorporaron los cuatro scripts de audio del otro proyecto y su mixer. Los originales no se modificaron. VolumeSettings usa los parámetros reales VolumeMusic, VolumeSFX y VolumeUI, guarda preferencias Mismo.* y limita el rango de cada slider a 0–1. La curva es 10 log10(valor): 100 % = 0 dB, 70 % = −1,55 dB y 50 % = −3,01 dB. Cero aplica −80 dB. Es una curva deliberadamente suave en la parte alta, no una medición de sonoridad percibida. Se elimina el aumento heredado de +20 dB del subgrupo de enemigos.

AudioRuntime crea un único servicio persistente con fuentes de música, efectos e interfaz; también funciona al abrir directamente una escena de gameplay. Los sonidos de movimiento y combate existentes salen por SFX. No se añadieron pistas ni efectos nuevos: se pueden asignar clips a MusicController o solicitar reproducción mediante AudioEvents.

`Mismo.Menu.Editor.MainMenuBuilder.CreateAndCheck` verifica menú, sliders, preferencias, transición al juego y persistencia del servicio. `BuildWindows` genera una build Windows de menú + región. El menú está primero en Build Settings sin quitar las escenas existentes.

Validado en Unity 6000.3.11f1: 16 comprobaciones del menú aprobadas, compilación Windows completada (157,7 MB sin comprimir) y ZIP verificado. Capturas de Inicio y Opciones en Docs/Validation. La build incluye únicamente MainMenu y VoxelRegion_7319; los archivos de depuración DoNotShip se excluyeron del ZIP.
