"""Generate the recipe UI's code-native pixel geometry (Python + Pillow).

No concept-art pixels or baked text are used. Existing game assets are not changed.
"""
from pathlib import Path
import json
import re
import uuid
from PIL import Image, ImageDraw, ImageFont

ROOT = next(p for p in Path(__file__).resolve().parents if (p/'ProjectSettings/ProjectVersion.txt').exists())
OUT = ROOT/'Assets/Art/UI/Recipes'
OUT.mkdir(parents=True, exist_ok=True)
INK = (234, 232, 211, 255)
GOLD = (239, 206, 127, 255)
DARK = (8, 12, 14, 255)
CLEAR = (0, 0, 0, 0)
entries = []
images = {}

def guid_meta(path, folder=False):
    target = Path(str(path)+'.meta')
    if not target.exists():
        extra = 'folderAsset: yes\nDefaultImporter:\n' if folder else 'DefaultImporter:\n'
        target.write_text(f'fileFormatVersion: 2\nguid: {uuid.uuid4().hex}\n{extra}', encoding='utf-8')

def save(name, im, border=(0, 0, 0, 0)):
    path = OUT/(name+'.png')
    im.save(path)
    meta = Path(str(path)+'.meta')
    if meta.exists():
        # Preserve Unity-generated sprite IDs as well as the asset GUID.
        text = meta.read_text(encoding='utf-8')
        text = re.sub(r'spriteBorder: \{[^}]*\}', 'spriteBorder: {x: %s, y: %s, z: %s, w: %s}' % border, text)
    else:
        text = f'''fileFormatVersion: 2
guid: {uuid.uuid4().hex}
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
  textureShape: 1
  spriteMode: 1
  spriteMeshType: 0
  spritePixelsToUnits: 100
  spritePivot: {{x: 0.5, y: 0.5}}
  spriteBorder: {{x: {border[0]}, y: {border[1]}, z: {border[2]}, w: {border[3]}}}
  alphaIsTransparency: 1
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    textureFormat: -1
    textureCompression: 0
    overridden: 0
'''
    meta.write_text(text, encoding='utf-8')
    entries.append({'key': name, 'path': path.relative_to(ROOT).as_posix(), 'border': list(border)})
    images[name] = im

