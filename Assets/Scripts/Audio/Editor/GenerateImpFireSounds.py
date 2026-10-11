"""Build original fire Foley around the approved Imp B recordings (NumPy only).

Does not rewrite the approved voice files or game settings. Rebuild after voice
auditions with: python Assets/Scripts/Audio/Editor/GenerateImpFireSounds.py
"""
from pathlib import Path
import hashlib
import json
import sys
import wave

sys.dont_write_bytecode = True
import numpy as np
from GenerateImpVoiceConcepts import ensure_meta, player

ROOT = Path(__file__).resolve().parents[4]
VOICE = ROOT / "Assets/Art/Audio/Enemies/Imp/Concept"
DEST = VOICE.parent / "Fire"
OUT = ROOT / "output/imp-audio"
RATE = 48000
RNG = np.random.default_rng(740118)


def clock(seconds):
    return np.arange(round(seconds * RATE)) / RATE


def noise(n, low, high):
    raw = RNG.normal(size=n)
    f = np.fft.rfftfreq(n, 1 / RATE)
    shape = (1 - np.exp(-(f / low) ** 4)) * np.exp(-(f / high) ** 4)
    value = np.fft.irfft(np.fft.rfft(raw) * shape, n=n)
    return value / max(np.std(value), 1e-6)


def crackle(seconds, count, strength):
    result = np.zeros(round(seconds * RATE))
    for _ in range(count):
        offset = int(RNG.uniform(.015, max(.016, seconds - .04)) * RATE)
        duration = RNG.uniform(.006, .022)
        t = clock(duration)
        grain = noise(len(t), 800, 6500) * np.exp(-t / (duration / 5))
        grain *= 1 - np.exp(-t / .0006)
        n = min(len(result) - offset, len(grain))
        result[offset:offset+n] += grain[:n] * RNG.uniform(.4, 1) * strength
    return result


def read_voice(name):
    with wave.open(str(VOICE / f"Imp_Balanced_{name}.wav"), "rb") as w:
        assert (w.getframerate(), w.getsampwidth(), w.getnchannels()) == (RATE, 2, 1)
        return np.frombuffer(w.readframes(w.getnframes()), "<i2").astype(float) / 32768


def mix(destination, source, at=0, gain=1):
    offset = round(at * RATE)
    count = min(len(destination)-offset, len(source))
    destination[offset:offset+count] += source[:count] * gain


def finish(signal, peak):
    signal -= np.mean(signal)
    signal = np.tanh(signal * 1.15)
    signal[:192] *= np.sin(np.linspace(0, np.pi/2, 192)) ** 2
    signal[-1920:] *= np.cos(np.linspace(0, np.pi/2, 1920)) ** 2
    signal *= peak / max(np.max(np.abs(signal)), 1e-8)
    signal[0] = signal[-1] = 0
    return signal


def fire_prepare():
    t = clock(1.18)  # fits the existing 1.2 s Imp fireball telegraph
    progress = t / t[-1]
    flutter = .8 + .12*np.sin(2*np.pi*11*t) + .08*np.sin(2*np.pi*23*t)
    envelope = np.minimum(t/.07, 1) * (.18 + .82*progress**1.5)
    fire = (.027*noise(len(t), 130, 1300) + .021*noise(len(t), 1100, 6800))
    fire *= envelope * flutter
    fire += crackle(1.18, 24, .032) * envelope
    mix(fire, read_voice("Prepare"), .025, .85)
    return finish(fire, .51)


def fire_launch():
    t = clock(.64)
    blast = (1 - np.exp(-t/.009)) * np.exp(-t/.14)
    signal = (.068*noise(len(t), 160, 1800) + .042*noise(len(t), 1400, 8200))*blast
    signal += crackle(.64, 13, .021) * np.exp(-t/.21)
    mix(signal, read_voice("Attack"), .006, .92)
    return finish(signal, .62)


def fire_impact():
    t = clock(1.25)
    # A spreading flame: rounded ignition, uneven combustion, fading embers.
    # The former pressure pulse and abrupt broadband burst sounded like a pop.
    ignition = (1 - np.exp(-t/.045)) ** 2
    flame = ignition * np.exp(-t/.34)
    flame += .28*np.exp(-.5*((t-.29)/.12)**2) * ignition
    knots = np.arange(0, t[-1]+.035, .035)
    turbulence = np.interp(t, knots, RNG.uniform(.58, 1.15, len(knots)))
    turbulence *= .91 + .06*np.sin(2*np.pi*17*t) + .03*np.sin(2*np.pi*29*t)
    signal = (.18*noise(len(t), 230, 1800) + .075*noise(len(t), 1400, 6200))
    signal *= flame * turbulence
    # Small overlapping crackles sit inside the flame instead of leading it.
    signal += crackle(1.25, 86, .022) * ignition * np.exp(-t/.52)
    signal += .025*noise(len(t), 650, 3500) * ignition * np.exp(-t/.48)
    return finish(signal, .64)


