# Ataques del jabalí y la araña

`polish_attacks.py` hornea las cadenas IK de los rigs originales en dos FBX de animación en `Assets/Art/Animations/ForestCreatures/Combat`. No guarda sobre los `.blend`, ni exporta mallas o materiales. Requiere los originales extraídos en `ArtSource/Creatures/Boar/Wild_Boar_Rigged.blend` y `ArtSource/Creatures/Spider/Forest_Spider_Rigged.blend`.

Regenerar desde la raíz del proyecto:

```text
blender --background --python Assets/Art/Source/Creatures/polish_attacks.py -- <ruta absoluta del proyecto>
```

En Unity, **Mismo → Enemigos → Mejorar anticipación de jabalí y araña** configura los importadores y aplica los cuatro ataques a sus settings compartidos. Este comando restablece los tiempos indicados abajo; no ejecutarlo para conservar ajustes posteriores hechos en el taller. **Verificar anticipación de jabalí y araña** solo comprueba las referencias, poses y variantes, y genera capturas en `output/creature-attack-polish`.

| Ataque | Preparación | Ejecución | Recuperación |
| --- | ---: | ---: | ---: |
| Jabalí: colmillazo | 0,55 s | 0,16 s | 0,08 s |
| Jabalí: carga | 0,72 s | 0,60 s | 0,10 s |
| Araña: mordida | 0,48 s | 0,14 s | 0,07 s |
| Araña: salto | 0,60 s | 0,48 s | 0,08 s |

Los FBX duran un segundo y se muestrean por las fases de la IA usando `activeStartsAt` / `recoveryStartsAt`. La traslación de avance sigue perteneciendo al controlador; Root permanece fijo y el salto eleva Body y las patas. El aterrizaje se produce al 90 % de la ejecución, coincidiendo con el comienzo del daño. Los ataques concatenados del gólem mantienen su reproducción anterior.

Las capturas muestran, por fila, reposo, preparación avanzada, inicio de ejecución, ejecución media y fin de ejecución. Primera fila: ataque cercano. Segunda fila: carga/salto.

Validación completa en Unity batch: `-executeMethod ForestCreatureAttackBuildChecks.RunBatch` sin `-quit` (la prueba de Play Mode cierra el proceso al finalizar). Construye y carga las siete variantes en un bundle Windows, revisa la organización y ejecuta `ForestCreatureAttackPlayChecks` en una arena temporal, incluyendo contactos, interrupciones y altura real del salto. Es una build de contenido; no sustituye una build completa del juego. Para repetir solo combate y capturas: `-executeMethod ForestCreatureAttackPlayChecks.RunBatch`.
