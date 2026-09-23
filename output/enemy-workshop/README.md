# Taller de enemigos

Menú: Mismo > Enemigos > Taller de armas y animaciones.
Seleccionar un prefab enemigo editable (no el FBX/modelo importado directamente), abrir vista de ajuste aislada.

## Equipo opcional
Principal y secundaria aceptan una WeaponDefinition existente o un prefab visual propio. Ambos campos vacíos dejan esa pieza sin arma. El prefab visual tiene prioridad si se asignan ambos.
El agarre se almacena en EnemyEquipment en el prefab enemigo, separado del perfil del jugador. Elegir mano Humanoid o ruta de hueso (Generic), mover con W y rotar con E. Se admite Undo durante el ajuste.
Ocultar/mostrar arma integrada selecciona renderers por ruta relativa al Animator.
Guardar equipo modifica únicamente el componente EnemyEquipment del prefab original; no guarda toda la escena de previsualización ni modifica el arma fuente.
Controller opcional permite usar un Animator Controller/Override compatible con los parámetros de la IA existente.
Los colliders de las piezas equipadas se desactivan: es equipo visual; no cambia automáticamente el volumen, daño, root motion ni reglas de los ataques enemigos.

## Animaciones
Goblins: slash/charge o lista attacks si está definida.
Criaturas: misma lista de ataques y muestreo de clips de preparación, ataque, recuperación y bucle conforme al driver de criaturas.
Jefes: frontSlash, overheadSmash, straightCharge.
Cada ataque muestra sus datos existentes (clip, máscara, tiempos, rangos de animación, daño y volumen). Se editan en su ScriptableObject compartido y tienen su propio botón de guardar. El componente de equipo no es necesario para editar estos assets.
Probar otro clip permite previsualizar sin asignar. Slider de progreso y reproducción en bucle; volumen de ataque visible en Scene.
La herramienta advierte sobre la diferencia Humanoid/Generic, pero no convierte rigs automáticamente. Otros controladores específicos permiten ajustar equipo/probar clips; requieren su propio editor de ataques.

## Validación
Compilación de assemblies runtime de enemigos y editor completa, sin errores nuevos.
13 comprobaciones Unity aisladas usando EnemyEquipment y WeaponAttachmentPose reales: desarmado, principal opcional, segunda pieza opcional, anclaje, seguimiento, origen sin modificar, ocultación y restauración, controlador opcional y restauración, eliminación de equipo. Servicios ajenos sustituidos. No es una prueba visual del taller completo ni una partida con los prefabs de producción.
No se modificaron prefabs ni configuraciones de enemigos existentes al implementar la herramienta. No se añadieron cargas globales ni arte.
