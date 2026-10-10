"""Edit CC0 recorded goblin voices with subtle original Foley. Requires numpy.

Run from any directory with Python. Replaces only the seven named WAVs, preserves
existing GUIDs, and writes a listening reel / measurements outside Assets.
Source: artisticdude, https://opengameart.org/content/goblins-sound-pack (CC0).
Original 24-bit recordings are preserved in the adjacent Source asset folder.
"""
from pathlib import Path
import base64
import json
import uuid
import wave

import numpy as np

ROOT = Path(__file__).resolve().parents[4]
DEST = ROOT / "Assets/Art/Audio/Enemies/Goblin"
OUT = ROOT / "output/goblin-audio"
RATE = 48000
RNG = np.random.default_rng(640617)


def time(duration):
    return np.arange(round(duration * RATE)) / RATE


def filtered_noise(t, low, high):
    n = RNG.normal(size=len(t))
    f = np.fft.rfftfreq(len(t), 1 / RATE)
    shape = (1 - np.exp(-(f / low) ** 4)) * np.exp(-(f / high) ** 4)
    n = np.fft.irfft(np.fft.rfft(n) * shape, n=len(t))
    return n / max(np.std(n), .001)


def envelope(t, attack, decay):
    return (1 - np.exp(-t / attack)) * np.exp(-t / decay)


def add(destination, signal, start=0, gain=1):
    offset = round(start * RATE)
    count = min(len(destination) - offset, len(signal))
    destination[offset:offset + count] += signal[:count] * gain


def leather(duration, strength=1):
    t = time(duration)
    n = filtered_noise(t, 220, 3400)
    rub = (.65 + .22 * np.sin(2 * np.pi * 43 * t) + .13 * np.sin(2 * np.pi * 71 * t))
    return strength * n * rub * envelope(t, .009, duration / 4)


def recorded_voice(index, speed=.944):
    """Retain the performed phrasing; varispeed lowers pitch about one semitone."""
    with wave.open(str(DEST / 'Source' / f'goblin-{index}.wav'), 'rb') as stream:
        if stream.getsampwidth() != 3:
            raise ValueError('Expected the original 24-bit PCM recording')
        raw = np.frombuffer(stream.readframes(stream.getnframes()), np.uint8).reshape(-1, 3).astype(np.int32)
        pcm = raw[:, 0] | raw[:, 1] << 8 | raw[:, 2] << 16
        pcm = np.where(pcm & 0x800000, pcm - 0x1000000, pcm) / 8388608
        pcm = pcm.reshape(-1, stream.getnchannels()).mean(axis=1)
        source_rate = stream.getframerate()
    pcm -= np.mean(pcm)
    # Trim only silence, with a small margin to retain consonants and breaths.
    voiced = np.flatnonzero(np.abs(pcm) > np.max(np.abs(pcm)) * .006)
    if not len(voiced): raise ValueError('Source recording is silent')
    margin = round(source_rate * .008)
    pcm = pcm[max(0, voiced[0] - margin):min(len(pcm), voiced[-1] + margin + 1)]
    positions = np.arange(0, len(pcm) - 1, source_rate * speed / RATE)
    pcm = np.interp(positions, np.arange(len(pcm)), pcm)
    # Gentle cleanup and chest resonance. No oscillator or generated voice layer.
    padded = np.pad(pcm, (2048, 2048))
    frequencies = np.fft.rfftfreq(len(padded), 1 / RATE)
    eq = (1 - np.exp(-(frequencies / 95) ** 4)) * np.exp(-(frequencies / 9000) ** 6)
    eq *= 1 + .15 * np.exp(-.5 * ((frequencies - 430) / 250) ** 2)
    pcm = np.fft.irfft(np.fft.rfft(padded) * eq, n=len(padded))[2048:-2048]
    pcm /= max(np.max(np.abs(pcm)), .001)
    return .85 * pcm + .15 * np.tanh(pcm * 1.7) / np.tanh(1.7)


