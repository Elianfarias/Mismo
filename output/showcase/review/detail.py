from inspect_video import *
def sequence(src,start,duration,name):
    p=subprocess.run([FF,'-v','error','-ss',str(start),'-i',src,'-t',str(duration),'-vf','fps=1,scale=480:270','-f','rawvideo','-pix_fmt','rgb24','-threads','1','-'],capture_output=True,check=True)
    n=len(p.stdout)//(480*270*3)
    sheet=Image.new('RGB',(1920,300*((n+3)//4)),(20,22,25)); d=ImageDraw.Draw(sheet)
    for i in range(n):
        im=Image.frombytes('RGB',(480,270),p.stdout[i*388800:(i+1)*388800]); x=(i%4)*480; y=(i//4)*300
        sheet.paste(im,(x,y)); t=start+i; d.text((x+8,y+274),f'{name} {t//60:02}:{t%60:02}',font=font,fill='white')
    sheet.save(OUT/f'{name}.jpg',quality=92); print(name,flush=True)
g='D:/Mismo/Videos/Gameplay.mp4'; m='D:/Mismo/Videos/Muestra de menu.mp4'
for args in [(g,287,12,'hook'),(g,258,12,'skills'),(g,208,12,'bow'),(g,228,12,'duel'),(m,73,12,'town')]: sequence(*args)
