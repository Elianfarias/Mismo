import subprocess, io
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
FF = r'C:/Users/elian/Mismo/Temp/showcase-tools/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe'
OUT = Path(__file__).parent
font = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 19)
def frame(src,t,w=480):
    p=subprocess.run([FF,'-v','error','-ss',str(t),'-i',src,'-frames:v','1','-vf',f'scale={w}:-1','-f','image2pipe','-vcodec','mjpeg','-threads','1','-'],capture_output=True,check=True)
    return Image.open(io.BytesIO(p.stdout)).convert('RGB')
def sheets(src,times,name):
    for start in range(0,len(times),16):
        sheet=Image.new('RGB',(1920,1200),(20,22,25)); draw=ImageDraw.Draw(sheet)
        for i,t in enumerate(times[start:start+16]):
            im=frame(src,t); x=(i%4)*480; y=(i//4)*300
            sheet.paste(im,(x,y)); draw.text((x+8,y+272),f'{name}  {int(t)//60:02}:{t%60:04.1f}',font=font,fill='white')
        p=OUT/f'{name}_{start//16+1}.jpg'; sheet.save(p,quality=90); print(p,flush=True)
if __name__=='__main__':
    sheets('D:/Mismo/Videos/Gameplay.mp4',list(range(0,318,5)),'gameplay')
    sheets('D:/Mismo/Videos/Muestra de menu.mp4',list(range(0,101,5)),'menu')
