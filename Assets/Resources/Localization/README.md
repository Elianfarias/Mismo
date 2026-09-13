# Idiomas

El juego utiliza tablas StringTable del paquete oficial Unity Localization. Game_es y Game_en contienen los textos en español e inglés, con claves compartidas. Se cargan desde Resources para que el menú no dependa de una descarga ni de una consulta remota. El idioma se guarda en PlayerPrefs (Mismo.Language), separado del inventario y la partida.

El selector aparece arriba a la derecha del menú principal y dentro del menú B. Los idiomas disponibles se descubren a partir de las tablas Game_<código> de esta carpeta. Al agregar idiomas, usar códigos de cultura válidos (por ejemplo, pt-BR) y conservar las claves compartidas. Una traducción ausente vuelve al español y, si tampoco existe allí, al texto de respaldo.

## Edición

- Abrir la colección Game con Window > Asset Management > Localization Tables. Editar los valores en las columnas de cada idioma. Las claves no deben cambiar al corregir la redacción.
- Unity Localization permite exportar e importar CSV desde el editor de tablas. Se pueden generar borradores con traducción automática, revisar terminología y longitud, e importar el resultado. No se traduce mediante un servicio en línea durante el juego.
- Translations.json contiene el catálogo inicial y el vínculo entre textos españoles existentes y claves. Mismo > Languages > Import Spanish and English tables vuelve a importar ese catálogo y **sobrescribe sus entradas** en es/en. No usarlo después de editar traducciones en las tablas sin actualizar también el JSON.
- Para contenido nuevo, preferir GameLanguage.Get(clave, respaldo) y mensajes completos con parámetros. GameLanguage.Format traduce la plantilla y aplica el formato numérico del idioma. No traducir fragmentos concatenados ni IDs de guardado.
- AbilityDefinition.localizationKey identifica una habilidad. Sus textos usan los sufijos .name y .description. Los campos originales siguen disponibles como respaldo.

## Cobertura inicial

Menú principal, menú B, inventario, comparación, panel de personaje/maestría y habilidades de espada/arco. Incluye nombres iniciales y avisos comunes del inventario. Los mensajes del mundo, HUD de combate, mapa y errores técnicos adicionales todavía requieren migración a tablas. Agregar una tabla no traduce automáticamente textos nuevos ni contenidos que aún no estén conectados al servicio.

## Validación

InventoryExperienceChecks comprueba español/inglés, decimales, respaldo de claves y captura el inventario y habilidades en inglés. Se ejecuta únicamente en una copia aislada del proyecto y cambia el idioma de prueba sin escribir preferencias del jugador.
