"""Reproducible, original UI primitives. Run with Python + Pillow from any directory."""
from pathlib import Path
import uuid
import argparse
import re
from PIL import Image, ImageDraw, ImageFont

ROOT = next(p for p in Path(__file__).resolve().parents if (p / 'ProjectSettings/ProjectVersion.txt').exists())
OUT = ROOT / 'Assets/Art/UI/PixelFrames'
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--preview-only', action='store_true', help='Render the proposal without changing Unity assets.')
args = parser.parse_args()
if not args.preview_only: OUT.mkdir(parents=True, exist_ok=True)
INK = (225, 230, 224, 255)
DARK = (8, 12, 14, 245)
SURFACE = (12, 18, 21, 220)

def contour(w, h, inset, cut):
    a, b, r, t = inset, inset, w-1-inset, h-1-inset
    # Square silhouette with exclusively horizontal/vertical pixel steps.
    step = max(1, cut // 2)
    return [(a+cut,b),(r-cut,b),
            (r-cut,b+step),(r-step,b+step),(r-step,b+cut),(r,b+cut),
            (r,t-cut),(r-step,t-cut),(r-step,t-step),(r-cut,t-step),(r-cut,t),
            (a+cut,t),(a+cut,t-step),(a+step,t-step),(a+step,t-cut),(a,t-cut),
            (a,b+cut),(a+step,b+cut),(a+step,b+step),(a+cut,b+step)]

def frame(w=32, h=32, color=INK, filled=False):
    im = Image.new('RGBA', (w,h))
    d = ImageDraw.Draw(im)
    if filled: d.polygon(contour(w,h,1,5), fill=SURFACE)
    for inset, cut, c, width in [(1,5,DARK,2),(3,4,color,2),(5,3,DARK,1)]:
        p = contour(w,h,inset,cut)
        d.line(p+[p[0]], fill=c, width=width)
    return im

def meta(path, border=9):
    target = Path(str(path)+'.meta')
    if target.exists():
        content = target.read_text(encoding='utf-8')
        content = re.sub(r'spriteBorder: \{[^}]*\}', f'spriteBorder: {{x: {border}, y: {border}, z: {border}, w: {border}}}', content)
        target.write_text(content, encoding='utf-8')
        return
    target.write_text(f'''fileFormatVersion: 2
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
  spriteBorder: {{x: {border}, y: {border}, z: {border}, w: {border}}}
  alphaIsTransparency: 1
  textureCompression: 0
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    textureFormat: -1
    textureCompression: 0
    overridden: 0
''', encoding='utf-8')

assets = {}
def save(name, im, border=9):
    path = OUT / (name+'.png')
    if not args.preview_only:
        im.save(path)
        meta(path,border)
    assets[name] = im

save('Frame', frame())
save('Panel', frame(filled=True))
save('FrameHover', frame(color=(255,255,247,255)))
save('FrameSelected', frame(color=(239,206,127,255)))
save('FrameDisabled', frame(color=(100,114,119,180)))
save('BarFrame', frame(32,24))
for name, color in [('HealthFill',(205,61,71,255)),('StaminaFill',(99,181,112,255)),('ManaFill',(63,148,215,255))]:
    im = Image.new('RGBA',(8,8),color)
    ImageDraw.Draw(im).line((0,0,7,0),fill=tuple(min(255,c+28) for c in color[:3])+(255,))
    save(name, im, 0)
save('Surface', Image.new('RGBA',(8,8),SURFACE),0)
save('CooldownOverlay', Image.new('RGBA',(8,8),(5,9,12,180)),0)
for name, gap in [('Crosshair',3),('CrosshairPrecise',2)]:
    im = Image.new('RGBA',(25,25)); d=ImageDraw.Draw(im)
    for box in [(12,12-gap-3,12,12-gap),(12,12+gap,12,12+gap+3),(12-gap-3,12,12-gap,12),(12+gap,12,12+gap+3,12)]:
        x,y,r,b=box
        d.rectangle((x-1,y-1,r+1,b+1),fill=DARK)
    for box in [(12,12-gap-3,12,12-gap),(12,12+gap,12,12+gap+3),(12-gap-3,12,12-gap,12),(12+gap,12,12+gap+3,12)]: d.rectangle(box,fill=INK)
    save(name,im,0)

# Nine-pixel corners retain all steps and dark outlines when stretched.
def sliced(src,w,h):
    dst=Image.new('RGBA',(w,h)); sw,sh=src.size
    xs,ys=[0,9,sw-9,sw],[0,9,sh-9,sh]
    xd,yd=[0,9,w-9,w],[0,9,h-9,h]
    for j in range(3):
        for i in range(3):
            tile=src.crop((xs[i],ys[j],xs[i+1],ys[j+1]))
            tile=tile.resize((xd[i+1]-xd[i],yd[j+1]-yd[j]),Image.Resampling.NEAREST)
            dst.paste(tile,(xd[i],yd[j]))
    return dst

canvas=Image.new('RGBA',(1000,680),(27,39,43,255)); draw=ImageDraw.Draw(canvas)
fontpath='C:/Windows/Fonts/consola.ttf'
font=ImageFont.truetype(fontpath,16); title=ImageFont.truetype(fontpath,26)
def text(x,y,s,big=False,color=INK): draw.text((x,y),s,font=title if big else font,fill=color)
def panel(x,y,w,h): canvas.alpha_composite(frame(w,h,filled=True),(x,y))
def slot(x,y,n,state='Frame'):
    color=(239,206,127,255) if state=='FrameSelected' else INK
    canvas.alpha_composite(frame(52,52,color,filled=True),(x,y)); text(x+17,y+17,n)
text(36,25,'MISMO / UN SOLO MARCO',True)
text(36,66,'Casillas cuadradas · lados rectos · esquinas escalonadas en píxeles')
panel(36,110,288,345); text(54,129,'MENÚ',True)
for i,label in enumerate(['Continuar','Personaje','Habilidades','Opciones']):
    panel(54,180+i*59,252,45); text(70,193+i*59,label)
panel(344,110,620,345); text(365,129,'INVENTARIO',True)
for row in range(3):
    for col in range(9): slot(365+col*64,184+row*67,'', 'FrameSelected' if (row,col)==(0,2) else 'Frame')
text(365,403,'Mismo marco para equipo, objetos y consumibles')
text(36,484,'HABILIDADES / CONSUMIBLES')
for i,k in enumerate(['1','2','3','4','Q','E']): slot(36+i*64,515,k,'FrameSelected' if i==1 else 'Frame')
text(478,484,'VIDA / ESTAMINA')
for y,name,value in [(519,'HealthFill',.78),(563,'StaminaFill',.57)]:
    panel(478,y,302,34)
    fill=Image.new('RGBA',(302,34)); fd=ImageDraw.Draw(fill)
    fd.polygon(contour(302,34,7,2),fill=assets[name].getpixel((3,3)))
    fd.rectangle((int(302*value),0,302,34),fill=(0,0,0,0))
    canvas.alpha_composite(fill,(478,y))
    canvas.alpha_composite(frame(302,34),(478,y))
text(833,484,'MIRA')
canvas.alpha_composite(assets['Crosshair'].resize((50,50),Image.Resampling.NEAREST),(851,525))
text(36,622,'PROPUESTA CORREGIDA / misma silueta en paneles, casillas y barras')
preview=ROOT/'Docs/Previews/PixelFrames.png'; preview.parent.mkdir(parents=True,exist_ok=True)
canvas.convert('RGB').save(preview)
foldermeta=OUT.with_suffix('.meta')
if not args.preview_only and not foldermeta.exists(): foldermeta.write_text(f'fileFormatVersion: 2\nguid: {uuid.uuid4().hex}\nfolderAsset: yes\nDefaultImporter:\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
print(f'Preview saved: {preview}' if args.preview_only else f'{len(assets)} sprites generated in {OUT}')