def write(path, signal, asset=True):
    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(RATE)
        w.writeframes(np.round(signal*32767).astype("<i2").tobytes())
    if asset:
        ensure_meta(path, "audio")
    assert np.all(np.isfinite(signal)) and np.max(np.abs(signal)) < .9
    assert signal[0] == signal[-1] == 0 and abs(np.mean(signal)) < .003
    return dict(file=path.relative_to(ROOT).as_posix(), seconds=round(len(signal)/RATE, 3),
        peak_dbfs=round(float(20*np.log10(np.max(np.abs(signal)))), 2),
        rms_dbfs=round(float(20*np.log10(np.sqrt(np.mean(signal**2)))), 2),
        sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def main():
    global RNG
    RNG = np.random.default_rng(740118)
    DEST.mkdir(parents=True, exist_ok=True)
    OUT.mkdir(parents=True, exist_ok=True)
    ensure_meta(DEST, "folder")
    protected = {p:hashlib.sha256(p.read_bytes()).hexdigest() for p in VOICE.glob('Imp_Balanced_*.wav')}
    clips = dict(Prepare=fire_prepare(), Launch=fire_launch(), Impact=fire_impact())
    report = [write(DEST / f"Imp_Fire_{name}.wav", value) for name, value in clips.items()]
    # Listening sequence at game volumes, with an example 6 m flight (15 m/s).
    sequence = np.zeros(round(3.4*RATE))
    mix(sequence, clips['Prepare'], .15, .78)
    mix(sequence, clips['Launch'], 1.35, .68)
    mix(sequence, clips['Impact'], 1.75, .65)
    write(OUT / 'Imp_Fire_Sequence.wav', sequence, asset=False)
    assert all(hashlib.sha256(p.read_bytes()).hexdigest()==h for p,h in protected.items())
    (OUT/'fire-audio-analysis.json').write_text(json.dumps(dict(rate=RATE, channels=1, bits=16,
        generator='Original procedural fire plus approved B voice for preparation and launch',
        approved_voice_sha256={p.name:h for p,h in protected.items()}, clips=report), indent=2), encoding='utf-8')
    cards = []
    for label, path in [
        ('Voz · preparación aprobada', VOICE/'Imp_Balanced_Prepare.wav'),
        ('Voz · ataque B', VOICE/'Imp_Balanced_Attack.wav'),
        ('Voz · hit B', VOICE/'Imp_Balanced_Hit.wav'),
        ('Fuego · preparación con voz', DEST/'Imp_Fire_Prepare.wav'),
        ('Fuego · lanzamiento con voz', DEST/'Imp_Fire_Launch.wav'),
        ('Fuego · impacto de llamas', DEST/'Imp_Fire_Impact.wav')]:
        cards.append(f'<article><h2>{label}</h2>{player(path)}</article>')
    page = '''<!doctype html><html lang="es"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Imp · Voz y fuego</title><style>*{box-sizing:border-box}body{margin:0;background:#171719;color:#f5eee6;font:16px system-ui,sans-serif}
main{max-width:950px;margin:auto;padding:40px 24px}small,a{color:#efb185}h1{font-size:38px}p{color:#c2b9b2;line-height:1.6}
section,article{background:#242124;border:1px solid #44383b;border-radius:14px;padding:22px}section{margin:26px 0}h2{font-size:18px}
.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(260px,1fr));gap:16px}audio{width:100%}footer{margin:30px 0;font-size:13px;line-height:1.7}</style>
<main><small>MISMO · IMP · VOZ B</small><h1>Voz y bola de fuego</h1><p>La voz elegida para combate, acompañada por carga de fuego, lanzamiento e impacto de llamas con crepitar.</p>
<section><h2>Secuencia de bola de fuego</h2><p>Preparación de 1,2 s → lanzamiento → impacto. Ejemplo con un objetivo a 6 metros.</p>'''
    page += player(OUT/'Imp_Fire_Sequence.wav') + '</section><div class="grid">' + ''.join(cards) + '</div>'
    page += '''<footer>Voz: <a href="https://opengameart.org/content/voices-sound-effects-library">Little Robot Sound Factory, Voices Sound Effects Library</a>,
<a href="https://creativecommons.org/licenses/by/3.0/">CC BY 3.0</a>. Edición, mezcla y efectos de fuego para Mismo.
Referencia de dirección: Hellspawn. No contiene audio de ese pack.</footer></main>
<script>document.querySelectorAll('audio').forEach(a=>a.addEventListener('play',()=>document.querySelectorAll('audio').forEach(b=>{if(a!==b)b.pause()})))</script></html>'''
    (OUT/'escuchar.html').write_text(page, encoding='utf-8')
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()
