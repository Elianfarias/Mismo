# Mismo — WebGL Pueblos v2

## Cambios incluidos
- Pueblos al doble de escala: unos 88 metros de ancho.
- Casas y murallas apoyadas en el terreno, sin plataforma gris.
- Pueblos mucho menos frecuentes: un candidato por región de 1.024 m, con 65 % de probabilidad; pueblo inicial garantizado.
- Entrada y cámara adaptadas al nuevo tamaño.
- Migración de guardados anteriores para evitar aparecer dentro de edificios ampliados.
- Incluye también mapa, criaturas, superficies de pasto/tierra y ciclo de día y noche.

## itch.io
Subir el ZIP como juego HTML y marcar que se ejecuta en el navegador. El archivo index.html está en la raíz del ZIP junto a las carpetas Build y TemplateData. Reemplazar la build anterior con esta versión.

## Prueba local
Con Python 3, ejecutar dentro de la carpeta extraída:

python -m http.server 8080

Después abrir http://127.0.0.1:8080. No abrir index.html con doble clic.

Los guardados web pertenecen al navegador y al origen HTTP utilizado. La build conserva el sistema de guardado; no transfiere automáticamente perfiles del editor.