def contour(w, h, inset, cut):
    a, b, r, t = inset, inset, w-1-inset, h-1-inset
    step = max(1, cut//2)
    return [(a+cut,b),(r-cut,b),(r-cut,b+step),(r-step,b+step),(r-step,b+cut),(r,b+cut),
            (r,t-cut),(r-step,t-cut),(r-step,t-step),(r-cut,t-step),(r-cut,t),
            (a+cut,t),(a+cut,t-step),(a+step,t-step),(a+step,t-cut),(a,t-cut),
            (a,b+cut),(a+step,b+cut),(a+step,b+step),(a+cut,b+step)]

def frame(color=INK, fill=CLEAR):
    im=Image.new('RGBA',(32,32)); d=ImageDraw.Draw(im)
    d.polygon(contour(32,32,1,5),fill=fill)
    for inset,cut,c,width in [(1,5,DARK,2),(3,4,color,2),(5,3,DARK,1)]:
        p=contour(32,32,inset,cut); d.line(p+[p[0]],fill=c,width=width)
    return im

save('WindowFrame',frame(),(9,9,9,9))
save('WindowSurface',Image.new('RGBA',(4,4),(8,15,18,220)))
for name, color, fill in [
    ('ControlNormal',INK,(12,18,21,235)),
    ('ControlHover',(255,251,230,255),(32,42,44,245)),
    ('ControlSelected',GOLD,(53,49,29,240)),
    ('ControlPressed',(216,181,101,255),(32,30,20,255)),
    ('ControlDisabled',(111,123,122,255),(18,24,26,230))]:
    save(name,frame(color,fill),(9,9,9,9))
for name,color in [('RowHover',(220,226,215,22)),('RowSelected',(239,206,127,38))]:
    im=Image.new('RGBA',(32,32),color); d=ImageDraw.Draw(im)
    if name=='RowSelected':
        d.rectangle((0,0,31,0),fill=(239,206,127,170)); d.rectangle((0,31,31,31),fill=(239,206,127,170))
        d.rectangle((0,0,2,31),fill=GOLD)
    save(name,im,(4,4,4,4))
im=Image.new('RGBA',(16,3)); ImageDraw.Draw(im).line((0,1,15,1),fill=(204,216,210,95))
save('RowSeparator',im,(2,0,2,0))
im=Image.new('RGBA',(24,5)); d=ImageDraw.Draw(im)
d.line((2,2,21,2),fill=INK); d.rectangle((1,1,2,3),fill=INK); d.rectangle((21,1,22,3),fill=INK)
save('SectionDividerHorizontal',im,(4,0,4,0))
save('SectionDividerVertical',im.transpose(Image.Transpose.ROTATE_90),(0,4,0,4))
for name,color,width in [('ScrollTrack',(172,179,160,90),4),('ScrollThumb',GOLD,8)]:
    im=Image.new('RGBA',(width,16)); d=ImageDraw.Draw(im)
    d.rectangle((1,1,width-2,14),fill=color)
    save(name,im,(1,3,1,3))

# Icons are deliberately NOT generated. Reuse inventory icons and official Flaticon sources.
manifest=OUT/'RecipeUIManifest.json'
manifest.write_text(json.dumps({'entries':entries, 'iconSources':'Assets/Art/UI/Flaticon/Recipes/Sources.json'},indent=2)+'\n',encoding='utf-8')
guid_meta(manifest); guid_meta(OUT,True); guid_meta(Path(__file__))

# Static contact sheet of the actual generated components, not a concept render.
def sliced(src, w, h, border):
    l,b,r,t=border; sw,sh=src.size
    dst=Image.new('RGBA',(w,h))
    for ys,yd,hs,hd in [(0,0,t,t),(t,t,sh-t-b,h-t-b),(sh-b,h-b,b,b)]:
        for xs,xd,ws,wd in [(0,0,l,l),(l,l,sw-l-r,w-l-r),(sw-r,w-r,r,r)]:
            if min(ws,wd,hs,hd)>0:
                dst.paste(src.crop((xs,ys,xs+ws,ys+hs)).resize((wd,hd),Image.Resampling.NEAREST),(xd,yd))
    return dst

preview=Image.new('RGBA',(1200,490),(23,32,35,255)); d=ImageDraw.Draw(preview)
font=ImageFont.truetype('C:/Windows/Fonts/consola.ttf',17)
title=ImageFont.truetype('C:/Windows/Fonts/consola.ttf',27)
d.text((32,24),'MISMO / RECETAS — PIEZAS DE INTERFAZ',font=title,fill=INK)
d.text((32,67),'PNG separados · transparencia real · marcos de 9 cortes · sin textos incrustados',font=font,fill=INK)
preview.alpha_composite(sliced(images['WindowFrame'],(1136),180,(9,9,9,9)),(32,108))
d.text((53,126),'MARCO DE VENTANA / ESQUINAS CONSERVADAS',font=font,fill=INK)
states=['Normal','Hover','Selected','Pressed','Disabled']
for i,state in enumerate(states):
    x=52+i*220; preview.alpha_composite(sliced(images['Control'+state],205,63,(9,9,9,9)),(x,172))
    d.text((x+18,194),state,font=font,fill=INK)
d.text((53,253),'Filtros y botón de fabricar comparten las mismas piezas.',font=font,fill=INK)
for i,name in enumerate(['RowHover','RowSelected']):
    y=322+i*65; preview.alpha_composite(sliced(images[name],525,52,(4,4,4,4)),(32,y))
    d.text((48,y+16),name,font=font,fill=INK)
preview.alpha_composite(sliced(images['SectionDividerHorizontal'],555,5,(4,0,4,0)),(610,320))
preview.alpha_composite(sliced(images['SectionDividerVertical'],5,116,(0,4,0,4)),(610,340))
d.text((636,352),'Separadores / scroll',font=font,fill=INK)
preview.alpha_composite(sliced(images['ScrollTrack'],4,88,(1,3,1,3)),(1140,344))
preview.alpha_composite(sliced(images['ScrollThumb'],8,36,(1,3,1,3)),(1138,357))
dest=ROOT/'Docs/Previews/RecipeUIAssets.png';dest.parent.mkdir(parents=True,exist_ok=True)
preview.convert('RGB').save(dest)
print(f'{len(entries)} recipe UI sprites generated in {OUT}')
print(f'Contact sheet: {dest}')
