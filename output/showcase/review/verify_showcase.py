from inspect_video import *
import json
ROOT=OUT.parent
video=ROOT/'Mismo_Showcase.mp4'
timeline=json.loads((OUT/'timeline.json').read_text(encoding='utf8'))
meta=[';FFMETADATA1','title=Mismo | Gameplay Showcase','artist=Elian Farias']
for s in timeline:
    meta.extend(['[CHAPTER]','TIMEBASE=1/1000',f"START={round(s['showcase_start']*1000)}",f"END={round(s['showcase_end']*1000)}",'title='+s['title']])
(OUT/'chapters.txt').write_text('\n'.join(meta),encoding='utf8')
p=subprocess.run([FF,'-v','error','-y','-i',str(video),'-i',str(OUT/'chapters.txt'),'-map_metadata','1','-map_chapters','1','-map','0:v','-map','0:a','-c','copy','-movflags','+faststart',str(OUT/'chaptered.mp4')],capture_output=True,check=True)
(OUT/'chaptered.mp4').replace(video)
p=subprocess.run([FF,'-hide_banner','-i',str(video),'-af','volumedetect','-f','null','-'],capture_output=True,text=True,encoding='utf8',errors='replace')
(OUT/'validation.log').write_text(p.stderr,encoding='utf8')
assert p.returncode==0,p.stderr
times=[round((s['showcase_start']+s['showcase_end'])/2,2) for s in timeline]
sheets(str(video),times,'final_review')
frame(str(video),times[4],1280).save(OUT/'check_skills.jpg')
frame(str(video),times[-1],1280).save(OUT/'check_end.jpg')
print('VERIFIED',video.stat().st_size,flush=True)
print(p.stderr[-1500:],flush=True)
