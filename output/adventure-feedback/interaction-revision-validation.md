# Revisión de interacción y avisos — 2026-09-23

Validado con Unity 6000.6.0f1 en `.validation/AdventureCheck`, sin usar la partida ni modificar escenas del proyecto principal.

- Compilación de los scripts de juego y editor: correcta.
- Tab alterna armas sin aviso; generar un drop no crea aviso y el nombre del arma está disponible en el cartel cercano.
- F selecciona una sola acción y la más cercana.
- Botín persistente: no entra automáticamente a la mochila; guardado fallido y mochila llena conservan el drop; recoger no duplica objetos.
- La espera posterior al combate permite recolección/recogida, mientras los cambios de equipamiento siguen bloqueados.
- En Play Mode, un recurso grande puede alcanzarse por los cuatro lados, las paredes bloquean la recolección y se respeta el alcance hasta su superficie.
- EXP y nivel animan, se suspenden durante pausa y terminan correctamente; una victoria silenciosa no activa el panel de avisos.
- Captura de la interfaz revisada visualmente: nombre junto a F y aviso normal sin encabezado.

No se reprodujo la escena exacta de las capturas del usuario ni se generó una build completa. El registro contiene una excepción del indexador UnityEditor.Search durante el arranque; las pruebas terminaron correctamente.
