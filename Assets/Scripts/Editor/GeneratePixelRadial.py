"""Generate original stepped radial UI assets without modifying existing artwork."""
from pathlib import Path
import math
import uuid
import re
from PIL import Image, ImageFilter

ROOT = next(p for p in Path(__file__).resolve().parents if (p/'ProjectSettings/ProjectVersion.txt').exists())
OUT = ROOT/'Assets/Art/UI/PixelFrames'
OUT.mkdir(parents=True, exist_ok=True)
SIZE = 256
SECTORS = 7
INK = (225,230,224,255)
DARK = (8,12,14,255)
SURFACE = (12,18,21,235)
GOLD = (239,206,127,255)

def mask(sector=None):
    im=Image.new('L',(SIZE,SIZE))
    pixels=[]
    for y in range(SIZE):
        for x in range(SIZE):
            dx,dy=x*2+1-256,256-(y*2+1)
            r=math.hypot(dx,dy)
            angle=math.degrees(math.atan2(dx,dy))
            delta=abs((angle-(sector or 0)*(360/SECTORS)+180)%360-180)
            pixels.append(255 if (r<80 if sector is None else 92<=r<=248 and delta<=180/SECTORS-2) else 0)
    im.putdata(pixels)
    return im

def layer(color,mask):
    im=Image.new('RGBA',mask.size,color)
    im.putalpha(mask.point(lambda a:round(a*color[3]/255)))
    return im

def border(mask,color=INK):
    im=layer(DARK,mask)
    im.paste(color,(0,0,SIZE,SIZE),mask.filter(ImageFilter.MinFilter(3)))
    im.paste(DARK,(0,0,SIZE,SIZE),mask.filter(ImageFilter.MinFilter(5)))
    im.paste((0,0,0,0),(0,0,SIZE,SIZE),mask.filter(ImageFilter.MinFilter(7)))
    return im

def save(name,im):
    p=OUT/(name+'.png')
    im.resize((512,512),Image.Resampling.NEAREST).save(p)
    meta=Path(str(p)+'.meta')
    guid=re.search(r'^guid: (\w+)',meta.read_text(encoding='utf-8'),re.M).group(1) if meta.exists() else uuid.uuid4().hex
    meta.write_text(f'''fileFormatVersion: 2
guid: {guid}
TextureImporter:
  serializedVersion: 13
  mipmaps:
    enableMipMap: 0
  isReadable: 0
  textureSettings:
    serializedVersion: 2
    filterMode: 0
    aniso: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  textureType: 8
  spriteMode: 1
  spriteMeshType: 0
  spritePixelsToUnits: 100
  spritePivot: {{x: 0.5, y: 0.5}}
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  alphaIsTransparency: 1
  textureCompression: 0
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 1024
    textureFormat: -1
    textureCompression: 0
    overridden: 0
''',encoding='utf-8')

outline=Image.new('RGBA',(SIZE,SIZE))
surface=Image.new('RGBA',(SIZE,SIZE))
for i in [None,*range(SECTORS)]:
    shape=mask(i)
    outline.alpha_composite(border(shape))
    surface.alpha_composite(layer(SURFACE,shape.filter(ImageFilter.MinFilter(7))))
    if i is not None:
        selected=layer((45,43,30,245),shape.filter(ImageFilter.MinFilter(7)))
        selected.alpha_composite(border(shape,GOLD))
        save('RadialSelected'+str(i),selected)
save('RadialOutline',outline)
save('RadialSurface',surface)
preview=Image.new('RGBA',(SIZE,SIZE),(27,39,43,255))
preview.alpha_composite(surface);preview.alpha_composite(outline)
review=ROOT/'Docs/Previews/PixelRadial.png';review.parent.mkdir(parents=True,exist_ok=True)
preview.resize((512,512),Image.Resampling.NEAREST).convert('RGB').save(review)
print('Generated 9 radial sprites: 512px RGBA, 2px steps, seven sectors including Recipes, and center.')
