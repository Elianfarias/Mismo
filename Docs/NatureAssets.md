# Nature en el mundo

Los 26 FBX de `Assets/Art/FBX/Nature/Nature` se usan en 28 prefabs: ocho árboles ensamblados con tronco y copa, ocho troncos caídos, dos arbustos, tres flores, tres pastos y cuatro rocas. Se agregan vivienda y taller usando la casa existente de Town; Nature no contiene edificios. El taller comparte el modelo de casa, no incorpora nuevas interacciones de herrería.

## Configuración

Seleccionar `Assets/Resources/WorldContentCatalog.asset` en el Inspector:

- Assets: entradas `nature.*`, con prefab, tipo, biomas, peso, tamaño, pendiente máxima y rango de escala. Biomes vacío admite todos; Deadwood está limitado al bosque.
- Ground Vegetation Density: densidades de pasto, arbustos, flores, rocas y troncos caídos. Estos valores se aplican a las partidas existentes al recargar sectores.
- Árboles: Tree Density y Biomes → Trees en ExplorationWorldSettings conservan la densidad de la geografía guardada; los nuevos prefabs sustituyen su apariencia. Scale Range del catálogo cambia la variación de tamaño de los prefabs.

Prefabs editables en `Assets/Prefabs/World/Nature`; materiales y paletas recuperadas en `Assets/Art/Materials/Nature`. El menú Mismo → World → Integrate Nature assets reconstruye prefabs y materiales, conservando parámetros de las entradas existentes del catálogo. Las modificaciones manuales dentro de esos prefabs se sobrescriben al ejecutar el importador.

## Aparición y colisiones

La distribución es determinista y evita caminos y zonas reservadas. El pasto tiene colisión de malla; las flores no tienen collider. Ambos omiten las sombras. Las copas de los árboles también tienen colisión de malla. Rocas, troncos caídos y casas usan colisión de malla con lectura habilitada para construir NavMesh en ejecución. Los árboles tienen colisión de tronco y ClimbableTree.

En Play directo y partidas legacy se sustituyen los árboles del bosque original y se agrega vegetación al cargar sectores. Las construcciones existentes del centro no se reubican. En el exterior procedural y mundos nuevos, las casas y talleres reemplazan los bloques provisionales. Los cambios no alteran la semilla ni las derrotas guardadas.

Algunos PNG entregados son vistas previas en lugar de las paletas 256×1 que usan las UV de los FBX. El importador recupera esos colores desde el bloque RGBA de su archivo VOX y genera una textura compatible; los originales permanecen intactos.

## Validación

Importación y capturas en Unity 6000.3.11f1, usando una copia aislada del proyecto. Se verifican referencias de materiales y texturas, mallas legibles para navegación, colisiones en pasto y copas, ausencia de colliders en flores y posiciones deterministas de vegetación fuera de caminos reservados. Informes y capturas: Docs/Validation/NatureIntegration.txt, NatureOverview.png y NatureGroundCover.png.

Los troncos caídos sólo se colocan sobre una terraza plana: se comprueba cada columna de terreno bajo su huella rotada y escalada. Los candidatos que cruzan escalones se descartan para evitar madera enterrada o suspendida.

