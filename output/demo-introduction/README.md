# Demo de introducción

Abrir `Assets/Scenes/DemoIntroduction.unity` o usar **Mismo → Demo → Crear y abrir introduccion**, y entrar en Play.

La demo contiene una toma inicial de cámara, guía modal con resaltados del HUD, seis goblins en cinco encuentros, cambio al arco, Focus y pueblo con dos contactos de misiones. La geometría y los habitantes son provisionales. Las prácticas avanzan al derrotar enemigos; no certifican el dominio de parry o esquiva. El inventario es temporal.

## Evidencia

- `checks.txt`: 34 comprobaciones de Play Mode aprobadas.
- `combat-regression.txt`: las 94 comprobaciones existentes de combate/feedback pasaron. El conteo está también en `unity-build.log`.
- `player-compilation.txt`: compilación runtime Windows aprobada.
- `content-build.txt`: nueve secuencias cargadas desde contenido construido.
- `health.png`, `focus.png`: capturas reales del Game View revisadas después de corregir el orden del oscurecimiento.
- `organization.txt`: incidencias preexistentes de rutas de arte, ajenas al tutorial.
- `build.txt`: Unity devolvió `Succeeded`, 788177190 bytes y **1 error** por el verificador de organización. Hay ejecutable de prueba, pero la build no está limpia.
- `player-smoke.log`: arranque de 15 segundos sin gráficos; sin excepciones de gameplay, con errores de conexión de telemetría de Unity en el entorno restringido. No constituye validación visual del ejecutable.

El ejecutable está en `.validation/Organization/output/demo-introduction/Windows/MismoDemo.exe`. La guía de edición completa está en `Docs/Demo-introduccion.md`.
