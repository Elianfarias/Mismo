# Flujo de personajes jugables y skins voxel

Este flujo se aplica al mago, al caballero y a la rana ninja, y a futuras apariencias del jugador. Una entrega incluye el modelo editable, la skin animable y su selección funcional en MainMenu. El menú de herramientas del editor por sí solo no la integra en el juego.

## 1. Revisar contexto y dirección artística

Leer `AGENTS.md`, `Docs/Buenas-practicas-de-assets.md` y, si está disponible, `Docs/Escala-compartida-personajes.md`. Revisar el concept aprobado y las correcciones del usuario antes de generar geometría. Los detalles específicos de las skins existentes están en `Docs/ArcaneMage.md`, `Docs/AshenWarrior.md` y `Docs/NinjaFrog.md` cuando esos documentos locales están disponibles. Si faltan, inspeccionar los prefabs y generadores actuales; no inventar sus medidas.

- Usar volúmenes de cubos visibles, silueta legible y capas con espesor. Mantener una retícula coherente dentro de cada personaje; no resolver todo con superficies planas o texturas que imiten vóxeles.
- Trabajar cabeza, torso, piernas y accesorios como una silueta completa. Comparar frente, tres cuartos, costado y espalda con el concept, y a la distancia real de la cámara del juego.
- La guía de escala compartida establece 180 cm de estatura base, unidad de exportación en metros, objeto a escala (1,1,1), origen entre las plantas de los pies y retícula principal de 2 cm. Excluir sombreros y coletas al medir la altura base. Los modelos históricos pueden tener otras medidas; no reescalarlos silenciosamente al añadir una skin o modificar el menú.
- El generador original de la rana usa una retícula de 2,5 cm. Es una característica del asset existente, no una sustitución de la guía común para nuevas creaciones.
- Respetar exclusiones del concept: la rana no lleva katana ni funda. Las armas equipables pertenecen al sistema de equipo y se sujetan a la mano, no se hornean en el cuerpo.
- Ojos y detalles faciales deben estar unidos a la superficie de la cabeza. Cascos, cuellos, capas y pañuelos necesitan uniones visibles; evitar placas flotantes, cuellos demasiado anchos o espacios que aparezcan al correr y atacar.

Si el usuario aún no aprobó una dirección, preparar un concept y compararlo con las referencias. Si ya la aprobó y pidió construirla, continuar sin volver a pedir el mismo permiso.

## 2. Crear una fuente reproducible y revisar fuera de Assets

Usar un generador o fuente editable. El ejemplo más reciente es `Assets/Scripts/Gameplay/Player/Editor/build_ninja_frog.py`, con salida de revisión mediante `--staging` a `output/ninja-frog/staging`. Revisar renders antes de reemplazar assets importados.

| Entrega | Ubicación |
| --- | --- |
| Concept aprobado | `Assets/Art/Textures/Concepts/Player/` |
| FBX Humanoid en pose T | `Assets/Art/FBX/Characters/` |
| Materiales | `Assets/Art/Materials/Characters/<Personaje>/` |
| Prefab visual | `Assets/Art/Prefabs/Player/Skins/` |
| Fuente editable archivada | `Assets/Art/Source/Characters/<Personaje>/` |
| Generador y herramientas | Subcarpetas `Editor` de `Assets/Scripts/` |
| Renders, informes y exportaciones de revisión | `output/<personaje>/` |

Mantener los `.blend` de trabajo fuera de Assets para evitar conversiones automáticas de Unity. Archivar la fuente junto con la versión exacta del generador; el `.gitignore` global ignora ZIP, por lo que un archivo fuente destinado al repositorio necesita una excepción explícita y localizada. No entregar sólo renders: comprobar que la fuente puede reproducir el FBX. Conservar GUID y `.meta` al actualizar assets.

## 3. Rig y exportación

Exportar el cuerpo en pose T para Humanoid y herramientas externas como DeepMotion. Las poses usadas para renders no deben reemplazar esa pose de referencia. En Unity, frente +Z y vertical Y; convertir correctamente los ejes de Blender.

Conservar la jerarquía y orientación compatibles con las skins actuales:

