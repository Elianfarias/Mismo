# Gameloop de la primera región: El despertar del dragón

## Propósito

Cerrar el primer ciclo jugable de Mismo con un objetivo claro, visible y memorable:

> El jugador llega al primer pueblo después de derrotar a los dos goblins de la entrada, descubre que un dragón está despertando en la región y debe preparar su invocación para enfrentarlo.

El diseño reutiliza los sistemas que ya existen:

- tutorial y combates contra goblins;
- misiones;
- exploración de la región;
- experiencia, niveles y maestría;
- armas, tiers y mejoras;
- mapa y marcadores;
- combate contra un boss.

La meta no es agregar muchos sistemas. La meta es darles una dirección común y convertirlos en un arco completo de inicio, preparación, desafío y recompensa.

## Punto de partida actual

El tutorial termina cuando el jugador derrota a los dos goblins que custodian la entrada al primer pueblo.

Ese momento debe funcionar como una transición importante:

1. El jugador demuestra que aprendió los fundamentos del combate.
2. Cruza el umbral del pueblo y deja atrás la introducción.
3. Recibe el primer objetivo de aventura de largo plazo.
4. Ve o escucha por primera vez al dragón.

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
Primer avistamiento o rugido del dragón
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
Desbloquear la siguiente etapa de la aventura
```

## Gancho narrativo al terminar el tutorial

### Evento de llegada al pueblo

Cuando muere el segundo goblin, el jugador puede tener un breve momento de control libre. Después sucede un evento corto:

1. Se escucha un rugido.
2. El cielo se oscurece por la sombra del dragón.
3. El dragón sobrevuela la zona a distancia, sin entrar todavía en combate.
4. Una parte de la montaña libera humo, fuego o ceniza.
5. El guardia del pueblo abre el acceso y reacciona al evento.

No hace falta una cinemática compleja. La secuencia puede resolverse con una toma de cámara breve, audio, VFX y el movimiento del modelo del dragón. El jugador debe conservar el control siempre que sea posible.

### Primera misión principal

Nombre provisional: **El despertar de la montaña**.

Texto de inicio:

> “El rugido que escuchaste no vino de los goblins. Algo despertó en la montaña. Averigua qué ocurrió y habla con la gente del pueblo.”

La misión debe comenzar automáticamente al entrar al pueblo o al hablar con el primer NPC importante. No conviene ocultarla detrás de una cadena larga de conversaciones.

### Función del pueblo

El pueblo es el punto de preparación, no solo un lugar de entrega de misiones. Debe ofrecer tres lecturas claras:

- **información:** alguien conoce la historia de los faros y la guarida;
- **preparación:** el jugador puede revisar armas, niveles y recursos;
- **dirección:** existe un camino o pista visual hacia la primera zona de la región.

Un único NPC puede iniciar la misión para el primer prototipo. Más adelante se pueden repartir las funciones entre herrero, explorador y anciano del pueblo.

## Cadena de misiones

La cadena debe ser corta y utilizar actividades ya presentes. Tres objetivos son suficientes para que el jugador sienta que se está preparando sin convertir el boss en una espera excesiva.

### Etapa 1: Investigar el rugido

**Objetivo:** seguir las señales del dragón hasta un mirador, campamento destruido o zona quemada.

**Acciones del jugador:**

- salir del pueblo;
- recorrer un tramo conocido o una bifurcación cercana;
- investigar huellas, restos o una zona afectada;
- derrotar a un pequeño grupo de enemigos alterados.

**Resultado:** el jugador descubre que tres faros antiguos mantenían dormido o contenido al dragón.

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
  → Investigando
  → Faro 1 activo
  → Faro 2 activo
  → Faro 3 activo
  → Foco obtenido
  → Guarida descubierta
  → Invocación disponible
  → Dragón derrotado
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
- un material o núcleo para una mejora posterior;
- un objeto que confirme la victoria en el inventario o perfil.

La progresión actual ya contempla una recompensa de boss y valores específicos de experiencia. Conviene usar esos valores como punto de partida y balancearlos después de las primeras pruebas, en lugar de crear una economía paralela.

### Recompensa de mundo

La derrota debe cambiar algo visible:

- el humo de la montaña desaparece;
- el pueblo celebra o reacciona;
- una puerta o ruta queda habilitada;
- el mapa revela la siguiente región;
- aparecen nuevos NPCs, misiones o enemigos.

El jugador debe poder comprobar que la región fue afectada por su victoria.

### Próximo objetivo

Después del boss, la misión debe cerrar con una nueva pregunta:

> “El dragón protegía algo o estaba huyendo de algo. ¿Qué hay más allá de la montaña?”

Esto permite continuar hacia la siguiente región sin que el jugador sienta que terminó el juego después del primer gran desafío.

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
| Investigación inicial | 5–10 min | Dar dirección y mostrar la región |
| Tres faros | 20–35 min | Explorar, combatir y progresar |
| Foco y llegada a la guarida | 5–10 min | Preparar el compromiso final |
| Primer intento contra el dragón | 5–8 min | Entregar el desafío memorable |
| Reintentos posteriores | 3–8 min | Aprender y dominar el combate |

El arco completo debería poder terminarse en una sesión corta, pero dejar suficiente espacio para explorar, mejorar armas y completar misiones secundarias.

## Alcance del primer prototipo

Para validar el gameloop no hace falta construir toda la región definitiva. El primer prototipo debería incluir únicamente:

1. El final actual del tutorial con los dos goblins.
2. La entrada al pueblo.
3. El evento de avistamiento del dragón.
4. Un NPC con la misión principal.
5. Tres objetivos representados por encuentros simples.
6. Un altar de invocación.
7. El dragón con una primera fase terrestre y una fase aérea.
8. Una recompensa visible.
9. Un estado de victoria que desbloquee la siguiente ruta.

Si ese recorrido resulta divertido aunque los escenarios sean provisionales, el diseño está funcionando. El arte final, la cinemática y los detalles de lore pueden incorporarse después.

## Guía de implementación para el proyecto

### Datos y estados

- Crear la misión y sus etapas como datos configurables, no como textos hardcodeados en el controlador.
- Mantener referencias serializadas a NPCs, faros, altar, guarida y boss.
- Persistir el progreso de los faros y la disponibilidad de la invocación.
- Usar un identificador estable para la misión y sus objetivos.
- Mantener la recompensa del boss dentro del sistema de inventario y progresión existente.

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
- entiende que el dragón es el objetivo principal de la región;
- puede explicar qué debe hacer para invocarlo;
- encuentra los tres objetivos sin depender de una guía paso a paso;
- reconoce cuándo está preparado para el combate;
- puede reintentar al boss sin repetir la preparación completa;
- entiende por qué murió después de cada intento;
- la recompensa se siente diferente a la de un goblin común;
- la victoria modifica visualmente el mundo o desbloquea una nueva ruta;
- el recorrido completo produce una sensación de cierre y deja un objetivo siguiente.

## Checklist de prueba con jugadores

Después de una primera sesión, preguntar solamente:

1. ¿Qué pensaste que debías hacer al llegar al pueblo?
2. ¿En qué momento entendiste que el dragón era el objetivo principal?
3. ¿Recordabas cuántos faros debías activar?
4. ¿Te pareció claro cuándo estabas listo para invocarlo?
5. ¿Qué aprendiste después de morir?
6. ¿La recompensa te dio ganas de seguir jugando?

Si el jugador no sabe qué hacer, hay un problema de dirección. Si sabe qué hacer pero no le interesa hacerlo, hay un problema de recompensa o presentación. Si llega al dragón sin haber mejorado nada y lo derrota fácilmente, falta presión de progresión. Si muere sin entender por qué, falta legibilidad en los ataques.

## Decisión recomendada

La versión inicial debe ser:

> Pueblo → avistamiento del dragón → tres faros → foco de invocación → arena → dragón → recompensa y nueva ruta.

El punto exacto donde termina hoy el tutorial es ideal para introducir el gancho narrativo. Los dos goblins representan el final del aprendizaje; el rugido que viene después representa el comienzo de la aventura real.

