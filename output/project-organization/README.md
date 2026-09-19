# Reorganización del proyecto — 19/09/2026

Se aplicó `migration-plan.json` mediante `AssetDatabase.MoveAsset` en el editor de trabajo.

- 1.353 assets reubicados; 2.261 entradas comprobadas.
- Cero archivos faltantes, cero cambios de GUID y cero carpetas Resources en Assets.
- Los originales de FourEvilDragonsPBR se distribuyeron por categoría dentro de Art. Se eliminaron las carpetas vacías; se conservaron los archivos necesarios para materiales, animaciones y futuras exportaciones.
- ScriptableObjects en Data; prefabs, mallas, clips, materiales, texturas, audio y fuentes en sus categorías de Art.
- Se corrigieron 53 MTL para conservar sus rutas relativas a texturas.
- Unity sincronizó propiedades de materiales BluePBR/RedPBR al importarlos; se preservaron la variante seleccionada, los GUID y las referencias PBR.
- Las cargas del juego usan RuntimeAssetCatalog, registrado en Preloaded Assets. Se mantienen las claves lógicas y el filtrado por tipo, incluso cuando un material y un shader comparten una clave.
- Las ventanas de generación y las guías existentes usan las nuevas rutas.

## Comprobaciones

`reference-audit.json` contiene la auditoría de archivos y GUIDs. `ProjectOrganizationChecks.Run` pasó tanto en la copia aislada como en el Unity de trabajo. La copia también pasó las comprobaciones de carga de catálogo, inventario/localización, los 51 clips de dragón y las regresiones del voxelizador.

La build completa se interrumpió por un fallo nativo de Unity en el procesamiento de variantes de shaders (`ShaderKeywordFilter.SettingsNode.GetVariantArray`) con agotamiento de memoria. No se modificó la configuración gráfica del proyecto de trabajo para resolverlo.

## Guía para futuros cambios

Leer `Docs/Buenas-practicas-de-assets.md`; `AGENTS.md` establece estas convenciones para futuros asistentes. En Unity: **Mismo → Proyecto → Actualizar catálogo de assets** y **Verificar organización y referencias**.

Addressables requiere la adaptación de ruta en `ProjectPackagePaths`; revisar su compatibilidad al actualizar ese paquete. La configuración principal de renderizado y las escenas del usuario se conservaron.

### Build y ejecución aislada

Build Windows64 de comprobación: **correcta**, 646.157.203 bytes. El player terminó con `CATALOG_PLAYER_OK`, cargando inventario, AudioMixer, clips de audio, UI, traducciones, material/shader bajo la misma clave y búsquedas de carpetas sin AssetDatabase.

Esta prueba usa una escena mínima y desactiva URP solo en la copia aislada para evitar el fallo nativo de la build completa. Verifica inclusión e inicialización de assets; no sustituye una validación visual completa del juego con sus perfiles URP. Evidencia: `catalog-build.log`, `catalog-player.log` y `catalog-player-result.txt`.