def stamp(duration, pitch=115):
    t = time(duration)
    phase = 2 * np.pi * (pitch * .62 * t + pitch * .38 * .025 * (1 - np.exp(-t / .025)))
    return (np.sin(phase) * np.exp(-t / .055) + .2 * filtered_noise(t, 140, 1900)
            * np.exp(-t / .023)) * (1 - np.exp(-t / .002))


def finish(signal, peak):
    signal -= np.mean(signal)
    signal = np.tanh(signal * 1.1)
    fade_in = min(96, len(signal))
    fade_out = min(960, len(signal))
    signal[:fade_in] *= np.linspace(0, 1, fade_in) ** 2
    signal[-fade_out:] *= np.linspace(1, 0, fade_out) ** 2
    signal *= peak / max(np.max(np.abs(signal)), .0001)
    return signal


def write_wav(path, signal):
    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), "wb") as stream:
        stream.setnchannels(1)
        stream.setsampwidth(2)
        stream.setframerate(RATE)
        stream.writeframes(np.round(signal * 32767).astype("<i2").tobytes())


def meta(path, folder=False):
    meta_path = Path(str(path) + ".meta")
    if meta_path.exists():
        return
    guid = uuid.uuid5(uuid.NAMESPACE_URL, "mismo-goblin-audio/" + path.relative_to(ROOT).as_posix()).hex
    if folder:
        body = "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n"
    else:
        body = ("AudioImporter:\n  externalObjects: {}\n  serializedVersion: 8\n"
                "  defaultSettings:\n    serializedVersion: 2\n    loadType: 0\n"
                "    sampleRateSetting: 0\n    sampleRateOverride: 48000\n"
                "    compressionFormat: 0\n    quality: 1\n    conversionMode: 0\n"
                "    preloadAudioData: 1\n  platformSettingOverrides: {}\n"
                "  forceToMono: 1\n  normalize: 0\n  loadInBackground: 0\n  ambisonic: 0\n  3D: 1\n")
    meta_path.write_text(f"fileFormatVersion: 2\nguid: {guid}\n{body}  userData: \n  assetBundleName: \n  assetBundleVariant: \n", encoding="utf-8")


def main():
    global RNG
    RNG = np.random.default_rng(640617)
    clips = {}
    s = np.zeros(round(.40 * RATE))
    add(s, leather(.24), gain=.016)
    add(s, recorded_voice(1), .025, .82)
    clips["Goblin_Slash_Prepare"] = finish(s, .48)

    # Exact recipe of the approved B preview: the familiar voice on its own.
    effort = finish(recorded_voice(2), .52)
    clips["Goblin_Slash_Execute"] = effort

    # Preserve the charge preparation's seeded Foley: the removed slash
    # whoosh previously consumed two 0.31-second noise buffers here.
    RNG.normal(size=2 * round(.31 * RATE))

    s = np.zeros(round(.46 * RATE))
    add(s, leather(.24), gain=.014)
    add(s, recorded_voice(5), .025, .94)
    add(s, stamp(.17, 120), .075, .025)
    clips["Goblin_Charge_Prepare"] = finish(s, .46)

    clips["Goblin_Charge_Execute"] = effort.copy()

    for index, source_index in enumerate([6, 10, 13], 1):
        s = recorded_voice(source_index)
        clips[f"Goblin_Hit_{index:02}"] = finish(s, .61)

    DEST.mkdir(parents=True, exist_ok=True)
    meta(DEST.parent, folder=True)
    meta(DEST, folder=True)
    report = []
    for name, signal in clips.items():
        path = DEST / (name + ".wav")
        write_wav(path, signal)
        meta(path)
        assert np.all(np.isfinite(signal)) and np.max(np.abs(signal)) < .9
        assert signal[0] == 0 and signal[-1] == 0
        assert abs(np.mean(signal)) < .003
        report.append(dict(name=name, seconds=round(len(signal) / RATE, 3),
                           peak_dbfs=round(20 * np.log10(np.max(np.abs(signal))), 2),
                           rms_dbfs=round(20 * np.log10(np.sqrt(np.mean(signal ** 2))), 2)))

    # First each take separately, then two timed attacks plus confirmed hits.
    reel = []
    schedule = []
    for name, signal in clips.items():
        schedule.append(dict(at=round(sum(len(x) for x in reel) / RATE, 3), name=name))
        reel.extend([signal, np.zeros(round(.55 * RATE))])
    for action, preparation_volume, execution_volume in [("Slash", .6, .78), ("Charge", .65, .8)]:
        sequence = np.zeros(round(1.65 * RATE))
        add(sequence, clips[f"Goblin_{action}_Prepare"], gain=preparation_volume)
        add(sequence, clips[f"Goblin_{action}_Execute"], .5, execution_volume)
        add(sequence, clips["Goblin_Hit_01"], 1.06, .58)
        schedule.append(dict(at=round(sum(len(x) for x in reel) / RATE, 3), name=f"Sequence_{action}"))
        reel.append(sequence)
    OUT.mkdir(parents=True, exist_ok=True)
    write_wav(OUT / "Goblin_Audio_Preview.wav", np.concatenate(reel))
    (OUT / "audio-analysis.json").write_text(json.dumps(dict(sample_rate=RATE, channels=1, bits=16,
        voice_source="Goblins Sound Pack / artisticdude / CC0-1.0", version="approved-effort-B",
        clips=report, preview=schedule), indent=2), encoding="utf-8")
    write_preview_page()
    print(json.dumps(report, indent=2))


