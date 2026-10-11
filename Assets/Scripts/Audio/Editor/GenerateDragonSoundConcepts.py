"""Soul Eater replacement audio auditions. Requires NumPy and SoundFile.

Generates Concepts and a listening page; never changes live clips or prefabs.
python Assets/Scripts/Audio/Editor/GenerateDragonSoundConcepts.py
"""
from pathlib import Path
from functools import lru_cache
import base64
import hashlib
import io
import json
import sys
import uuid
import wave
import numpy as np

ROOT = Path(__file__).resolve().parents[4]
try:
    import soundfile as sf
except ModuleNotFoundError:
    sys.path.insert(0, str(ROOT/'.validation/audio-python'))
    import soundfile as sf

BASE = ROOT/'Assets/Art/Audio/Enemies/SoulEater'
SOURCE = BASE/'Source'
DEST = BASE/'Concept'
OUT = ROOT/'output/dragon-audio'
DOC = ROOT/'Assets/Documentation/Audio/DragonSoundConcepts-source.json'
RATE = 48000
LOOPS = {'FlameLoop', 'GroundFireLoop', 'WingLoop', 'ChargeLoop'}
LABELS = {
    'Roar_A': 'A · Rugido grave', 'RoarShort_A': 'A · Rugido corto', 'Hit_A': 'A · Hit recibido', 'Death_A': 'A · Muerte',
    'Roar_B': 'B · Rugido áspero', 'RoarShort_B': 'B · Rugido corto', 'Hit_B': 'B · Hit recibido', 'Death_B': 'B · Muerte',
    'Inhale': 'Preparación · inhalación', 'FlameStart': 'Encendido del aliento', 'FlameLoop': 'Aliento sostenido · loop',
    'FlameEnd': 'Final del aliento', 'GroundFireLoop': 'Fuego sobre el suelo · loop',
    'WingFlap': 'Un aleteo', 'WingLoop': 'Aleteo continuo · loop', 'Jump': 'Despegue / salto', 'Dive': 'Descenso en picado',
    'Footstep_01': 'Pisada pesada · 1', 'Footstep_02': 'Pisada pesada · 2', 'Land': 'Aterrizaje', 'DiveImpact': 'Impacto de la caída',
    'Bite_Prepare': 'Preparación · mordida', 'Bite': 'Mordida', 'Tail_Prepare': 'Preparación · coletazo', 'Tail': 'Coletazo',
    'Charge_Prepare': 'Preparación · carga', 'Charge': 'Arranque de la carga', 'ChargeLoop': 'Carrera · loop', 'Brake': 'Frenada y arrastre',
}


def meta(path, kind='default'):
    dest = Path(str(path)+'.meta')
    if dest.exists(): return
    guid = uuid.uuid5(uuid.NAMESPACE_URL, 'mismo-dragon-audio/'+path.relative_to(ROOT).as_posix()).hex
    if kind == 'audio':
        body = ('AudioImporter:\n  externalObjects: {}\n  serializedVersion: 8\n'
                '  defaultSettings:\n    serializedVersion: 2\n    loadType: 0\n'
                '    sampleRateSetting: 0\n    sampleRateOverride: 48000\n'
                '    compressionFormat: 0\n    quality: 1\n    conversionMode: 0\n'
                '    preloadAudioData: 1\n  platformSettingOverrides: {}\n'
                '  forceToMono: 1\n  normalize: 0\n  loadInBackground: 0\n  ambisonic: 0\n  3D: 1\n')
    else:
        body = ('folderAsset: yes\n' if kind == 'folder' else '')
        body += ('TextScriptImporter' if kind == 'text' else 'DefaultImporter')+':\n  externalObjects: {}\n'
    dest.write_text(f'fileFormatVersion: 2\nguid: {guid}\n'+body+'  userData:\n  assetBundleName:\n  assetBundleVariant:\n', encoding='utf-8')


def empty(seconds): return np.zeros(round(seconds*RATE))


