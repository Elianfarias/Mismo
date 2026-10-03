import subprocess, json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageEnhance
from inspect_video import FF, frame
ROOT=Path(__file__).resolve().parent.parent
WORK=ROOT/'review'
G='D:/Mismo/Videos/Gameplay.mp4'
M='D:/Mismo/Videos/Muestra de menu.mp4'
shots=[
 ('01_accion',G,290,5.5,'MISMO','Exploración y combate voxel'),
 ('02_mundo',G,54,5,'UN MUNDO POR DESCUBRIR','Explorá mundos generados proceduralmente'),
 ('03_pueblo',M,76,5.5,'ENTRÁ EN LA AVENTURA','Pueblos, personajes y misiones'),
 ('04_equipo',M,21,3.5,'ELEGÍ TU EQUIPO','Un personaje. Distintas formas de combatir.'),
 ('05_habilidades',G,261.5,4,'LAS HABILIDADES ESTÁN EN EL ARMA','Armá tu combinación'),
 ('06_arco',G,330-120,6,'CAMBIÁ TU FORMA DE COMBATIR','Arco y combate a distancia'),
 ('07_espada',G,233,5.8,'ESPADA Y COMBOS','Enfrentá a varios enemigos'),
 ('08_fabricacion',M,30,3.2,'PREPARATE PARA SEGUIR','Fabricación de objetos'),
 ('09_mapa',M,48,3,'BUSCÁ TU PRÓXIMO DESTINO','Mapa del mundo'),
 ('10_remate',G,310,5,'SEGUÍ TU PROPIA AVENTURA',''),
]
def font(size,bold=False): return ImageFont.truetype('C:/Windows/Fonts/'+('arialbd.ttf' if bold else 'arial.ttf'),size)
def centered(d,y,text,f,color):
    b=d.textbbox((0,0),text,font=f); d.text(((1920-(b[2]-b[0]))/2,y),text,font=f,fill=color)
def overlay(name,title,sub):
    im=Image.new('RGBA',(1920,1080)); d=ImageDraw.Draw(im)
    # Place captions above the bottom HUD; preserve all actual game framing.
    for y in range(780,984):
        a=int(190*min(1,(y-780)/75)); d.line((0,y,1919,y),fill=(9,17,20,a))
    d.rectangle((82,838,88,931),fill=(217,188,111,255))
    d.text((112,836),title,font=font(42,True),fill=(249,242,221,255))
    if sub:d.text((114,893),sub,font=font(29),fill=(232,236,230,255))
    im.save(WORK/f'{name}.png')
def run(args,log):
    with open(WORK/log,'w',encoding='utf8') as f:
        subprocess.run([FF,'-hide_banner','-y','-nostdin']+args,stdout=f,stderr=f,check=True)
timeline=[];cursor=0
for name,src,start,dur,title,sub in shots:
    overlay(name,title,sub)
    graph=f'[0:v]scale=1920:1080:flags=lanczos,setsar=1,fps=30[v];[1:v]format=rgba,fade=t=in:st=0:d=0.2:alpha=1,fade=t=out:st={dur-0.35}:d=0.25:alpha=1[o];[v][o]overlay=0:0:shortest=1,format=yuv420p[out]'
    run(['-ss',str(start),'-t',str(dur),'-i',src,'-loop','1','-i',str(WORK/f'{name}.png'),'-filter_complex_threads','2','-filter_complex',graph,'-map','[out]','-map','0:a:0','-af',f'volume=12dB,alimiter=limit=0.89:level=false,afade=t=in:d=0.06,afade=t=out:st={dur-0.12}:d=0.12','-t',str(dur),'-c:v','libx264','-preset','fast','-crf','19','-threads','4','-c:a','aac','-b:a','192k','-ar','48000','-ac','2',str(WORK/f'{name}.mp4')],name+'.log')
    timeline.append(dict(clip=name,source=src,source_start=start,source_end=start+dur,showcase_start=cursor,showcase_end=cursor+dur,title=title))
    cursor+=dur; print('Rendered',name,flush=True)
# End card uses the real village from the user's recording.
bg=frame(M,80,1920).resize((1920,1080)).filter(ImageFilter.GaussianBlur(5)).convert('RGBA')
bg=Image.alpha_composite(bg,Image.new('RGBA',bg.size,(7,17,21,190)))
d=ImageDraw.Draw(bg)
centered(d,262,'MISMO',ImageFont.truetype('C:/Windows/Fonts/georgiab.ttf',154),(233,208,137,255))
centered(d,468,'Tu arma. Tus habilidades. Tu aventura.',font(43),(249,245,234,255))
centered(d,622,'Disponible para Windows',font(31),(216,226,222,255))
centered(d,680,'elianity.itch.io/mismo',font(44,True),(249,245,234,255))
centered(d,885,'Desarrollado por Elián Farias  ·  Unity / C#',font(28),(193,205,200,255))
bg.convert('RGB').save(WORK/'endcard.png')
run(['-loop','1','-i',str(WORK/'endcard.png'),'-f','lavfi','-i','anullsrc=r=48000:cl=stereo','-t','4.5','-vf','fps=30,format=yuv420p,fade=t=in:d=0.3,fade=t=out:st=4.1:d=0.4','-c:v','libx264','-preset','fast','-crf','19','-threads','4','-c:a','aac','-b:a','192k',str(WORK/'11_cierre.mp4')],'endcard.log')
timeline.append(dict(clip='11_cierre',showcase_start=cursor,showcase_end=cursor+4.5,title='MISMO — Windows e itch.io'))
(WORK/'concat.txt').write_text('\n'.join("file '"+str(WORK/(s[0]+'.mp4')).replace('\\','/')+"'" for s in shots)+"\nfile '"+str(WORK/'11_cierre.mp4').replace('\\','/')+"'\n",encoding='utf8')
run(['-f','concat','-safe','0','-i',str(WORK/'concat.txt'),'-c','copy','-movflags','+faststart','-metadata','title=Mismo | Gameplay Showcase','-metadata','artist=Elian Farias',str(ROOT/'Mismo_Showcase.mp4')],'concat.log')
(WORK/'timeline.json').write_text(json.dumps(timeline,ensure_ascii=False,indent=2),encoding='utf8')
def tc(t): return f'{int(t)//60:02}:{t%60:04.1f}'
lines=['MISMO — SELECCIÓN DE MOMENTOS','Montaje en 1920 × 1080, 30 fps. Audio de las grabaciones originales.','']
for t in timeline:
    lines.append(f"Showcase {tc(t['showcase_start'])}–{tc(t['showcase_end'])} | {t['title']}")
    if 'source' in t: lines.append(f"  Original: {Path(t['source']).name} | {tc(t['source_start'])}–{tc(t['source_end'])}")
(ROOT/'Momentos_seleccionados.txt').write_text('\n'.join(lines),encoding='utf8')
print('DONE',ROOT/'Mismo_Showcase.mp4',cursor+4.5,flush=True)
