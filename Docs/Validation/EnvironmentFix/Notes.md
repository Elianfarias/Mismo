# Correcciones de cielo y pueblos

Los EXR originales son tiras de seis caras (6:1). Se importan como Cubemap y el shader mezcla muestras por dirección, con decodificación HDR. Se conserva la configuración horaria.

El terreno se nivela en una zona cuadrada de 52 metros bajo cada pueblo, después de calcular los caminos. La fundación queda 8 cm por encima para evitar superficies superpuestas. El pueblo original también nivela su terreno y reconstruye las mallas y colisiones cercanas al entrar al mundo.

Se retiraron 30 instancias sin archivo de origen del paquete Medieval Town - Free Sample. La escena previa está en VoxelRegion_7319.before.unity.txt; los nombres retirados están en RemovedMissingPrefabs.txt. Los modelos del pueblo nuevo se conservan.

Salir de Play, dejar importar Unity y volver a abrir VoxelRegion_7319 si estaba abierta antes del cambio. Continuar vuelve a construir el terreno con la corrección; no hace falta crear otra partida.
