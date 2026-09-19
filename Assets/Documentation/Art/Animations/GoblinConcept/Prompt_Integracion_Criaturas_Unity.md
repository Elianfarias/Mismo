# Integrar jabalí, araña y gólem voxel en mi proyecto Unity

Actuá como un desarrollador senior de Unity y C#. Quiero que IMPLEMENTES la integración de los tres paquetes adjuntos dentro de mi proyecto existente. No te quedes en un análisis o plan: inspeccioná lo necesario, implementá, conectá los assets y verificá el resultado. No reemplaces la arquitectura del proyecto por un sistema paralelo.

## Objetivo y dirección artística

Mi juego es un action RPG 3D de fantasía voxel, con cámara de tercera persona alejada. Los enemigos deben verse y leerse bien desde la cámara real del juego. Estoy incorporando criaturas progresivamente al mundo y necesito prefabs reutilizables, variantes y configuración editable.

El gólem es un boss único de aproximadamente 5 metros. Su primera entrega fue rechazada: los movimientos parecían intentar simular realismo, las muñecas se veían mal, el núcleo parecía una boca y la roca era pequeña. La versión actual corrige eso: núcleo pequeño sobre pecho cerrado, puños rígidos, roca grande y animaciones de poses marcadas con anticipación, golpe rápido y pausa. Preservá ese estilo; no agregues balanceos procedurales ni transiciones largas que diluyan los ataques.

## Paquetes que debés usar

1. `Jabali_Voxel_Rig_Animaciones.zip`
2. `Arana_Voxel_Rig_Animaciones.zip`
3. `Golem_Boss_Estilizado_V3.zip`

El tercer ZIP reemplaza al gólem original y a la revisión parcial V2. No mezcles sus animaciones con `Golem_Boss_Rig_Animaciones.zip` ni uses `Golem_Revision_V2.zip` como paquete completo. V3 conserva el barrido aprobado y contiene el conjunto actualizado de siete clips.

Si los archivos llegan renombrados, identificá cada paquete por su contenido. Si falta uno, avanzá con los disponibles e informá exactamente cuál falta. Nunca supongas que un clip o una textura existe: comprobalo.

Los ZIP contienen fuentes `.blend`, FBX, paletas, vistas y documentación. Usá los FBX como assets de Unity. Conservá los `.blend`, scripts y documentos fuera de `Assets` en la ubicación de fuentes del proyecto; no provoques importaciones automáticas de Blender. No ejecutes generadores de geometría para una integración que solo requiere importar los modelos.

## 1. Inspección dirigida del proyecto

Leé las instrucciones del repositorio. Identificá la versión de Unity, el render pipeline, las convenciones de carpetas y un enemigo existente que funcione como referencia. Revisá únicamente los sistemas relacionados:

- Prefabs de enemigos, configuración y ScriptableObjects.
- Vida, daño, ataques, muerte, recompensas y facciones/layers.
- IA, detección, movimiento, navegación y límites de persecución.
- Animator, parámetros y eventos de combate.
- Registro de criaturas, generador de encuentros o spawner, si existen.
- Pooling y autoridad de red, solo si el proyecto los utiliza.

Diferenciá lo realmente implementado de lo que aparece solo en documentos. Explicá brevemente qué sistemas vas a reutilizar y continuá. Evitá refactors ajenos a esta integración, cambios de pipeline y nuevas dependencias innecesarias.

## 2. Importación y materiales

Extraé cada paquete en su propia carpeta de trabajo y mantené separados sus assets. Jabalí y araña incluyen paletas con nombres iguales, como `Standard_Palette.png`: no deben sobrescribirse ni cruzarse sus referencias.

Prepará materiales compatibles con el pipeline existente. Usá las paletas suministradas, conservá la apariencia voxel y comprobá que ninguna malla quede rosa, blanca o con colores de otra criatura. En las paletas de una fila evitá el filtrado y la compresión que mezclen colores vecinos; verificá el resultado real después de importar.

