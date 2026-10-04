# Gameloop de la primera región: El despertar del dragón

## Propósito

Cerrar el primer ciclo jugable de Mismo con un objetivo claro, visible y memorable:

> El jugador llega al primer pueblo después de derrotar a los dos goblins de la entrada, presencia el sobrevuelo del dragón y conoce su relación con el aumento de monstruos. El rey le encomienda activar los tres faros y preparar su invocación. Derrotar al primer dragón termina la demo.

El diseño reutiliza los sistemas que ya existen:

- tutorial y combates contra goblins;
- misiones;
- exploración de la región;
- experiencia, niveles y maestría;
- armas, tiers y mejoras;
- mapa y marcadores;
- combate contra un boss.

La meta es dar a los sistemas una dirección común y convertirlos en un arco completo de inicio, preparación, desafío y recompensa. Las regiones interiores del continente quedan planteadas como misterio para la aventura futura; el contenido jugable de la demo termina con este boss.

## Punto de partida actual

El tutorial termina cuando el jugador derrota a los dos goblins que custodian la entrada al primer pueblo.

Ese momento debe funcionar como una transición importante:

1. El jugador demuestra que aprendió los fundamentos del combate.
2. Cruza el umbral del pueblo y deja atrás la introducción.
3. Presencia el sobrevuelo del dragón con cámara dirigida y movimiento bloqueado.
4. Escucha la explicación del NPC y recibe el objetivo de hablar con el rey, quien propone la misión principal.

El jugador no debería salir del tutorial preguntándose qué hacer a continuación. Debe entender que el pueblo es un lugar de preparación y que la región tiene una amenaza mayor que los goblins.

## Fantasía del jugador

La sensación buscada es:

> “Acabo de llegar a un lugar peligroso. Hay algo enorme sobrevolando la región. Voy a mejorar, explorar y reunir lo necesario para desafiarlo.”

El dragón no debe sentirse como un enemigo guardado detrás de una puerta. Debe estar presente desde temprano mediante señales y consecuencias:

- una sombra que cruza el cielo;
- un rugido lejano;
- humo o fuego en la montaña;
- árboles o estructuras quemadas;
- NPCs que describen ataques recientes;
- monstruos alterados por su presencia;
- una zona elevada visible desde el pueblo.

## Estructura general del loop

```text
Derrotar a los dos goblins
          ↓
Entrar al primer pueblo
          ↓
Sobrevuelo del dragón con cámara dirigida y control bloqueado
          ↓
El NPC explica la amenaza y envía al jugador ante el rey
          ↓
El rey cuenta la leyenda de los tres faros y propone la misión
          ↓
Investigar la amenaza
          ↓
Completar tres objetivos de preparación
          ↓
Descubrir la guarida
          ↓
Invocar al dragón
          ↓
Derrotar al boss regional
          ↓
Recibir una recompensa única
          ↓
Fin de la demo
```

## Gancho narrativo al terminar el tutorial

### Evento de llegada al pueblo

Después de derrotar a los dos goblins, al alcanzar el punto seguro de llegada al pueblo sucede una secuencia breve:

1. El personaje queda inmóvil y se bloquean sus acciones de combate.
2. La cámara se orienta suavemente para enfocar al dragón que pasa volando al fondo.
3. El dragón cruza el paisaje sin atacar ni aterrizar.
4. El NPC explica que el dragón es el culpable del aumento de monstruos, que nunca se había comportado así y que ya no responde a las ofrendas.
5. El NPC plantea que algo está pasando en las regiones interiores del continente y pide al jugador que hable con el rey.
6. La cámara regresa al personaje, se devuelve el control y aparece el objetivo «Hablá con el rey».

La secuencia puede resolverse con cámara, una trayectoria de vuelo y diálogo con subtítulos. No requiere una voz grabada para el prototipo. Mientras las acciones del jugador están bloqueadas, el dragón y la escena siguen animándose. Al terminar, omitir o interrumpir la secuencia, siempre deben restaurarse cámara y controles.

Texto provisional del NPC:

> «Ese dragón es el culpable de que haya cada vez más monstruos. Nunca se había comportado así… y ya no responde a nuestras ofrendas. Algo está pasando en las regiones interiores del continente. Tenés que hablar con el rey».

### Primera misión principal

Nombre provisional: **El despertar de la montaña**.

