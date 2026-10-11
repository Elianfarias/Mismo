"""Boar combat auditions; requires NumPy and SoundFile (libsndfile).

python Assets/Scripts/Audio/Editor/GenerateBoarSoundConcepts.py
Regenerates auditions and the approved B clips; does not assign CreatureSettings.
"""
from pathlib import Path
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
    sys.path.insert(0, str(ROOT / '.validation/audio-python'))
    import soundfile as sf

BASE = ROOT / 'Assets/Art/Audio/Enemies/Boar'
SOURCE = BASE / 'Source'
DEST = BASE / 'Concept'
OUT = ROOT / 'output/boar-audio'
RATE = 48000
PROFILES = {
    'Woodland': dict(label='A · Gruñido seco', description='Gruñidos cortos, resoplido de aviso y un chillido breve al recibir daño.', speed=1.0, voice=.30, breath=.18, hit='Pig_Hit1.mp3'),
    'Bristled': dict(label='B · Resoplido furioso · elegida', description='Más cuerpo y aspereza. Hit revisado: un gruñido nasal corto a partir de un cerdo real.', speed=.90, voice=.85, breath=.44, hit='Pig_Grunt_CC0.mp3'),
}
ROLES = {'Attack_Prepare': 'Preparación · ataque', 'Attack_Execute': 'Ataque',
         'Charge_Prepare': 'Preparación · carga', 'Charge_Execute': 'Arranque de la carga', 'Hit': 'Hit recibido'}
SOURCES = [
    dict(file='Boar_Vocal.ogg', title='Boar', author='fvcalderan', license='CC0-1.0',
         page='https://opengameart.org/content/boar-0',
         download='https://opengameart.org/sites/default/files/boar_0.ogg',
         recording='Human voice performance with EQ; original OGG.',
         sha256='14b874678ef92e52168e1b96da4aee08e3c7e259d06337dd098e1eacaa25b370'),
    dict(file='Pig_Grunt_CC0.mp3', title='Pig.ogg', author='egomassive; original recording felix.blume', license='CC0-1.0',
         page='https://freesound.org/people/egomassive/sounds/536746/',
         original_page='https://freesound.org/people/felix.blume/sounds/158746/',
         download='https://cdn.freesound.org/previews/536/536746_1415754-hq.mp3',
         recording='Real pig; public HQ MP3 preview of the CC0 sound, not the original OGG. Decoded rate is 24 kHz.',
         sha256='3d13ee49765e6d359f360b6e699b3466836e16ec8ff5cff71478c7126a008a89'),
    dict(file='Pig_Idle2.mp3', title='Pig SFX Pack / pig_idle2', author='Vinrax', license='CC-BY-3.0',
         page='https://opengameart.org/content/pig-sfx-pack', author_page='https://opengameart.org/users/vinrax',
         download='https://opengameart.org/sites/default/files/pig_idle2.mp3',
         recording='Rubber pig toy recording, speed processed by author; original MP3.',
         sha256='9844a9d25b671354287b12d7da18b2f0fca80f1d9535ee6892442a5f7974516f'),
    dict(file='Pig_Hit1.mp3', title='Pig SFX Pack / pig_hit1', author='Vinrax', license='CC-BY-3.0',
         page='https://opengameart.org/content/pig-sfx-pack', author_page='https://opengameart.org/users/vinrax',
         download='https://opengameart.org/sites/default/files/pig_hit1.mp3',
         recording='Rubber pig toy recording, speed processed by author; original MP3.',
         sha256='946d396a0cdfdd57448363d7d3053f9ae9270b6cac723743cefe4ad1e712f671'),
    dict(file='Pig_Hit2.mp3', title='Pig SFX Pack / pig_hit2', author='Vinrax', license='CC-BY-3.0',
         page='https://opengameart.org/content/pig-sfx-pack', author_page='https://opengameart.org/users/vinrax',
         download='https://opengameart.org/sites/default/files/pig_hit2.mp3',
         recording='Rubber pig toy recording, speed processed by author; original MP3.',
         sha256='bf6eea8b67053fbe2a0a62ed75fff5e59d91c931eb881d5e4a90c806e5f55ca8'),
]


def meta(path, kind='default'):
    target = Path(str(path)+'.meta')
    if target.exists():
        return
    guid = uuid.uuid5(uuid.NAMESPACE_URL, 'mismo-boar-audio/'+path.relative_to(ROOT).as_posix()).hex
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


def band(signal, low=95, high=6500):
    padded = np.pad(signal, (2048, 2048))
    f = np.fft.rfftfreq(len(padded), 1/RATE)
    shape = (1-np.exp(-(f/low)**4))*np.exp(-(f/high)**4)
    return np.fft.irfft(np.fft.rfft(padded)*shape, len(padded))[2048:-2048]