Usá rig Generic, identificá correctamente la raíz y preservá las normales planas. Verificá escala, orientación y apoyo en el suelo. Los FBX se exportaron con conversión de ejes, pero debés comprobar su resultado en esta versión del proyecto. No corrijas errores de orientación rotando indiscriminadamente toda la jerarquía de gameplay.

Preferí una raíz de gameplay con colliders/controlador y un hijo visual con el modelo y Animator. Conservá GUID y referencias de assets existentes. Si creás herramientas de Editor, que sean acotadas e idempotentes y utilicen las APIs de Unity; no edites YAML de prefabs a ciegas.

## 3. Contenido esperado

### Jabalí

FBX: `Boar_Standard.fbx`, `Boar_Dark_Fur.fbx`, `Boar_Forest_Moss.fbx`.

Clips: `Idle`, `Walk`, `Run`, `Charge`, `Attack`.

Creá un prefab base y tres variantes visuales, compartiendo comportamiento cuando corresponda. Integrá detección, persecución, ataque cercano y embestida usando los sistemas existentes. La embestida debe tener anticipación, dirección definida y finalización; evitá que siga rotando instantáneamente hacia el jugador durante el golpe.

### Araña

FBX: `Spider_Standard.fbx`, `Spider_Forest_Moss.fbx`, `Spider_Dark_Cave.fbx`, `Spider_Albino.fbx`.

Clips: `Idle`, `Walk`, `Run`, `Jump`, `Attack_Bite`, `Spit_Web`.

Creá un prefab base y cuatro variantes visuales. Reutilizá ataque cercano y los sistemas de salto/proyectiles si existen. El salto contiene elevación visual de cuerpo y patas: coordiná esa elevación con el movimiento físico para no sumar dos trayectorias completas.

`Spit_Web` contiene el gesto y el hueso `Web_Origin`, pero no incluye telaraña, partículas ni un proyectil. No presentes esos efectos como disponibles. Si el proyecto ya tiene un sistema de proyectiles/ralentización, conectalo de forma configurable; si no existe, dejá esa habilidad deshabilitada y señalada como pendiente, manteniendo funcional la mordida. No inventes un sistema grande de estados alterados para completar esta importación. Lo mismo aplica al veneno.

### Gólem — versión estilizada V3

FBX: `Forest_Golem_Stylized.fbx` y `Rock_Projectile.fbx`.

Clips: `Idle`, `Walk`, `Pick_Up_Rock`, `Throw_Rock`, `Ground_Slam`, `Sweep_Attack`, `Recover`.

Creá un único prefab de boss con una sola paleta. Medí su altura importada contra el jugador; el objetivo es aproximadamente 5 m, conservando las proporciones compactas. La roca mide aproximadamente 2 m y se usa con ambas manos.

Conectá el agarre al hueso `Rock_Carry`. La roca debe permanecer alineada durante la carga y separarse una sola vez al lanzar. Usá la trayectoria del controlador/proyectil del juego después de la liberación. La trayectoria visual de Blender no sustituye la física de Unity. No uses el antiguo anclaje de una mano `Rock_Grip` para esta nueva acción.

Implementá, reutilizando la infraestructura de combate:

- Recoger y lanzar roca a distancia, como una secuencia coherente.
- Golpe al suelo en área frontal/cercana.
- Barrido lateral con ventana de daño acotada.
- Recuperación sin ataques, como ventana de castigo.

No habilites daño durante toda la animación. Cada acción debe separar anticipación, ventana activa y recuperación. Ajustá el alcance con el tamaño real del boss y su animación, no con valores copiados del jabalí. Evitá daño múltiple al mismo objetivo dentro de un único barrido o impacto, salvo que el sistema existente lo requiera explícitamente.

El núcleo visible no implica invulnerabilidad del resto del cuerpo por defecto. Solo reutilizá una mecánica de punto débil si ya existe o si es necesaria para el comportamiento definido del boss; dejá cualquier decisión adicional de diseño claramente diferenciada.

## 4. Animator y sincronización de gameplay

Los nombres importados pueden incluir prefijos como `Rig|Rig|...`. Inventariá los clips y mapealos a sus nombres semánticos sin asumir coincidencias literales.