| Humanoid | Huesos del proyecto |
| --- | --- |
| Pelvis y torso | `Hips`, `Spine`, `Chest` |
| Cuello y cabeza | `Neck`, `Head` |
| Hombro, brazo, antebrazo, mano | `Clavicle.L/R`, `UpperArm.L/R`, `LowerArm.L/R`, `Hand.L/R` |
| Muslo, pierna, pie, dedos | `UpperLeg.L/R`, `LowerLeg.L/R`, `Foot.L/R`, `Toes.L/R` |

La raíz `Root` y los huesos adicionales de accesorios no sustituyen los huesos Humanoid. Preservar las orientaciones de las manos para los montajes de equipo existentes. Mantener pesos normalizados; compartir posiciones y pesos en uniones que deben permanecer cerradas, especialmente cuello/mandíbula y costuras de ropa.

Importar con `ModelImporterAnimationType.Human`, avatar creado desde el modelo y mapeo explícito. Comprobar `avatar.isValid` e `isHuman`. No optimizar eliminando transformaciones que necesitan los accesorios o el montaje del arma. Mantener las mallas legibles cuando lo requiera el apoyo al suelo. Remapear materiales explícitamente a los materiales externos del personaje.

## 4. Prefab visual, movimiento secundario y suelo

La skin visual contiene Animator, mallas, huesos y componentes de presentación. El prefab principal `Assets/Art/Prefabs/Player/Player.prefab` conserva motor, collider, inventario, combate y demás gameplay. Reutilizar su controller de locomoción y la configuración de root motion; añadir una apariencia no debe modificar habilidades ni hitboxes.

Como referencia, `NinjaFrogIntegration.Apply` importa y crea la skin; `AshenWarriorIntegration.ConfigureVisual` configura su presentación. Después de sustituir jerarquía/avatar, llamar `Animator.Rebind()` antes de consultar `GetBoneTransform`, y remapear las referencias a la jerarquía nueva.

| Skin | Movimiento secundario |
| --- | --- |
| Mago | `MageHatMotion` |
| Caballero | `WarriorCapeMotion` y `WarriorPlumeMotion` |
| Rana ninja | `FrogScarfMotion`: dos cadenas de cuatro huesos, ancladas al pecho |

Las raíces de los accesorios deben permanecer sujetas al cuerpo. Simular sólo sus huesos, con amortiguación, límites y reinicio ante teletransporte o salto de tiempo; conservar la pausa. Verificar referencias después de instanciar o cambiar la skin. Retirar componentes de otra apariencia al sustituir el visual. Esta simulación aproximada no equivale a colisión completa de tela contra armas y escenario.

`MageGroundContact` es el corrector compartido de apoyo. Reconoce mallas terminadas en `_Boot_L/R` y `_Foot_L/R`, con vértices rígidos vinculados al pie correspondiente. Corrige la cadera cuando hay suelo, sin desplazar la cápsula ni alterar salto/caída. Una nueva anatomía exige revisar estos supuestos; no compensar pies flotantes moviendo todo el Player o cambiando arbitrariamente su collider.

## 5. Integración obligatoria en MainMenu y guardado

1. En `PlayerAppearance.cs`, añadir un ID persistente, incluirlo en `Skins` y definir su etiqueta en `SkinName`. Los IDs existentes son `mage`, `knight` y `ninja-frog`; no renombrarlos al cambiar un texto visible, porque se guardan en las partidas.
2. En `PlayerSkinCatalogIntegration.cs`, añadir la relación ID → prefab visual. Ejecutar **Mismo → Character → Skins → Registrar en el selector** (`PlayerSkinCatalogIntegration.Register`). Crea/actualiza referencias `PlayerSkins/<id>` en `Assets/Data/System/RuntimeAssetCatalog.asset` y lo mantiene en Preloaded Assets. La actualización general del catálogo y la build también registran estas dependencias.
3. `CharacterCreatorView` recorre `PlayerAppearance.Skins` en ambos sentidos, con vuelta al principio/final, y obtiene la etiqueta de `SkinName`. No añadir otro booleano o alternancia exclusiva entre dos personajes. `CharacterCreationStage` instancia `PlayerAppearance.Prefab(id)` en la casita del MainMenu y conserva el giro fuera de la jerarquía animada.
4. El creador pasa nombre e ID a `WorldSession.NewGame`; el guardado usa `WorldSaveData.playerSkin`. No hace falta cambiar el formato para añadir un ID. `PlayerAnimationDriver` llama `PlayerAppearance.Apply` al comenzar: la nueva skin debe conservar controller, referencia visual del motor, mano de equipo y apoyo al suelo. Las partidas anteriores sin ID conservan la apariencia de autoría.
5. Confirmar que Nueva partida permite elegirla, que la vista previa está animada y que iniciar y continuar mantienen la misma apariencia. Comprobar también las skins anteriores.

