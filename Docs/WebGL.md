# Build HTML / WebGL

Salida: Builds/Mismo-WebGL-2026-09-09. Incluye index.html, Build y TemplateData. Hay que conservar la estructura completa.

## Abrir localmente

Desde la carpeta del proyecto, ejecutar:

```powershell
powershell -ExecutionPolicy Bypass -File Tools/ServeWebBuild.ps1
```

Abrir http://127.0.0.1:8080. El servidor se detiene con Ctrl+C. No abrir index.html con doble clic: WebGL carga sus archivos mediante HTTP.

En otro equipo con Python 3 se puede ejecutar dentro de la carpeta de la build:

```text
python -m http.server 8080
```

## Publicar

Subir index.html, Build y TemplateData a un hosting estático. La build usa compresión Gzip con descompresión de respaldo de Unity para servidores sin reglas especiales. Los archivos deben mantener sus nombres.

## Guardados

El navegador guarda las partidas localmente. Continuar requiere usar el mismo navegador y origen (protocolo, dominio y puerto). Los guardados del editor no se transfieren automáticamente a la web.

## Volver a compilar

Con el módulo WebGL instalado, ejecutar el método Mismo.Menu.Editor.WebBuildBuilder.Build en Unity. MISMO_WEBGL_OUTPUT permite indicar otra carpeta de salida. La configuración incluye el menú y la escena del mundo.
