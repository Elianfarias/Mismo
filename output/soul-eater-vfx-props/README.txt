Soul Eater: VFX, seguimiento, derribo y cheat de fase

Configuración: Assets/Data/Enemies/DragonBosses/SoulEater_PhaseOne.asset
- Aliento · ambas fases > Breath Prefab: VFX_Fire_Green.
- Phase Two Breath Tracking Speed: 120 grados/segundo. 60 sigue más lento; 180, más rápido.
- Breath Turn Speed: giro del cuerpo (se conservó el valor del usuario: 65).
- Breath Propagation Speed: avance del fuego (se conservó el valor del usuario: 20).
- Breath Particle Budget: máximo total de partículas del aliento (72).
- Vegetación, rocas y decoración · ambas fases > Crush World Props / Prop Break Reach.
- F8 activa cheats; F7 inicia el rugido de transición a la fase 2 del boss invocado.

El efecto reutiliza una instancia del prefab, conservando material y animación de partículas.
La distribución se adapta al volumen de daño, desde el suelo hasta la boca y hacia delante.
El prefab fuente y los charcos VFX_GroundFire_Circle_Green no fueron modificados.

El contacto al caminar/cargar elimina árboles, madera muerta, rocas, hierbas, arbustos,
flores y decoración catalogada como cerca, ruina o landmark. Incluye vegetación sin collider.
Se conservan edificios, pozos, aldeas y zonas de misiones, altares y faros.
La destrucción se guarda antes de retirar los objetos; un fallo de escritura no destruye nada.
Se mantiene la lista antigua de árboles de partidas existentes y se agrega una lista tipada
para los demás objetos. El contacto no reconstruye terreno ni crea un cráter.

Validación en Unity 6000.6.0f1, proyecto aislado .validation/DragonRuntime:
- 61 checks de cheats (incluye fase 2 con un golpe activo, repetición e interrupción de aliento).
- 88 comprobaciones de fase 1, VFX, colisión, navegación y animación.
- 41 comprobaciones de fase 2, seguimiento regulable, vuelo y destrucción persistente.
- 84 comprobaciones de contacto con props, fallos de guardado, protección y recarga.
- 100 comprobaciones de reglas y 49 del arco tutorial en juego.
- Ver build.txt y organization.txt para el resultado de compilación y organización.
- La organización presenta las mismas 117 incidencias anteriores, sin diferencias con el informe previo.

La escena abierta del usuario no se modificó. No se hizo commit ni push de estos cambios.