def write_preview_page():
    labels = [
        ("Goblin_Slash_Prepare", "Golpe · preparación"),
        ("Goblin_Slash_Execute", "Golpe · esfuerzo B"),
        ("Goblin_Charge_Prepare", "Carga · preparación"),
        ("Goblin_Charge_Execute", "Carga · esfuerzo B"),
        ("Goblin_Hit_01", "Hit · toma 1"),
        ("Goblin_Hit_02", "Hit · toma 2"),
        ("Goblin_Hit_03", "Hit · toma 3"),
    ]
    def player(path):
        encoded = base64.b64encode(path.read_bytes()).decode('ascii')
        return f'<audio controls preload="none" src="data:audio/wav;base64,{encoded}"></audio>'
    cards = ''.join(f'<article id="{name}"><h2>{label}</h2>{player(DEST / (name + ".wav"))}</article>' for name, label in labels)
    html = '''<!doctype html><html lang="es"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Goblin · Voces grabadas</title><style>
*{box-sizing:border-box}body{margin:0;background:#171c17;color:#eef2e9;font:16px system-ui,sans-serif}
main{max-width:920px;margin:auto;padding:44px 24px}small,a{color:#b9d991}h1{font-size:38px;margin:14px 0}
p{color:#bcc8b7;line-height:1.5}h2{font-size:18px}section,article{background:#252e23;border:1px solid #3b4a35;border-radius:12px;padding:22px}
section{margin:28px 0}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(260px,1fr));gap:14px}audio{width:100%;margin-top:10px}
footer{font-size:13px;line-height:1.6;color:#a4b49b;margin-top:30px}
article:target{border-color:#b9d991}
</style><main><small>MISMO · NUEVA VERSIÓN</small><h1>Goblin: voces grabadas</h1>
<p>Golpe y Carga usan el esfuerzo B aprobado: la voz goblin-2 aislada, sin capas de movimiento. Se conservan las preparaciones y las reacciones al daño.</p>
<section><h2>Escuchar todo</h2><p>Primero los siete sonidos separados. Al final, las dos habilidades con su preparación de 0,5 segundos y una reacción de hit posterior.</p>'''
    html += player(OUT / 'Goblin_Audio_Preview.wav') + '</section><div class="grid">' + cards + '</div>'
    html += '''<footer>Voz: <a href="https://opengameart.org/content/goblins-sound-pack">Goblins Sound Pack, artisticdude</a> · CC0.
Edición y capas de movimiento para Mismo. Inspiración de dirección: Goblin Vocalizations; esta alternativa no contiene audio de ese pack comercial.
</footer></main><script>document.querySelectorAll('audio').forEach(a=>a.addEventListener('play',()=>document.querySelectorAll('audio').forEach(b=>{if(b!==a)b.pause()})))</script></html>'''
    (OUT / 'escuchar.html').write_text(html, encoding='utf-8')


if __name__ == "__main__":
    main()
