# Trail procedural de armas

## Uso
Mismo > Armas > Taller de poses (pestaña Taller de armas). Elegir arma/perfil y abrir la vista de ajuste aislada.
En «Trail procedural · melee», activar Estela y Cinta procedural. «Ajustar puntos a los límites de la pieza» propone extremos sobre el eje más largo del modelo y activa el efecto. Ajustar manualmente Base y Punta para cubrir solo la hoja, excluyendo el mango.
«Editar puntos en Scene» permite elegir/arrastrar cada extremo, con Undo. La segunda pieza tiene extremos independientes y activación propia.
Elegir el golpe del combo y «Usar clip del golpe seleccionado», o un clip manual. Reproducir en bucle o arrastrar el tiempo del clip. La opción «Solo ventana de impacto» respeta los porcentajes del golpe seleccionado; el ritmo de reproducción respeta su duración.
Ajustar persistencia, estrechamiento de cola, colores y material opcional. Guardar trail del perfil. Los perfiles compartidos afectan a todas sus armas.

## Implementación
Cinta de malla entre dos puntos muestreados en coordenadas mundiales; historial limitado a 192 muestras, colores/alpha por antigüedad, UV para material opcional, cola estrechada. No modifica colisiones ni daño. Mantiene estelas simples anteriores cuando Cinta procedural está desactivada.
WeaponTrailPresentation evalúa después de WeaponPresentation, con puntos de los visuales realmente equipados. Emite durante ventana de impacto del combo o fase activa de otras habilidades melee. Limpia en cambio de arma/perfil, nuevo golpe, desactivación o muerte; permite desvanecimiento al finalizar normalmente. No une trazos separados. Libera mallas/materiales al destruirse.
El taller reconstruye el historial del clip al mover el tiempo, dentro de la escena de previsualización aislada. El material predeterminado reutiliza CombatParticles, ya referenciado por el catálogo del juego: no introduce nuevas cargas ni shaders que incluir en la build.

## Validación
Runtime y editor completos compilados sin errores (advertencias preexistentes en logs).
8 checks en Unity aislado: geometría, coincidencia de punta, separación de trazos, expiración, retroceso temporal, límite de muestras, desactivación y render visible.
preview.png es un barrido sintético renderizado en Unity con el mismo generador de geometría y un shader de prueba de colores de vértices; no es captura del taller ni de la escena principal URP. Inspeccionada visualmente.
No se compró/importó el paquete de referencia ni se copiaron sus recursos. Implementación propia inspirada en la idea de weapon trails.
Referencia: https://assetstore.unity.com/packages/vfx/shaders/procedural-weapon-trails-337724