Texto de inicio:

> «Las ofrendas ya no reciben respuesta. La leyenda habla de tres faros antiguos capaces de llamar al dragón. Encontralos, encendelos y prepará su invocación».

El rey propone esta misión durante una audiencia breve y cuenta la leyenda de los tres faros. Al aceptarla, el jugador recibe el primer destino de investigación y preparación. El NPC de llegada sólo presenta la amenaza y dirige al rey; no entrega por adelantado la misión de los faros. La leyenda debe explicar por qué su llamado antiguo puede funcionar cuando las ofrendas han dejado de servir; el texto definitivo se desarrollará durante la implementación narrativa.

### Función del pueblo

El pueblo es el punto de preparación, no solo un lugar de entrega de misiones. Debe ofrecer tres lecturas claras:

- **información:** el NPC de llegada describe la amenaza y el rey conoce la leyenda de los faros y la guarida;
- **preparación:** el jugador puede revisar armas, niveles y recursos;
- **dirección:** existe un camino o pista visual hacia la primera zona de la región.

El prototipo incluye al NPC de llegada y al rey, reutilizando el sistema de conversación. La audiencia debe estar señalizada y ser accesible desde el pueblo; no requiere una cadena adicional de encargos.

## Cadena de misiones

La cadena debe ser corta y utilizar actividades ya presentes. Tres objetivos son suficientes para que el jugador sienta que se está preparando sin convertir el boss en una espera excesiva.

### Etapa 1: Investigar el rugido

**Objetivo:** seguir las señales del dragón hasta un mirador, campamento destruido o zona quemada.

**Acciones del jugador:**

- salir del pueblo;
- recorrer un tramo conocido o una bifurcación cercana;
- investigar huellas, restos o una zona afectada;
- derrotar a un pequeño grupo de enemigos alterados.

**Resultado:** el jugador encuentra una pista que confirma el relato del rey y orienta hacia los faros. La leyenda ya se conoce por la audiencia; esta investigación la conecta con el terreno y las consecuencias del comportamiento del dragón.

**Recompensa:** experiencia, una pista de mapa y una primera señal de la ubicación del faro más cercano.

Esta etapa sirve para volver a poner al jugador en movimiento después del tutorial. No debería exigir una pelea difícil.

### Etapa 2: Encender los tres faros

**Objetivo:** reactivar los tres faros de la región.

Cada faro debe ocupar una zona con una pequeña variación de gameplay:

1. **Faro del bosque:** exploración y combate contra enemigos básicos.
2. **Faro de la ruina:** navegación vertical, plataformas o una pelea contra un enemigo más resistente.
3. **Faro de la garganta:** combate más exigente, terreno abierto y mayor exposición al dragón.

No es necesario crear tres mazmorras completas. Cada faro puede ser un encuentro de 3–6 minutos con una identidad visual y una dificultad ligeramente mayor que el anterior.

Al activar cada faro:

- el haz de luz debe verse desde otras partes de la región;
- debe emitirse una señal sonora diferente;
- el dragón puede reaccionar con un vuelo lejano o un rugido;
- el mapa puede revelar parcialmente el siguiente objetivo.

**Regla importante:** los faros deben poder completarse en cualquier orden si la estructura del mundo lo permite. Si el orden es necesario, la misión debe explicarlo y la geografía debe evitar desvíos confusos.

### Etapa 3: Conseguir el foco de invocación

**Objetivo:** recuperar un objeto que permita atraer al dragón a la arena final.

Nombre provisional: **Corazón de brasa**, **Escama primordial** o **Fragmento del faro**.

El objeto puede obtenerse de un monstruo élite ya existente o de un guardián de la última zona. Esto conecta directamente la progresión de enemigos con el boss sin introducir una categoría nueva de misión.

El enemigo que lo custodia debe ser más peligroso que un goblin común, pero no un segundo boss. La intención es comprobar que el jugador aprovechó sus mejoras de arma y sus puntos de progresión.

**Resultado:** al obtener el objeto, se revela por completo la ubicación de la guarida en el mapa y se habilita la misión final.

## Cómo guiar al jugador

La dirección debe apoyarse en varias señales que se refuercen entre sí.

### Señales visuales