No usar `Resources`, rutas de archivos del editor en runtime ni búsquedas por nombre global para cargar skins. No regenerar `MainMenu.unity` para añadir una opción: la lista se construye desde el código anterior. El menú **Usar rana ninja/mago/guerrero** modifica el prefab de autoría; es independiente de la selección guardada del jugador.

## 6. Validar y entregar

- Revisar en Unity frente, perfil y espalda; muestrear Idle, Walk, Run, Attack, Jump y Fall. Comprobar apoyo, unión cabeza/cuello, hombros, ropa, manos y accesorios. Usar clips reales del controller, no subassets `__preview__`.
- Ejecutar las comprobaciones específicas del modelo. `NinjaFrogChecks.Run` muestra cómo revisar 48 poses por clip, materiales, pesos, ausencia de armas incorporadas, uniones y simulación del pañuelo. Este método también cambia temporalmente la skin de autoría y termina en rana: no usarlo como prueba genérica sin revisar sus efectos.
- Ejecutar `CharacterCreatorChecks.Run`: catálogo, validación, guardado serializado, avatares, referencias del motor/mano, apoyo y componentes secundarios. Ampliar las expectativas específicas al añadir nuevas anatomías o componentes.
- Ejecutar `CharacterCreatorSceneChecks.Run`: usa una escena de preview del MainMenu, captura las opciones y comprueba ambos sentidos del selector, nombre, giro, confirmación y cierre. Las escenas abiertas del usuario no se reemplazan.
- Ejecutar `CharacterCreatorPlayChecks.Run` en Unity batch o **Mismo → Character → Verificar creador en Play Mode**: Nueva partida → elegir → confirmar → gameplay → continuar. Redirige los guardados a una carpeta de prueba y restaura las escenas abiertas al terminar. En el editor se bloquea si hay escenas sin guardar. También puede solicitarse mediante `Temp/CharacterCreatorPlayChecks.request`. No ejecutarlo contra el perfil real. Revisar sus asserts al añadir una opción.
- Ejecutar `ProjectOrganizationChecks.Run` y `CharacterCreatorChecks.Build`, porque registrar una skin cambia las dependencias incluidas en el juego. La build compila scripts, construye el contenido de las skins y genera el player Windows en `.validation/CharacterCreator`.
- Los informes y capturas del creador están en `output/character-creator`. Para solicitar verificaciones al editor abierto, las herramientas existentes aceptan `check` o `build` en `Temp/CharacterCreatorChecks.request` y escriben `request-result.txt`; esperan a que termine Play Mode y la compilación. No cerrar el editor del usuario para lanzar otra instancia sobre el mismo proyecto.
- Revisar el diff: no deben cambiar otras skins, escenas, materiales o gameplay accidentalmente. Comparar las referencias y configuración del Player con el estado anterior. No revertir cambios ajenos.

Una build con `Succeeded` y errores registrados no es una build limpia. Si existe un fallo ajeno (por ejemplo, organización de VFX), dejar el diagnóstico por separado y explicar qué validaciones sí pasaron. No reorganizar assets ajenos para ocultarlo.

Al entregar, indicar el nombre visible en el selector, archivos principales, una captura real y resultados de las comprobaciones, con cualquier limitación pendiente. No declarar probado Play Mode o una build si sólo se revisó el código.
