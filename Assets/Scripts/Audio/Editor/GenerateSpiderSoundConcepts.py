"""Spider combat auditions from credited CC BY recordings; requires NumPy.

Does not assign game settings. python Assets/Scripts/Audio/Editor/GenerateSpiderSoundConcepts.py
"""
from pathlib import Path
import base64
import hashlib
import io
import json
import uuid
import wave

import numpy as np

ROOT = Path(__file__).resolve().parents[4]
BASE = ROOT / 'Assets/Art/Audio/Enemies/Spider'
SOURCE = BASE / 'Source'
DEST = BASE / 'Concept'
OUT = ROOT / 'output/spider-audio'
RATE = 48000
PROFILES = {
    'Dry': dict(label='A · Chasquidos', description='Patas y mandíbulas, textura seca y siseo discreto.', speed=1.04, hiss=.14, hurt=.12),
    'Hissing': dict(label='B · Siseo · elegida', description='Más respiración áspera y una reacción más aguda. Seleccionada para la araña.', speed=1.10, hiss=.70, hurt=.40),
}
ROLES = {'Bite_Prepare': 'Preparación · mordida', 'Bite_Execute': 'Mordida',
         'Jump_Prepare': 'Preparación · salto', 'Jump_Execute': 'Salto', 'Hit': 'Hit recibido'}
SOURCES = [
    dict(file='Spider_Chattering.wav', title='Spider Chattering', author='spookymodem',
         page='https://opengameart.org/content/spider-chattering',
         download='https://opengameart.org/sites/default/files/Spider%20Chattering.wav',
         sha256='9f147735ab8b120df1cbe0ea3bc6f8701c5b1e14c05c1c016010768c6c7a82a7'),
    dict(file='Minimare_Hiss.wav', title='A Lonely Nightmare - Minimare (Monster) SFX', author='WakianTech',
         page='https://opengameart.org/content/a-lonely-nightmare-minimare-monster-sfx',
         download='https://opengameart.org/sites/default/files/Minimare_Hiss.wav',
         sha256='76789c0e0803c78338949edf59ab182f0d15d667cdff8d1dc3329d4a767c0b44'),
    dict(file='Minimare_Hurt.wav', title='A Lonely Nightmare - Minimare (Monster) SFX', author='WakianTech',
         page='https://opengameart.org/content/a-lonely-nightmare-minimare-monster-sfx',
         download='https://opengameart.org/sites/default/files/Minimare_Hurt.wav',
         sha256='ea40ded7bb21d8edd071be412b496bbd0578a8f0618e2d4a6f947dbfb5e596b7'),
]


def meta(path, kind='default'):
    target = Path(str(path)+'.meta')
    if target.exists():
        return
    guid = uuid.uuid5(uuid.NAMESPACE_URL, 'mismo-spider-audio/'+path.relative_to(ROOT).as_posix()).hex
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
    target.write_text(f'fileFormatVersion: 2\nguid: {guid}\n'+body+
                      '  userData:\n  assetBundleName:\n  assetBundleVariant:\n', encoding='utf-8')


def band(signal, low=250, high=7000):
    padded = np.pad(signal, (2048, 2048))
    f = np.fft.rfftfreq(len(padded), 1/RATE)
    shape = (1-np.exp(-(f/low)**4))*np.exp(-(f/high)**4)
    return np.fft.irfft(np.fft.rfft(padded)*shape, len(padded))[2048:-2048]