- La montaña o guarida debe ser visible desde el pueblo o desde un punto alto.
- Los faros deben tener siluetas reconocibles.
- La columna de humo del dragón debe cambiar cuando se activa cada faro.
- Las zonas quemadas deben formar una lectura espacial: pueblo, sendero, faros y montaña.

### Señales narrativas

Los NPCs no necesitan explicar toda la historia. Cada uno debe responder una pregunta:

- ¿Qué es el dragón?
- ¿Por qué despertó?
- ¿Qué son los faros?
- ¿Cómo se llega a la guarida?

La información más importante debe estar en la misión, el mapa o el entorno. El jugador no debería tener que recordar una conversación larga para saber qué hacer.

### Señales de misión y mapa

- La misión activa muestra el objetivo actual, no los tres pasos futuros en exceso.
- Al activar un faro, se marca el siguiente objetivo o se actualiza la región visible.
- Al conseguir el foco de invocación, aparece la guarida como destino principal.
- La brújula o marcador debe indicar una dirección general, no llevar al jugador por cada metro del recorrido.

El marcador debe responder a la pregunta “¿qué estoy buscando ahora?”. El entorno debe responder a la pregunta “¿por qué voy hacia allí?”.

## Diseño de la invocación

La invocación debe ser una acción del jugador y no una transición automática a una cinemática.

### Preparación de la arena

La guarida debe tener:

- una zona de llegada segura;
- un espacio reconocible para el combate;
- un altar o círculo de invocación;
- límites legibles, sin bloquear al jugador de forma artificial;
- una ruta de salida antes de activar el encuentro;
- un punto de reintento cercano después de la primera activación.

Antes de invocar, el jugador debería poder revisar su equipamiento, volver al pueblo o explorar los alrededores. La invocación es el momento de compromiso, no la entrada a la zona.

### Secuencia de activación

1. El jugador interactúa con el altar.
2. El juego comprueba que los tres faros están activos y que el foco de invocación está en la misión.
3. El altar muestra los tres símbolos encendidos.
4. El jugador confirma la invocación.
5. El entorno cambia: viento, ceniza, luz, sonido y temblor.
6. El dragón aparece a distancia y entra en la arena con sus animaciones existentes.
7. El combate comienza cuando el jugador recupera el control.

La introducción debe poder omitirse después del primer intento. Esto es importante para que las derrotas no se vuelvan repetitivas.

### Estado de la misión

Durante la invocación deben existir estados claros:

```text
No iniciado
  → Sobrevuelo y explicación del NPC
  → Audiencia con el rey y misión aceptada
  → Investigando
  → Tres faros activos (registrados por separado, en cualquier orden)
  → Foco obtenido
  → Guarida descubierta
  → Invocación disponible
  → Dragón derrotado
  → Fin de la demo
```

El estado de misión debe sobrevivir a la muerte y al reinicio del intento. No se deben perder los faros activados ni el foco conseguido.

## Diseño del primer boss regional

El dragón debe evaluar las habilidades que el tutorial ya enseñó, no exigir un sistema completamente nuevo.

### Objetivos de diseño

- Que se entienda qué ataque va a realizar.
- Que cada ataque tenga una respuesta razonable.
- Que el jugador pueda reconocer ventanas de daño.
- Que el combate tenga momentos espectaculares sin volverse caótico.
- Que morir deje claro qué debe aprender el jugador.

### Fases sugeridas

#### Fase 1: El guardián terrestre

El dragón permanece principalmente en el suelo.

Ataques posibles:

- mordida frontal;
- golpe de garra;
- barrido de cola;
- pisotón con onda corta;
- rugido que obliga a separarse.

La intención es enseñar distancia, lectura de animaciones y ataques por detrás o durante ventanas concretas.

#### Fase 2: El fuego desde el cielo

El dragón despega y usa el espacio de la arena.

Ataques posibles:

- pasada en línea recta;
- lluvia de fuego con zonas visibles en el suelo;
- proyectil dirigido al jugador;
- aterrizaje con daño de área.

La arena debe ofrecer una respuesta visual clara: zonas de cobertura, espacios seguros o patrones que puedan esquivarse. No conviene que el jugador dependa de adivinar.

#### Fase 3: La última embestida

Con poca vida, el dragón combina ataques anteriores y rompe una parte de la arena o modifica el espacio disponible.

Debe existir una ventana final de daño muy clara. El final tiene que sentirse ganado y no convertirse en una carrera de daño.