def fade(signal, attack=.006, release=.025):
    signal = signal.copy()
    n, m = min(round(attack*RATE), len(signal)//2), min(round(release*RATE), len(signal)//2)
    signal[:n] *= np.sin(np.linspace(0, np.pi/2, n))**2
    signal[-m:] *= np.cos(np.linspace(0, np.pi/2, m))**2
    signal[0] = signal[-1] = 0
    return signal


def take(name, start, end, speed=1, low=95, high=6500):
    a, rate = sf.read(SOURCE/name, always_2d=True)
    a = a.mean(axis=1)[round(start*rate):round(end*rate)]
    assert len(a) > 100 and speed > 0
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
    grunt = lambda a,b,ratio=1: take('Pig_Grunt_CC0.mp3', a,b,speed*ratio)
    vocal = lambda a,b,ratio=1: take('Boar_Vocal.ogg', a,b,speed*ratio,115,5700)
    breath = lambda: take('Pig_Idle2.mp3', .0,.20,speed,240,7200)
    clips = {}
    a = np.zeros(round(.40*RATE))
    mix(a, grunt(.095,.30), .012, .90)
    mix(a, vocal(.03,.18), .045, p['voice']*.50)
    mix(a, breath(), .10, p['breath']*.55)
    clips['Attack_Prepare'] = a
    a = np.zeros(round(.32*RATE))
    mix(a, grunt(.55,.775,1.08), .003, .94)
    mix(a, vocal(.19,.36,1.05), .006, p['voice'])
    mix(a, breath(), .023, p['breath'])
    clips['Attack_Execute'] = a
    a = np.zeros(round(.62*RATE))
    mix(a, grunt(.10,.30,.98), .008, .64)
    mix(a, grunt(.93,1.19), .29, .95)
    mix(a, vocal(.03,.18), .30, p['voice']*.72)
    mix(a, breath(), .365, p['breath']*.64)
    clips['Charge_Prepare'] = a
    a = np.zeros(round(.48*RATE))
    mix(a, vocal(.012,.39,.96), .003, .72+p['voice']*.40)
    mix(a, grunt(.55,.80,.97), .018, .66)
    mix(a, breath(), .025, p['breath']*.66)
    clips['Charge_Execute'] = a
    if profile == 'Bristled':
        # A single real pig grunt: exclude the toy squeal and performed voice.
        # Trim its soft lead-in and shorten it for an immediate hurt reaction.
        a = np.zeros(round(.29*RATE))
        mix(a, take(p['hit'], .965,1.255,1.18,150,7100), .003, 1)
    else:
        a = np.zeros(round(.44*RATE))
        mix(a, take(p['hit'], .145,.50, speed*1.03,180,7000), .004, 1)
        mix(a, vocal(.19,.35,1.12), .04, p['voice']*.35)
    clips['Hit'] = a
    for name, a in clips.items():
        a = fade(a-a.mean(), release=.032)
        target = .073 if name.endswith('Prepare') else .108
        a *= min(target/max(np.sqrt(np.mean(a*a)),1e-7), .63/max(np.max(np.abs(a)),1e-7))
        clips[name] = a
    return clips


def write(path, a):
    assert np.all(np.isfinite(a)) and .01 < np.max(np.abs(a)) < .8
    pcm = np.round(a*32767).astype('<i2')
    assert pcm[0] == pcm[-1] == 0
    buffer = io.BytesIO()
    with wave.open(buffer,'wb') as stream:
        stream.setnchannels(1); stream.setsampwidth(2); stream.setframerate(RATE); stream.writeframes(pcm.tobytes())
    data = buffer.getvalue()
    if not path.exists() or path.read_bytes() != data:
        path.write_bytes(data)
    meta(path,'audio')
    return dict(file=path.relative_to(ROOT).as_posix(), seconds=round(len(a)/RATE,3),
                sha256=hashlib.sha256(data).hexdigest(),
                peak_dbfs=round(float(20*np.log10(np.max(np.abs(pcm.astype(float)))/32768)),2),
                rms_dbfs=round(float(20*np.log10(np.sqrt(np.mean((pcm.astype(float)/32768)**2)))),2))


def player(path):
    return '<audio controls preload="metadata" src="data:audio/wav;base64,'+base64.b64encode(path.read_bytes()).decode('ascii')+'"></audio>'


def main():
    settings = ROOT/'Assets/Data/Enemies/ForestCreatures/Boar.asset'
    settings_before = settings.read_bytes()
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
            report.append(write(DEST/f'Boar_{profile}_{name}.wav', a))
        # Match the live attack windups: 0.55 s melee and 0.72 s charge.
        reel = np.zeros(round(4.4*RATE))
        for name, at in [('Attack_Prepare',.15),('Attack_Execute',.70),('Charge_Prepare',1.65),('Charge_Execute',2.37),('Hit',3.55)]:
            mix(reel,clips[name],at)
        report.append(write(DEST/f'Boar_{profile}_Audition.wav',reel))
    page = '''<!doctype html><html lang="es"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Jabalí · Pruebas de sonido</title><style>*{box-sizing:border-box}body{margin:0;background:#191815;color:#f3eee4;font:16px system-ui,sans-serif}
main{max-width:1040px;margin:auto;padding:36px 22px}small,a{color:#d4b481}h1{font-size:36px;margin:12px 0}p{color:#c8bead;line-height:1.55}
.grid{display:grid;grid-template-columns:1fr 1fr;gap:18px}article{border:1px solid #62503b;border-radius:14px;padding:22px;background:#28231d}
h2{font-size:23px}h3{font-size:16px;margin:25px 0 8px}audio{width:100%}footer{font-size:13px;line-height:1.7;margin-top:28px}
details{margin-top:24px}summary{cursor:pointer;color:#e1c79e}.phase{border-top:1px solid #62503b;margin-top:16px}
.order{padding:14px 18px;background:#211e19;border-radius:10px;margin:20px 0 24px}
@media(max-width:700px){.grid{grid-template-columns:1fr}}</style><main><small>MISMO · JABALÍ</small><h1>Gruñidos y resoplidos</h1>
<p>La B es la elegida. El hit ahora es un gruñido nasal corto de cerdo; podés escucharlo solo o dentro de la secuencia completa.</p>
<div class="order">En cada secuencia: preparación y ataque → preparación y carga → hit recibido.</div><div class="grid">'''
    for profile,p in PROFILES.items():
        page += f'<article><h2>{p["label"]}</h2><p>{p["description"]}</p>'+player(DEST/f'Boar_{profile}_Audition.wav')
        page += ('<details open>' if profile == 'Bristled' else '<details>')+'<summary>Escuchar cada gesto</summary>'
        for role,label in ROLES.items():
            page += f'<div class="phase" id="{profile}_{role}"><h3>{label}</h3>'+player(DEST/f'Boar_{profile}_{role}.wav')+'</div>'
        page += '</details></article>'
    page += '''</div><p>La B con el hit revisado está integrada en las tres variantes del jabalí: preparación y ataque, preparación y carga, y reacción al recibir daño.</p>
<footer>Fuentes: <a href="https://freesound.org/people/egomassive/sounds/536746/">Pig, egomassive / grabación original de felix.blume</a> y
<a href="https://opengameart.org/content/boar-0">Boar, fvcalderan</a> (<a href="https://creativecommons.org/publicdomain/zero/1.0/">CC0</a>);
<a href="https://opengameart.org/content/pig-sfx-pack">Pig SFX Pack, Vinrax</a> (<a href="https://creativecommons.org/licenses/by/3.0/">CC BY 3.0</a>).
Edición para Mismo: recorte, tono y velocidad, filtros, envolventes y mezcla. La muestra de egomassive proviene de su preview MP3 público; las demás son los archivos publicados por sus autores.</footer></main>
<script>document.querySelectorAll('audio').forEach(a=>a.addEventListener('play',()=>document.querySelectorAll('audio').forEach(b=>{if(a!==b)b.pause()})))</script></html>'''
    (OUT/'escuchar.html').write_text(page,encoding='utf-8')
    (OUT/'audio-analysis.json').write_text(json.dumps(dict(clips=report,rate=RATE,channels=1,bits=16,profiles=PROFILES,
        settings_sha256=hashlib.sha256(settings_before).hexdigest()),indent=2),encoding='utf-8')
    doc = ROOT/'Assets/Documentation/Audio/BoarSoundConcepts-source.json'
    doc.write_text(json.dumps(dict(retrieved='2026-10-08',sources=SOURCES,
        modifications='Cuts, pitch/speed, equalization, fades, level matching, layered mixes.',
        revision='B preferred; B hurt replaced by a single real pig grunt (0.965-1.255 s at 1.18x) with no performed voice or toy layers. Other combat clips unchanged.',
        derived_mixes_license='CC-BY-3.0',
        license_url='https://creativecommons.org/licenses/by/3.0/',
        note='Original CC0 sources retain CC0. Attribution applies to the Vinrax recordings and derived audition mixes.'),indent=2),encoding='utf-8')
    meta(doc); meta(Path(__file__).resolve()); meta(doc.with_name('BoarSoundConcepts.txt'),'text')
    assert settings.read_bytes() == settings_before
    print(f'Created {len(report)} WAV files and output/boar-audio/escuchar.html; game settings unchanged.')


if __name__ == '__main__':
    main()
