"""Prepare the selected Creature Sounds pack and Daniel Simon fire for Unity.

Only silence trimming, gain, fades, channel conversion and a fire loop splice.
No synthetic voices, pitch changes or added effects.
"""
from pathlib import Path
import base64
import hashlib
import io
import json
import sys
import uuid
import zipfile

import numpy as np

ROOT = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(ROOT / '.validation/audio-python'))
import soundfile as sf

SOURCE = ROOT / 'Assets/Art/Audio/Enemies/SoulEater/Source/FreeMonsterSounds'
DEST = ROOT / 'Assets/Art/Audio/Enemies/SoulEater/Selected'
DOCS = ROOT / 'Assets/Documentation/Audio'
OUT = ROOT / 'output/dragon-audio'
ARCHIVE = ROOT / 'Assets/Art/Source/Audio/SoulEater/ClassicMonsterSounds_CreatureSounds.zip'
RATE = 48000


def meta(path, folder=False):
    file = path.with_name(path.name + '.meta')
    if file.exists():
        return
    header = 'fileFormatVersion: 2\nguid: ' + uuid.uuid4().hex + '\n'
    if folder:
        header += 'folderAsset: yes\n'
    if path.suffix == '.wav':
        template = (ROOT / 'Assets/Art/Audio/Enemies/SoulEater/Source/Dragon_Roar_JoelAudio_HQ.mp3.meta').read_text()
        header += template[template.index('AudioImporter:'):]
    else:
        importer = 'TextScriptImporter' if path.suffix in ('.txt', '.json') else 'DefaultImporter'
        header += importer + ':\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n'
    file.write_text(header, encoding='utf-8')


def load(path):
    x, sr = sf.read(path, always_2d=True)
    x = x.mean(axis=1)
    if sr != RATE:
        x = np.interp(np.arange(round(len(x) * RATE / sr)) * sr / RATE, np.arange(len(x)), x)
    return x