## Uso de las animaciones existentes

Antes de crear animaciones nuevas, clasificar las que ya existen por función:

- entrada a la arena;
- locomoción terrestre;
- ataques frontales;
- ataque lateral o de cola;
- despegue;
- vuelo o pasada aérea;
- aterrizaje;
- rugido o reacción;
- daño recibido;
- muerte;
- victoria o estado inactivo.

Con ese inventario se puede construir una primera versión del boss mediante una máquina de estados sencilla:

```text
Intro
  → GroundCombat
  → AirCombat
  → EnragedCombat
  → Death
```

El primer prototipo no necesita una IA compleja. Necesita buena selección de ataques, telegráficos legibles, distancias correctas y transiciones que respeten las animaciones.

## Recompensa y cierre del arco

La recompensa debe justificar toda la preparación.

### Recompensa inmediata

Recomendación:

- experiencia de personaje y maestría;
- un arma única o una mejora de arma relacionada con el dragón;
- un material o núcleo registrado para una mejora futura;
- un objeto que confirme la victoria en el inventario o perfil.

La progresión actual ya contempla una recompensa de boss y valores específicos de experiencia. Conviene usar esos valores como punto de partida y balancearlos después de las primeras pruebas, en lugar de crear una economía paralela.

La recompensa se registra automáticamente al ganar y aparece en el resumen final. La mochila llena no impide el cierre: el derecho al premio queda guardado. Recoger un objeto o volver al rey no es condición para terminar.

### Recompensa de mundo

La derrota puede apagar el humo y cambiar el ambiente de la arena durante la presentación final. La demostración de victoria ocurre en ese mismo cierre; no requiere regresar al pueblo ni habilitar otro tramo jugable.

### Fin de la demo

Matar al primer dragón es el final de la demo. Tras su animación de muerte y el registro de la victoria, mostrar «Fin de la demo», la recompensa y una acción para volver al menú.

La sospecha sobre las regiones interiores queda abierta como promesa narrativa para el juego completo. No se desbloquea una segunda región ni se asigna una nueva misión jugable después de este boss.

Si se continúa una partida que ya tiene esa victoria guardada, se recupera el cierre sin recrear al dragón ni volver a conceder sus premios.

## Muerte, reintento y persistencia

La primera experiencia con el boss debe ser cómoda para aprender.

Al morir:

- se conserva la experiencia ganada;
- se conservan niveles, puntos y armas;
- se conservan los faros activados;
- se conserva el foco de invocación;
- el dragón vuelve a su estado inicial;
- el jugador reaparece cerca de la guarida o en el pueblo, según la estructura actual de la partida;
- la introducción del boss se puede omitir en los siguientes intentos.

No se debe obligar al jugador a repetir los tres faros después de cada derrota. La preparación es contenido de descubrimiento; el combate es el contenido que debe poder repetirse.

## Pacing recomendado

Como objetivo de playtest, no como regla rígida:

| Momento | Duración aproximada | Función |
| --- | ---: | --- |
| Final del tutorial y entrada al pueblo | 5–10 min | Cerrar aprendizaje y presentar la amenaza |
| Audiencia con el rey e investigación inicial | 5–10 min | Presentar la leyenda, dar dirección y mostrar la región |
| Tres faros | 20–35 min | Explorar, combatir y progresar |
| Foco y llegada a la guarida | 5–10 min | Preparar el compromiso final |
| Primer intento contra el dragón | 5–8 min | Entregar el desafío memorable |
| Reintentos posteriores | 3–8 min | Aprender y dominar el combate |

El arco completo debería poder terminarse en una sesión corta, pero dejar suficiente espacio para explorar, mejorar armas y completar misiones secundarias.

## Alcance del primer prototipo

Para validar el gameloop no hace falta construir toda la región definitiva. El primer prototipo debería incluir únicamente:

1. El final actual del tutorial con los dos goblins.
2. La entrada al pueblo.
3. El sobrevuelo de fondo, con cámara dirigida y controles bloqueados durante la explicación del NPC.
4. El rey con su audiencia, la leyenda de los tres faros y la misión principal.
5. Tres objetivos representados por encuentros simples.
6. Un altar de invocación.
7. El dragón con una primera fase terrestre y una fase aérea.
8. Una recompensa visible.
9. Un estado de victoria que termine la demo y conserve el resultado al continuar.

