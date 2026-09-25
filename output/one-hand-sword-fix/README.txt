Espada a una mano

Causa: Sword.asset tenía DualSwords en el campo family. ComposeWeapon eliminaba correctamente la segunda pieza al dejar la mano vacía, pero conservaba esa familia base incorrecta.

Corrección: family apunta a OneHandSword. dualSwordFamily y swordShieldFamily conservan sus referencias. El editor identifica el campo como Familia base (arma sola) y advierte si coincide con una combinación.

Verificación: referencias de los tres assets; compilación runtime y editor; 12 comprobaciones de manos en Play Mode del proyecto aislado con el assembly real del jugador. Se verifican habilidades Q/E/R a una mano, segunda espada, escudo, dos conjuntos independientes y restauración de una mano al retirar la secundaria. El arranque aislado registra ausencia de RuntimeAssetCatalog en sus servicios globales; las pruebas de composición usan su catálogo explícito y terminaron correctamente. No equivale a una prueba de la escena completa del juego.

OpenWound: el área se configura en actions[0], RepeatedStrikeAction. forward = 1 m desplaza el centro al frente; radius = 0,7 m define la esfera. El borde frontal está a 1,7 m del origen del personaje, a la altura del golpe. range = 15 no determina el volumen de esta acción. Sus valores no se modificaron; se añadieron tooltips explicativos.
