param([int]$Port = 8080)
$buildDirectory = Join-Path $PSScriptRoot '../Builds/Mismo-WebGL-2026-09-09'
if (!(Test-Path (Join-Path $buildDirectory 'index.html'))) { throw 'La build WebGL todavía no está disponible.' }
$pythonCommand = Get-Command python -ErrorAction SilentlyContinue
$bundledPython = Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
Write-Host "Abrí http://127.0.0.1:$Port en el navegador. Ctrl+C detiene el servidor."
if (Test-Path $bundledPython) { & $bundledPython -m http.server $Port --bind 127.0.0.1 --directory $buildDirectory }
elseif ($pythonCommand) { & $pythonCommand.Source -m http.server $Port --bind 127.0.0.1 --directory $buildDirectory }
else { throw 'Instalá Python 3 o subí la carpeta completa a un hosting estático.' }