Si ese recorrido resulta divertido aunque los escenarios sean provisionales, el diseño está funcionando. El arte final, la cinemática y los detalles de lore pueden incorporarse después.

## Guía de implementación para el proyecto

### Datos y estados

- Crear la misión y sus etapas como datos configurables, no como textos hardcodeados en el controlador.
- Mantener referencias serializadas a NPCs, faros, altar, guarida y boss.
- Persistir la llegada, la audiencia y el progreso de los faros y del foco; calcular a partir de ese progreso la disponibilidad de la invocación.
- Usar un identificador estable para la misión y sus objetivos.
- Mantener la recompensa del boss dentro del sistema de inventario y progresión existente.
- Derivar el fin de demo de la victoria guardada; no exigir una entrega posterior al rey.

### Escena y mundo

- Colocar primero marcadores y volúmenes de gameplay antes de decorar.
- Verificar que la montaña o el área de la guarida sea visible desde al menos un punto relevante.
- Reservar espacio para la arena, la entrada, la salida y el reintento.
- No bloquear rutas con geometría que el jugador pueda saltar accidentalmente.
- Validar que la cámara mantenga al dragón dentro de una lectura clara durante los ataques grandes.

### Código y organización

- Mantener el código en `Assets/Scripts` y las herramientas en sus carpetas `Editor`.
- Mantener los datos de misiones y encuentros en `Assets/Data` según el sistema correspondiente.
- No agregar cargas mediante `Resources`.
- Si se incorporan o mueven modelos, animaciones, materiales o efectos, seguir las buenas prácticas de assets y conservar sus referencias.
- Si el dragón se integra como enemigo voxelizado o humanoide, revisar la guía específica de enemigos antes de modificar prefabs, animaciones o registros por bioma.

### Feel y combate

- Usar el receptor de cámara orbital existente para el feedback de la pelea.
- No agregar un shaker automático paralelo ni duplicar el hit stop actual.
- Reservar los eventos más fuertes para aterrizajes, rugidos, ataques de fuego y muerte.
- Probar el combate con cámara cercana y lejana antes de fijar los tamaños de la arena.

## Criterios de aceptación del gameloop

El primer arco está listo para enviar a testers cuando:

- un jugador nuevo llega al pueblo sin instrucciones externas;
- ve el sobrevuelo con el personaje inmóvil y recupera correctamente el control;
- entiende la explicación del NPC y encuentra al rey para recibir la misión;
- entiende que el dragón es el objetivo principal de la región;
- puede explicar qué debe hacer para invocarlo;
- encuentra los tres objetivos sin depender de una guía paso a paso;
- reconoce cuándo está preparado para el combate;
- puede reintentar al boss sin repetir la preparación completa;
- entiende por qué murió después de cada intento;
- la recompensa se siente diferente a la de un goblin común;
- matar al primer dragón termina la demo sin tareas adicionales;
- la victoria y el premio sobreviven a un cierre del juego durante la presentación final;
- el recorrido completo produce una sensación de cierre y curiosidad por las regiones interiores.

## Checklist de prueba con jugadores

Después de una primera sesión, preguntar solamente:

1. Después del sobrevuelo y la explicación del NPC, ¿qué pensaste que debías hacer?
2. ¿Qué te pidió el rey y qué entendiste de la leyenda?
3. ¿Recordabas cuántos faros debías activar?
4. ¿Te pareció claro cuándo estabas listo para invocarlo?
5. ¿Qué aprendiste después de morir?
6. ¿El final de la demo te dio ganas de conocer las regiones interiores?

Si el jugador no sabe qué hacer, hay un problema de dirección. Si sabe qué hacer pero no le interesa hacerlo, hay un problema de recompensa o presentación. Si llega al dragón sin haber mejorado nada y lo derrota fácilmente, falta presión de progresión. Si muere sin entender por qué, falta legibilidad en los ataques.

## Decisión recomendada

La versión inicial debe ser:

> Pueblo → sobrevuelo con cámara dirigida y explicación del NPC → audiencia con el rey y leyenda → investigación → tres faros → foco de invocación → arena → dragón derrotado → recompensa y fin de demo.

El punto exacto donde termina hoy el tutorial es ideal para introducir el gancho narrativo. Los dos goblins representan el final del aprendizaje; el rugido que viene después representa el comienzo de la aventura real.