Usá los parámetros y el patrón de Animator del proyecto. Si es posible, compartí la lógica entre variantes con Animator Override Controllers o la solución ya establecida; reutilizá Avatars únicamente después de verificar compatibilidad de esqueletos y bind poses.

Bucles: jabalí `Idle/Walk/Run/Charge`; araña `Idle/Walk/Run`; gólem `Idle/Walk`. Los demás son acciones de una ejecución. No suavices indiscriminadamente los clips del gólem: preservá sus pausas y la velocidad del impacto. El Root permanece fijo; el desplazamiento pertenece al controlador, sujeto a las convenciones reales del proyecto.

Leé los `LEEME.txt` y los archivos de eventos del paquete. Los marcadores de Blender y los JSON NO crean Animation Events de Unity automáticamente. Conectá explícitamente los tiempos mediante el sistema de eventos o de ventanas de ataque del proyecto. Verificá la estructura de cada JSON: no asumas que los paquetes tienen el mismo esquema.

El `.blend` del gólem incluye acciones `PROP_...` de previsualización de roca. No son ataques adicionales del personaje ni deben aparecer como estados de su Animator.

Los paquetes no proporcionan clips dedicados de muerte ni de recibir daño. Usá la solución existente compatible o una salida simple coherente con el juego, sin inventar que esos clips fueron entregados ni reutilizar animaciones incompatibles de otro esqueleto.

## 5. Prefabs, configuración y aparición

Entregá ocho prefabs/variantes seleccionables: tres jabalíes, cuatro arañas y un gólem. Usá colliders sencillos que se ajusten al volumen útil; evitá MeshColliders animados para cada voxel. Separá detección, colisión física y zonas de daño conforme a las convenciones del proyecto.

Las estadísticas, distancias, cooldowns y probabilidades deben ser editables. Tomá como punto de partida enemigos comparables existentes; documentá valores provisionales. No asignes mecánicas de élite o cambios de dificultad solo por cambiar el color.

Si existe registro/spawner procedural, registrá los nuevos prefabs para que puedan aparecer realmente. Verificá bioma, suelo/navegación, distancias al jugador, densidad y límites simultáneos. El gólem es un boss único: usá un encuentro o punto de aparición dedicado, no la tabla ordinaria de criaturas comunes; evitá duplicados en el mismo encuentro. Si no hay un spawner existente, conectá prefabs a una escena de prueba sin construir un generador de mundo nuevo.

No rompas escenas existentes. Prepará una escena de prueba o área de prueba siguiendo el flujo actual, con las criaturas visibles y suficientes distancias para ataques y proyectiles. Si el proyecto tiene multijugador, mantené daño, spawn y proyectiles bajo su autoridad existente; no introduzcas eventos duplicados por cliente.

## 6. Verificación y entrega

Comprobá lo que puedas ejecutar realmente:

- Compilación y ausencia de componentes o referencias faltantes.
- Materiales, paletas, escala, orientación y apoyo de las ocho variantes.
- Detección, movimiento, ataque, daño y muerte/despawn.
- Ventanas de impacto, interrupción de ataques y cancelación al morir.
- Roca: agarre con ambas manos, liberación única, colisión y limpieza/pooling.
- Barrido: anticipación visible, impacto rápido, una aplicación de daño por objetivo y recuperación.
- Spawn efectivo y ausencia de duplicados del boss.

Si tenés acceso al Editor, probá en Play Mode desde la cámara real del juego y entregá capturas o una grabación corta. Si no lo tenés, no afirmes que lo probaste: dejá cambios y herramientas concretas, indicá qué ejecutaste y los pasos mínimos pendientes. No te detengas en instrucciones genéricas si podés crear los assets desde las herramientas disponibles.

Terminá con un resumen breve de cambios, rutas de prefabs/escena de prueba, cómo probar cada criatura, validación realizada y pendientes reales. La meta es una integración jugable dentro de mi proyecto, conservando su arquitectura y el estilo voxel aprobado.