def band(a, low=45, high=8000, periodic=False):
    x = a if periodic else np.pad(a, (4096,4096))
    f = np.fft.rfftfreq(len(x), 1/RATE)
    shape = (1-np.exp(-(f/low)**4))*np.exp(-(f/high)**4)
    y = np.fft.irfft(np.fft.rfft(x)*shape,len(x))
    return y if periodic else y[4096:-4096]


def fade(a, attack=.008, release=.07):
    a = a.copy()
    n = min(round(attack*RATE),len(a)//2); m = min(round(release*RATE),len(a)//2)
    if n: a[:n] *= np.sin(np.linspace(0,np.pi/2,n))**2
    if m: a[-m:] *= np.cos(np.linspace(0,np.pi/2,m))**2
    a[0] = a[-1] = 0
    return a


@lru_cache(maxsize=None)
def recording(name):
    a, rate = sf.read(SOURCE/name,always_2d=True)
    return a.mean(axis=1), rate


def take(name, start, end, speed=1, low=50, high=7500, edges=True):
    a,rate = recording(name)
    a = a[round(start*rate):round(end*rate)]
    assert len(a)>100 and speed>0
    if speed*rate/RATE>1:
        # Filter the original samples before decimating high-rate recordings.
        f=np.fft.rfftfreq(len(a),1/rate)
        a=np.fft.irfft(np.fft.rfft(a)*np.exp(-(f/(.44*RATE/speed))**12),len(a))
    a = np.interp(np.arange(0,len(a)-1,speed*rate/RATE),np.arange(len(a)),a)
    a = band(a-a.mean(),low,high)
    a *= min(.14/max(np.std(a),1e-7),.7/max(abs(a).max(),1e-7))
    return fade(a) if edges else a


def mix(a,b,at=0,gain=1):
    offset=round(at*RATE); count=min(len(b),len(a)-offset)
    if count>0: a[offset:offset+count] += b[:count]*gain


def air(seconds,seed=1,low=70,high=6200):
    x=band(np.random.default_rng(seed).normal(size=round(seconds*RATE)),low,high,True)
    return x/max(np.std(x),1e-7)*.10


def gust(seconds,seed=1,gain=1):
    t=np.arange(round(seconds*RATE))/RATE
    envelope=np.sin(np.pi*np.linspace(0,1,len(t)))**1.8
    return fade(air(seconds,seed,120,6000)*envelope*gain)


def weight(seconds,seed=1):
    # Low rumble with an irregular transient, avoiding a prominent pitched boom.
    t=np.arange(round(seconds*RATE))/RATE
    return fade(air(seconds,seed,35,180)*(1-np.exp(-t/.009))*np.exp(-t/.17),.004,.08)


def echo(a,delay=.085,gain=.12):
    y=a.copy(); d=round(delay*RATE)
    y[d:]+=a[:-d]*gain
    y[d*2:]+=a[:-d*2]*gain*.45
    return y


def loop_recording(name,start,seconds,speed=1,low=50,high=7000):
    n=round(seconds*RATE); cross=round(.16*RATE)
    x=take(name,start,start+(seconds+.16)*speed,speed,low,high,False)
    assert len(x)>=n+cross-2
    x=np.pad(x,(0,max(0,n+cross-len(x))))
    y=x[:n].copy(); w=np.sin(np.linspace(0,np.pi/2,cross))**2
    y[:cross]=x[n:n+cross]*(1-w)+x[:cross]*w
    return y


def finish(a,level=.10,loop=False):
    a=a-a.mean()
    if loop:
        # Match the seam without a fade-to-silence hole in sustained effects.
        a[-96:] += np.linspace(0,a[0]-a[-1],96)
    else: a=fade(a)
    return a*min(level/max(np.std(a),1e-7),.66/max(abs(a).max(),1e-7))


def design():
    c={}
    bear=lambda a,b,s=.78: take('bear_01.flac',a,b,s,65,5500)
    bear2=lambda a,b,s=.83: take('bear_02.flac',a,b,s,65,6000)
    dragon=lambda a,b,s=.9: take('Dragon_Roar_JoelAudio_HQ.mp3',a,b,s,55,6900)
    hiss=lambda a,b,s=.92: take('Dino_Hiss_999999990_HQ.mp3',a,b,s,110,7900)
    rock=lambda a,b,s=.8,low=75,high=6500: take('Falling_Rock.wav',a,b,s,low,high)
    wing=lambda a,b,s=.84: take('wings_flap_large.flac',a,b,s,65,7200)
    for profile in ('A','B'):
        a=empty(2.55)
        if profile=='A':
            mix(a,dragon(.02,1.95,.88),.015,.90); mix(a,bear(.025,.99,.62),.11,.38)
        else:
            mix(a,hiss(2.96,5.15,.93),.006,.96); mix(a,dragon(.13,1.5,.80),.08,.28)
        c['Roar_'+profile]=echo(a)
        a=empty(1.60)
        mix(a,dragon(.09,1.36,.88) if profile=='A' else hiss(3.01,4.38,.97),.006)
        mix(a,bear2(.12,.84,.79),.025,.28)
        c['RoarShort_'+profile]=a
        a=empty(.58)
        mix(a,bear2(.08,.53,1.03) if profile=='A' else hiss(3.08,3.58,1.13),.005,.90)
        mix(a,bear(.16,.56,1.05),.035,.23)
        c['Hit_'+profile]=a
        a=empty(3.65)
        mix(a,dragon(.24,1.70,.77) if profile=='A' else hiss(3.35,5.12,.82),.01,.80)
        mix(a,bear2(.025,1.10,.63),1.18,.60)
        mix(a,rock(1.01,2.27,.67),1.65,.65);mix(a,weight(1.4,93),1.67,.7)
        c['Death_'+profile]=a
    a=empty(.76);mix(a,bear2(.06,.64,.85),.006,.60);mix(a,hiss(.11,.57,.92),.04,.30);c['Bite_Prepare']=a
    a=empty(.43);mix(a,bear(.10,.39,1.03),.003,.7);mix(a,wing(.07,.25,1.5),.01,.75);mix(a,rock(1.06,1.25,1.16),.012,.18);c['Bite']=a
    a=empty(.88);mix(a,gust(.8,6),.008,.65);mix(a,bear2(.25,.81,.80),.035,.35);c['Tail_Prepare']=a
    a=empty(.68);mix(a,wing(1.05,1.54,.93),.006,.8);mix(a,gust(.5,8),.03,.8);mix(a,weight(.45,9),.16,.35);c['Tail']=a
    a=empty(1.38);mix(a,hiss(.10,1.20,.85),.008,.64)
    t=np.arange(len(a))/RATE; a+=air(1.38,12,120,4500)*(.18+.8*(t/1.38)**1.4)
    mix(a,bear2(.04,.57,.72),.58,.20);c['Inhale']=a
    a=empty(.62);mix(a,take('Fire_Loop_qubodup.flac',.2,.75,.91,90,9000),.005,.65)
    mix(a,gust(.6,22),0,1.1);c['FlameStart']=a
    a=loop_recording('Fire_Loop_qubodup.flac',.42,2.4,.84,65,9000)
    t=np.arange(len(a))/RATE
    a+=air(2.4,24,40,1450)*(.68+.18*np.sin(2*np.pi*5*t))
    c['FlameLoop']=a
    a=empty(.88);mix(a,take('Fire_Loop_qubodup.flac',3.05,3.91,1,120,9000),.002,.85)
    a*=np.exp(-np.arange(len(a))/RATE/.25);c['FlameEnd']=a
    c['GroundFireLoop']=loop_recording('Fire_Loop_qubodup.flac',1.15,2.8,.96,250,9500)
    a=empty(.85);mix(a,wing(.006,.59,.76),.005,.84);mix(a,gust(.78,30),.025,.65);mix(a,weight(.45,31),.045,.26);c['WingFlap']=a
    a=empty(2.1);mix(a,c['WingFlap'],.0,.86);mix(a,wing(1.01,1.61,.79),1.05,.86);mix(a,gust(.79,32),1.065,.65);c['WingLoop']=a
    a=empty(.98);mix(a,c['WingFlap'],.08,.95);mix(a,rock(1.05,1.40,.9),.003,.48);mix(a,weight(.50,35),.008,.46);c['Jump']=a
    a=empty(.62);mix(a,gust(.61,38),.003,1.35);a*=np.linspace(.2,1.2,len(a));c['Dive']=a
    for i,start in enumerate((1.025,2.60),1):
        a=empty(.64);mix(a,rock(start,start+.4,.83,90,5400),.003,.73);mix(a,weight(.59,40+i),.004,1.25);c[f'Footstep_0{i}']=a
    a=empty(1.40);mix(a,rock(1.02,1.94,.79),.003,.86);mix(a,rock(2.59,3.19,.70,45,900),.008,.84);mix(a,weight(1,47),.002,.80);c['Land']=echo(a,.07,.085)
    a=empty(2.05);mix(a,rock(1.02,2.31,.74),.005,.95);mix(a,rock(1.05,1.99,.55,40,1200),.007,.75);mix(a,weight(1.7,48),.002,1.20);mix(a,rock(3.3,4.21,.89),.72,.42);c['DiveImpact']=echo(a,.105,.12)
    a=empty(.70);mix(a,bear(.04,.52,.73),.005,.68);mix(a,rock(3.2,3.63,.85),.07,.35);c['Charge_Prepare']=a
    a=empty(1.32);mix(a,dragon(.1,.98,.90),.005,.6)
    for i,at in enumerate((.02,.35,.66,.94)):mix(a,c[f'Footstep_0{1+i%2}'],at,.44)
    mix(a,gust(1.25,59),.03,.4);c['Charge']=a
    a=empty(1.4)
    # Wrap the previous footfall's tail so the cadence does not dip at the seam.
    for i,at in enumerate((0,.35,.7,1.05)):mix(a,c[f'Footstep_0{1+i%2}'],at,.70)
    spill=c['Footstep_02'][round(.35*RATE):];mix(a,spill,0,.70)
    c['ChargeLoop']=a
    a=empty(1.18);mix(a,rock(2.40,3.41,.95,160,6200),.004,.9);mix(a,weight(.8,63),.05,.6);a*=np.exp(-np.arange(len(a))/RATE/1.0);c['Brake']=a
    for name,a in c.items():
        level=.075 if name.startswith('Footstep') or name=='GroundFireLoop' else .087 if name.endswith('Prepare') or name in ('Inhale','WingFlap','WingLoop') else .115
        c[name]=finish(a,level,name in LOOPS)
    assert set(c)==set(LABELS)
    return c


def save(path,a,loop=False):
    assert np.isfinite(a).all() and .01<abs(a).max()<.8
    pcm=np.round(a*32767).astype('<i2');buf=io.BytesIO()
    with wave.open(buf,'wb') as w:
        w.setnchannels(1);w.setsampwidth(2);w.setframerate(RATE);w.writeframes(pcm.tobytes())
    data=buf.getvalue()
    if not path.exists() or path.read_bytes()!=data:path.write_bytes(data)
    if path.is_relative_to(ROOT/'Assets'):meta(path,'audio')
    return dict(file=path.relative_to(ROOT).as_posix(),seconds=round(len(a)/RATE,3),loop=loop,
                sha256=hashlib.sha256(data).hexdigest(),peak_dbfs=round(float(20*np.log10(abs(a).max())),2),
                rms_dbfs=round(float(20*np.log10(np.sqrt(np.mean(a*a)))),2))


def preview(c,items,length):
    a=empty(length)
    for name,at,gain in items:mix(a,fade(c[name],.055,.12) if name in LOOPS else c[name],at,gain)
    return fade(a*min(1,.70/max(abs(a).max(),1e-7)))


def player(path,loop=False):
    data=base64.b64encode(path.read_bytes()).decode('ascii')
    return f'<audio controls preload="none" {"loop" if loop else ""} src="data:audio/wav;base64,{data}"></audio>'


def main():
    for folder in (SOURCE,DEST,ROOT/'Assets/Art/Source/Audio',ROOT/'Assets/Art/Source/Audio/SoulEater'):
        folder.mkdir(parents=True,exist_ok=True);meta(folder,'folder')
    OUT.mkdir(parents=True,exist_ok=True)
    manifest=json.loads(DOC.read_text(encoding='utf-8'))
    for entry in manifest['sources']:
        for item in [entry]+entry.get('extracted',[]):
            p=ROOT/item['file'];assert hashlib.sha256(p.read_bytes()).hexdigest()==item['sha256'],p.name
            meta(p,'audio' if p.suffix!='.zip' else 'default')
    c=design();report=[save(DEST/f'Dragon_{name}.wav',a,name in LOOPS) for name,a in c.items()]
    reels={
        'Voice_A': ('Voz A · Grave y animal',[('Roar_A',.15,1),('RoarShort_A',3.0,1),('Hit_A',5.0,1),('Death_A',6.15,1)],10.3),
        'Voice_B': ('Voz B · Áspera y reptiliana',[('Roar_B',.15,1),('RoarShort_B',3.0,1),('Hit_B',5.0,1),('Death_B',6.15,1)],10.3),
        'Fire': ('Aliento de fuego',[('Inhale',.1,1),('FlameStart',1.6,.9),('FlameLoop',1.6,.8),('FlameEnd',3.8,.85),('GroundFireLoop',5.0,.75)],8.15),
        'Flight': ('Despegue, vuelo y caída',[('Jump',.1,1),('WingLoop',1.2,.8),('Dive',3.5,1),('DiveImpact',4.12,1)],6.8),
        'Ground': ('Pisadas y aterrizaje',[('Footstep_01',.15,1),('Footstep_02',1.1,1),('Land',2.25,1)],4.1),
        'Melee': ('Mordida y coletazo',[('Bite_Prepare',.1,1),('Bite',1.05,1),('Tail_Prepare',2.1,1),('Tail',3.2,1)],4.3),
        'Charge': ('Carga y frenada',[('Charge_Prepare',.1,1),('Charge',1.1,1),('Brake',2.46,1)],4.0),
    }
    for key,(_,items,length) in reels.items():report.append(save(OUT/f'Dragon_{key}_Preview.wav',preview(c,items,length)))
    page='''<!doctype html><html lang="es"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Soul Eater · Nuevos sonidos</title><style>*{box-sizing:border-box}body{margin:0;background:#131917;color:#edf3ed;font:16px system-ui,sans-serif}
main{max-width:1100px;margin:auto;padding:35px 22px}h1{font-size:37px;margin:10px 0}h2{font-size:23px;margin:30px 0 16px}h3{font-size:18px;margin:0 0 10px}
p{color:#b1bfb4;line-height:1.6}small,a,summary{color:#b5d18b}.grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:16px}
article{padding:20px;background:#202a22;border:1px solid #40563d;border-radius:14px}audio{width:100%}article p{font-size:14px;margin:0 0 16px}
.tag{display:inline-block;font-size:12px;padding:5px 9px;background:#334630;border-radius:20px;margin-bottom:12px}details{margin:25px 0}
summary{cursor:pointer;font-weight:600;padding:12px 0}.individual{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:12px}.individual article{padding:14px}
.individual h3{font-size:14px}.individual small{display:block;margin-bottom:10px;font-size:12px}footer{font-size:12px;line-height:1.7;color:#9eafa1;margin:32px 0 12px}
@media(max-width:740px){.grid,.individual{grid-template-columns:1fr}h1{font-size:30px}}</style><main>
<small>MISMO · BOSS DRAGÓN / SOUL EATER</small><h1>Una nueva voz para el dragón</h1>
<p>Dos voces para comparar y un conjunto nuevo de tierra, fuego y alas. Cada tarjeta reproduce una secuencia; más abajo podés escuchar los gestos por separado.</p>
<h2>Elegir la voz</h2><div class="grid">'''
    for key in ('Voice_A','Voice_B'):
        title,_,_=reels[key]
        page+=f'<article><span class="tag">VOZ {key[-1]}</span><h3>{title}</h3><p>Rugido largo → rugido corto → hit → muerte.</p>'+player(OUT/f'Dragon_{key}_Preview.wav')+'</article>'
    page+='</div><h2>Fuego, vuelo y cuerpo</h2><div class="grid">'
    descriptions={'Fire':'Inhalación → fuego sostenido → apagado → brasas sobre el suelo.',
        'Flight':'Despegue → aleteos → descenso → golpe contra el suelo.',
        'Ground':'Dos pisadas → aterrizaje pesado.',
        'Melee':'Preparación y mordida → preparación y coletazo.',
        'Charge':'Preparación → carrera → frenada con arrastre.'}
    for key in descriptions:
        page+=f'<article><h3>{reels[key][0]}</h3><p>{descriptions[key]}</p>'+player(OUT/f'Dragon_{key}_Preview.wav')+'</article>'
    page+=f'</div><details><summary>Escuchar los {len(c)} sonidos individuales</summary><div class="individual">'
    for name,label in LABELS.items():
        page+=f'<article id="{name}"><h3>{label}</h3><small>{len(c[name])/RATE:.2f} s'+(' · Se repite hasta pausar' if name in LOOPS else '')+'</small>'+player(DEST/f'Dragon_{name}.wav',name in LOOPS)+'</article>'
    page+='''</div></details><p>Propuesta para escuchar y ajustar antes de reemplazar los sonidos del juego.</p>
<footer>Fuentes y créditos: <a href="https://freesound.org/people/JoelAudio/sounds/85568/">JoelAudio · DRAGON_ROAR</a>,
<a href="https://freesound.org/people/999999990/sounds/320345/">999999990 · Dino Hiss Dragon Roar</a>,
<a href="https://opengameart.org/content/bear-growls">AntumDeluge / U.S. Fish &amp; Wildlife Service · Bear Growls</a>,
<a href="https://opengameart.org/content/large-wings-flap">AntumDeluge / dave.des · Large Wings Flap</a> (CC0);
<a href="https://opengameart.org/content/fire-loop">qubodup · Fire Loop</a> y
<a href="https://opengameart.org/content/falling-rock">spookymodem · Falling Rock</a> (<a href="https://creativecommons.org/licenses/by/3.0/">CC BY 3.0</a>).
Edición, capas y aire procedural para Mismo. Las voces de Freesound usan sus previews MP3 públicos; las otras fuentes provienen de WAV/FLAC originales.</footer></main>
<script>document.querySelectorAll('audio').forEach(a=>a.addEventListener('play',()=>document.querySelectorAll('audio').forEach(b=>{if(a!==b)b.pause()})))</script></html>'''
    (OUT/'escuchar.html').write_text(page,encoding='utf-8')
    (OUT/'audio-analysis.json').write_text(json.dumps(dict(rate=RATE,channels=1,bits=16,clips=report,labels=LABELS),indent=2),encoding='utf-8')
    meta(DOC);meta(DOC.with_name('DragonSoundConcepts.txt'),'text');meta(Path(__file__).resolve())
    print(f'Generated {len(c)} sounds, {len(reels)} previews and output/dragon-audio/escuchar.html. Runtime untouched.')


if __name__=='__main__':main()