def fade(signal, attack=.007, release=.04):
    signal = signal.copy()
    n, m = min(round(attack*RATE), len(signal)//2), min(round(release*RATE), len(signal)//2)
    signal[:n] *= np.sin(np.linspace(0, np.pi/2, n))**2
    signal[-m:] *= np.cos(np.linspace(0, np.pi/2, m))**2
    signal[0] = signal[-1] = 0
    return signal


def take(name, start, end, speed=1, low=250, high=7000):
    with wave.open(str(SOURCE/name), 'rb') as stream:
        assert stream.getsampwidth() == 2
        rate = stream.getframerate()
        a = np.frombuffer(stream.readframes(stream.getnframes()), '<i2')
        a = a.reshape(-1, stream.getnchannels()).mean(axis=1)/32768
    a = a[round(start*rate):round(end*rate)]
    a = np.interp(np.arange(0, len(a)-1, speed*rate/RATE), np.arange(len(a)), a)
    a = fade(band(a-a.mean(), low, high))
    a *= min(.12/max(np.sqrt(np.mean(a*a)), 1e-7), .65/max(np.max(np.abs(a)), 1e-7))
    return a


def mix(out, clip, at=0, gain=1):
    offset = round(at*RATE)
    n = min(len(clip), len(out)-offset)
    out[offset:offset+n] += gain*clip[:n]


def design(profile):
    p = PROFILES[profile]
    speed = p['speed']
    chatter = lambda a,b,ratio=1: take('Spider_Chattering.wav', a,b,speed*ratio,300,7000)
    hiss = lambda a,b: take('Minimare_Hiss.wav', a,b,speed*1.3,600,7400)
    clips = {}
    a = np.zeros(round(.34*RATE))
    mix(a, chatter(.63,.91), gain=.88)
    mix(a, hiss(.08,.48), .018, p['hiss']*.75)
    clips['Bite_Prepare'] = a
    a = np.zeros(round(.30*RATE))
    mix(a, chatter(2.08,2.26,1.05), gain=1)
    mix(a, chatter(2.73,2.90,1.14), .07, .64)
    mix(a, hiss(.17,.52), .006, p['hiss'])
    clips['Bite_Execute'] = a
    a = np.zeros(round(.50*RATE))
    mix(a, chatter(1.06,1.48), gain=.8)
    mix(a, hiss(.05,.68), .014, p['hiss']*.8)
    clips['Jump_Prepare'] = a
    a = np.zeros(round(.43*RATE))
    mix(a, chatter(2.93,3.31,1.16), gain=.8)
    mix(a, hiss(.14,.60), .012, p['hiss']*.72)
    t = np.arange(len(a))/RATE
    rng = np.random.default_rng(740203)
    gust = band(rng.normal(size=len(a)), 900, 5800)
    gust /= max(np.std(gust), 1e-7)
    a += .022*gust*(1-np.exp(-t/.028))*np.exp(-t/.09)
    clips['Jump_Execute'] = a
    a = np.zeros(round(.31*RATE))
    mix(a, chatter(3.56,3.90,1.35), gain=.94)
    mix(a, take('Minimare_Hurt.wav', .01,.42,1.65*speed,850,7400), .004, p['hurt'])
    clips['Hit'] = a
    for name, a in clips.items():
        a = fade(a-a.mean(), release=.035)
        target = .071 if name.endswith('Prepare') else .105
        a *= min(target/max(np.sqrt(np.mean(a*a)),1e-7), .63/max(np.max(np.abs(a)),1e-7))
        clips[name] = a
    return clips


def write(path, a):
    assert np.all(np.isfinite(a)) and .01 < np.max(np.abs(a)) < .8
    a = np.round(a*32767).astype('<i2')
    assert a[0] == a[-1] == 0
    buffer = io.BytesIO()
    with wave.open(buffer,'wb') as stream:
        stream.setnchannels(1); stream.setsampwidth(2); stream.setframerate(RATE); stream.writeframes(a.tobytes())
    data = buffer.getvalue()
    if not path.exists() or path.read_bytes() != data:
        path.write_bytes(data)
    meta(path,'audio')
    return dict(file=path.relative_to(ROOT).as_posix(), seconds=round(len(a)/RATE,3),
                sha256=hashlib.sha256(data).hexdigest(), peak_dbfs=round(float(20*np.log10(np.max(np.abs(a.astype(float)))/32768)),2))


def player(path):
    return '<audio controls preload="none" src="data:audio/wav;base64,'+base64.b64encode(path.read_bytes()).decode('ascii')+'"></audio>'


def main():
    for directory in (BASE, SOURCE, DEST):
        directory.mkdir(parents=True,exist_ok=True); meta(directory,'folder')
    OUT.mkdir(parents=True,exist_ok=True)
    for entry in SOURCES:
        path = SOURCE/entry['file']
        assert hashlib.sha256(path.read_bytes()).hexdigest() == entry['sha256'], path.name
        meta(path,'audio')
    report = []
    for profile in PROFILES:
        clips = design(profile)
        for name, a in clips.items():
            report.append(write(DEST/f'Spider_{profile}_{name}.wav', a))
        # Bite preparation -> bite at its 0.48 s windup; pause; jump at 0.60 s; hit.
        reel = np.zeros(round(4.1*RATE))
        for name, at in [('Bite_Prepare',.15),('Bite_Execute',.63),('Jump_Prepare',1.55),('Jump_Execute',2.15),('Hit',3.35)]:
            mix(reel,clips[name],at)
        report.append(write(DEST/f'Spider_{profile}_Audition.wav',reel))
    page = '''<!doctype html><html lang="es"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Araña · Pruebas de sonido</title><style>*{box-sizing:border-box}body{margin:0;background:#161b18;color:#edf0e8;font:16px system-ui,sans-serif}
main{max-width:1040px;margin:auto;padding:36px 22px}small,a{color:#accb84}h1{font-size:36px;margin:12px 0}p{color:#b8c3b5;line-height:1.55}
.grid{display:grid;grid-template-columns:1fr 1fr;gap:18px}article{border:1px solid #40523a;border-radius:14px;padding:22px;background:#20291f}
h2{font-size:23px}h3{font-size:16px;margin:25px 0 8px}audio{width:100%}footer{font-size:13px;line-height:1.7;margin-top:28px}
details{margin-top:24px}summary{cursor:pointer;color:#c7d9b3}.phase{border-top:1px solid #40523a;margin-top:16px}.reference{margin:24px 0;padding:16px 20px;border-left:3px solid #9fbf77}
@media(max-width:700px){.grid{grid-template-columns:1fr}}</style><main><small>MISMO · ARAÑA</small><h1>Chasquidos y siseos</h1>
<p>La versión B es la elegida para la araña. En cada secuencia: preparación y mordida → preparación y salto → hit recibido.</p>
<div class="grid">'''
    for profile,p in PROFILES.items():
        page += f'<article><h2>{p["label"]}</h2><p>{p["description"]}</p>'+player(DEST/f'Spider_{profile}_Audition.wav')
        page += '<details><summary>Escuchar cada gesto</summary>'
        for role,label in ROLES.items():
            page += f'<div class="phase"><h3>{label}</h3>'+player(DEST/f'Spider_{profile}_{role}.wav')+'</div>'
        page += '</details></article>'
    page += '''</div><div class="reference">Referencia comercial para comparar:
<a href="https://soundcloud.com/magic-sound-effects/spider-vocalizations-short-preview" target="_blank" rel="noopener">Spider Vocalizations · Magic Sound Effects</a>.
La referencia no forma parte de estas muestras.</div>
<p>La versión B está asignada a la mordida, el salto y el hit de las cuatro variantes de araña. La telaraña sigue desactivada. A queda disponible para comparar.</p>
<footer>Grabaciones: <a href="https://opengameart.org/content/spider-chattering">Spider Chattering, spookymodem</a> y
<a href="https://opengameart.org/content/a-lonely-nightmare-minimare-monster-sfx">Minimare, WakianTech</a>, del proyecto
<a href="https://opengameart.org/content/a-lonely-nightmare-cancelled-game">A Lonely Nightmare</a>.
Licencia <a href="https://creativecommons.org/licenses/by/3.0/">CC BY 3.0</a>. Recorte, cambios de tono y velocidad, filtros y mezcla para Mismo; soplo de salto procedural.</footer></main>
<script>document.querySelectorAll('audio').forEach(a=>a.addEventListener('play',()=>document.querySelectorAll('audio').forEach(b=>{if(a!==b)b.pause()})))</script></html>'''
    (OUT/'escuchar.html').write_text(page,encoding='utf-8')
    (OUT/'audio-analysis.json').write_text(json.dumps(dict(clips=report,rate=RATE,channels=1,bits=16,profiles=PROFILES),indent=2),encoding='utf-8')
    doc = ROOT/'Assets/Documentation/Audio/SpiderSoundConcepts-source.json'
    doc.write_text(json.dumps(dict(retrieved='2026-10-07',license='CC-BY-3.0',
        license_url='https://creativecommons.org/licenses/by/3.0/',
        project_credit='https://opengameart.org/content/a-lonely-nightmare-cancelled-game',sources=SOURCES),indent=2),encoding='utf-8')
    meta(doc)
    meta(Path(__file__).resolve())
    meta(doc.with_name('SpiderSoundConcepts.txt'),'text')
    print(json.dumps(report,indent=2))


if __name__ == '__main__':
    main()
