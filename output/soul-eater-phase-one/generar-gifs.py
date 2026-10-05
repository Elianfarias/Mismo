from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parent
for folder, name in [('flame-frames', 'aliento'), ('charge-frames', 'carga-75'), ('tail-frames', 'coletazo')]:
    frames = [Image.open(p).convert('RGB').quantize(colors=160) for p in sorted((root/folder).glob('*.png'))]
    if not frames:
        raise RuntimeError(folder)
    frames[0].save(root/(name+'.gif'), save_all=True, append_images=frames[1:], duration=120, loop=0, optimize=False)
print('GIFs actualizados')
