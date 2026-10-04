import subprocess, io
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
FF=r'C:/Users/elian/Mismo/Temp/showcase-tools/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe'
ROOT=Path(__file__).parent/'itch-selection'
ROOT.mkdir(exist_ok=True)
G='D:/Mismo/Videos/Gameplay.mp4'
M='D:/Mismo/Videos/Muestra de menu.mp4'
choices=[('01_combate',G,291.8),('02_combate_alternativo',G,311.0),('03_exploracion',G,65.0),('04_arco_piedra',G,59.0),('05_pueblo',M,75.5),('06_plaza',M,99.6),('07_habilidades',G,268.0),('08_personaje',G,15.0),('09_arco',G,211.1)]
sheet=Image.new('RGB',(1440,918),(19,23,26))
d=ImageDraw.Draw(sheet); font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',18)
for i,(name,src,t) in enumerate(choices):
    target=ROOT/(name+'.png')
    p=subprocess.run([FF,'-v','error','-nostdin','-y','-ss',str(t),'-i',src,'-frames:v','1','-c:v','png','-threads','1',str(target)],capture_output=True,check=True)
    im=Image.open(target); im.thumbnail((480,270));x=i%3*480;y=i//3*306
    sheet.paste(im,(x,y));d.text((x+10,y+276),f'{name} | {t:.1f}s',font=font,fill='white')
    print(target,flush=True)
sheet.save(ROOT/'selection.jpg',quality=94)