def fade(x, seconds=.008):
    x = x.copy()
    n = min(round(seconds * RATE), len(x) // 2)
    x[:n] *= np.linspace(0, 1, n)
    x[-n:] *= np.linspace(1, 0, n)
    return x


def trim(x):
    active = np.flatnonzero(np.abs(x) > max(.0006, np.max(np.abs(x)) * .01))
    if len(active):
        x = x[max(0, active[0] - 240):min(len(x), active[-1] + 2400)]
    return x


def main():
    for folder in (SOURCE, DEST, OUT):
        folder.mkdir(parents=True, exist_ok=True)
        if 'Assets' in folder.parts:
            meta(folder, True)
    records = []
    originals = {}
    members = ['Dragon/' + name + '.wav' for name in (
        'Dragon_Attack', 'Dragon_Damage', 'Dragon_Death', 'Dragon_Emerge', 'Dragon_Step')]
    members += ['Behemoth/Behemoth_Step.wav']
    with zipfile.ZipFile(ARCHIVE) as archive:
        for member in members:
            path = SOURCE / Path(member).name
            raw = archive.read('Monster Sounds/' + member)
            if path.exists() and path.read_bytes() != raw:
                raise ValueError('Refusing to replace modified original: ' + str(path))
            path.write_bytes(raw)
            meta(path)
            originals[path.stem] = load(path)
        terms = archive.read('Monster Sounds/Terms Of Use.txt').decode('utf-8-sig')
    meta(ARCHIVE)

    def save(name, x, origin, changes, peak=.72):
        x = fade(x)
        x *= peak / max(float(np.max(np.abs(x))), .00001)
        path = DEST / (name + '.wav')
        sf.write(path, x, RATE, subtype='PCM_16')
        meta(path)
        records.append(dict(name=name, file=path.relative_to(ROOT).as_posix(),
            duration=round(len(x) / RATE, 4), source=origin, changes=changes,
            peak=round(float(np.max(np.abs(x))), 4),
            sha256=hashlib.sha256(path.read_bytes()).hexdigest()))

    for original, name in [('Dragon_Attack', 'Dragon_Attack'), ('Dragon_Damage', 'Dragon_Hurt'),
                           ('Dragon_Death', 'Dragon_Death'), ('Dragon_Emerge', 'Dragon_Roar'),
                           ('Dragon_Step', 'Dragon_Wing'), ('Behemoth_Step', 'Dragon_Ground')]:
        save(name, trim(originals[original]), original + '.wav', 'Trim leading/trailing silence, short edge fades, gain, mono 48 kHz PCM16.')
    fire_path = ROOT / 'Assets/Art/Audio/Enemies/SoulEater/Source/Dragon_Fire_Breath_And_Roar_DanielSimon.wav'
    fire = load(fire_path)
    # The original onset remains intact across the 1.5 s ground-breath warning.
    save('Dragon_Fire_Prepare', fire[:int(1.5 * RATE)], fire_path.name, 'Original opening 0-1.5 s; mono, edge fade and gain.', .78)
    save('Dragon_Fire_Start', fire[int(1.5 * RATE):], fire_path.name, 'Original remainder from 1.5 s; mono, edge fade and gain.', .78)
    body = fire[int(2.7 * RATE):int(4.35 * RATE)]
    n = int(.08 * RATE)
    mix = np.linspace(0, 1, n)
    loop = np.concatenate([body[n:-n], body[-n:] * (1 - mix) + body[:n] * mix])
    # Do not fade a loop to silence; preserve continuity at the join.
    loop *= .70 / max(np.max(np.abs(loop)), .00001)
    path = DEST / 'Dragon_Fire_Loop.wav'
    sf.write(path, loop, RATE, subtype='PCM_16')
    meta(path)
    records.append(dict(name=path.stem, file=path.relative_to(ROOT).as_posix(),
        source=fire_path.name, duration=round(len(loop) / RATE, 4),
        changes='Original 2.7-4.35 s, 80 ms loop crossfade; mono and gain.',
        peak=round(float(np.max(np.abs(loop))), 4),
        sha256=hashlib.sha256(path.read_bytes()).hexdigest()))

    manifest = dict(selected='Free Monster Sounds + Dragon Fire Breath and Roar',
        previous_rejection_superseded=True, retrieved='2026-10-08',
        archive_sha256=hashlib.sha256(ARCHIVE.read_bytes()).hexdigest(),
        sources=[dict(title='Free Monster Sounds', author='Creature Sounds / Coucassi',
            page='https://coucassi.itch.io/free-monster-sounds', license_on_page='CC-BY-4.0',
            license_url='https://creativecommons.org/licenses/by/4.0/', included_terms=terms),
            dict(title='Dragon Fire Breath and Roar', author='Daniel Simon',
            page='https://soundbible.com/2127-Dragon-Fire-Breath-and-Roar.html', license='CC-BY-3.0',
            license_url='https://creativecommons.org/licenses/by/3.0/')], clips=records)
    manifest_path = DOCS / 'DragonSoundSelection-source.json'
    manifest_path.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
    meta(manifest_path)
    credit = DOCS / 'DragonSoundSelection.txt'
    credit.write_text('SOUL EATER - SELECTED AUDIO\n\n'
        'Free Monster Sounds by Creature Sounds / Coucassi\nhttps://coucassi.itch.io/free-monster-sounds\n'
        'Page license: CC BY 4.0 - https://creativecommons.org/licenses/by/4.0/\n'
        'Changes: silence trimming, gain, short edge fades and format conversion.\n'
        'Dragon voices and wing movement; Behemoth step used for ground impacts/footsteps.\n\n'
        'Included Terms Of Use (preserved verbatim):\n' + terms + '\n\n'
        'Dragon Fire Breath and Roar by Daniel Simon / SoundBible.com\n'
        'https://soundbible.com/2127-Dragon-Fire-Breath-and-Roar.html\n'
        'CC BY 3.0 - https://creativecommons.org/licenses/by/3.0/\n'
        'Changes: split at 1.5 s for preparation and release; loop splice for extended fire, gain and format conversion.\n\n'
        'Selection updated 2026-10-08: Free Monster Sounds was subsequently approved for in-game trial. '
        'It supersedes the earlier rejection of that pack. The custom dragon concepts remain unused.\n', encoding='utf-8')
    meta(credit)
    labels = ['Ataque / carga', 'Daño recibido', 'Muerte', 'Rugido / aparición', 'Aleteo / despegue',
              'Pasos / aterrizaje / impacto', 'Preparación del fuego', 'Salida del fuego', 'Fuego sostenido']
    cards = []
    for i, rec in enumerate(records):
        audio = base64.b64encode((ROOT / rec['file']).read_bytes()).decode()
        cards.append(f'<article><span>{i+1:02}</span><h2>{labels[i]}</h2><p>{rec["name"]} · {rec["duration"]:.2f} s</p>'
            f'<audio controls preload="none" src="data:audio/wav;base64,{audio}"></audio></article>')
    html = '''<!doctype html><html lang="es"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Dragon · Sonidos integrados</title><style>body{background:#10171a;color:#e9f0ec;font:16px system-ui;max-width:1050px;margin:48px auto;padding:0 24px}h1{font-size:38px;margin-bottom:12px}header p{color:#b6c8bf;max-width:750px;line-height:1.7}main{display:grid;grid-template-columns:repeat(auto-fit,minmax(280px,1fr));gap:18px;margin:30px 0}article{padding:24px;background:#1b2826;border:1px solid #34483f;border-radius:16px}span{color:#b0d776}h2{font-size:19px}article p{color:#a5b8b1;font-size:13px}audio{width:100%}a{color:#c4e599}</style>
<header><p>MISMO / SOUL EATER</p><h1>Sonidos para probar en combate</h1><p>Free Monster Sounds + el rugido con fuego de Daniel Simon. Estos son los clips del perfil del boss; podemos cambiar cada uno por separado después de la prueba.</p></header><main>'''
    html += ''.join(cards) + '''</main><p>Créditos: <a href="https://coucassi.itch.io/free-monster-sounds">Creature Sounds / Coucassi</a> · <a href="https://soundbible.com/2127-Dragon-Fire-Breath-and-Roar.html">Daniel Simon / SoundBible</a>.</p><script>document.addEventListener('play',e=>{document.querySelectorAll('audio').forEach(a=>{if(a!==e.target)a.pause()})},true)</script></html>'''
    (OUT / 'integrados.html').write_text(html, encoding='utf-8')
    print(json.dumps(records, indent=2))


if __name__ == '__main__':
    main()
