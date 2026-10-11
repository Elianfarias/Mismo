"""Build Imp voice auditions; the listening page follows the selected B profile.

Requires NumPy. No external service, commercial demo audio, or game-data edits.
The WAV originals and provenance are preserved alongside the derived assets.
Run: python Assets/Scripts/Audio/Editor/GenerateImpVoiceConcepts.py
"""
from pathlib import Path
import base64
import hashlib
import html
import json
import uuid
import wave

import numpy as np

ROOT = Path(__file__).resolve().parents[4]
DEST = ROOT / "Assets/Art/Audio/Enemies/Imp/Concept"
SOURCE = DEST.parent / "Source"
OUT = ROOT / "output/imp-audio"
RATE = 48000
# The labels below describe proposed game uses, not the source actor's intent.
TAKES = [
    ("Prepare", "Preparación · nueva toma corta", "SmallCreature3_17.wav", .005, .445),
    ("Attack", "Ataque · B elegido", "SmallCreature3_09.wav", .015, .535),
    ("Hit", "Hit · B elegido", "SmallCreature3_10.wav", .015, .555),
]
PROFILES = [
    dict(key="Body", title="A · Con cuerpo", semitones=-1.0, bite=.12, body=.30,
         presence=.18, description="Tono algo más bajo y más peso en la voz."),
    dict(key="Balanced", title="B · Equilibrada", semitones=.6, bite=.20, body=.14,
         presence=.32, description="Tono intermedio, con más presencia y aspereza."),
    dict(key="Sharp", title="C · Más aguda", semitones=2.2, bite=.15, body=.04,
         presence=.38, description="Voz más alta y ligera, con el mismo fraseo."),
]


def ensure_meta(path, kind="default"):
    target = Path(str(path) + ".meta")
    if target.exists():
        return
    guid = uuid.uuid5(uuid.NAMESPACE_URL, "mismo-imp-audition/" + path.relative_to(ROOT).as_posix()).hex
    if kind == "folder":
        body = "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n"
    elif kind == "audio":
        body = ("AudioImporter:\n  externalObjects: {}\n  serializedVersion: 8\n"
                "  defaultSettings:\n    serializedVersion: 2\n    loadType: 0\n"
                "    sampleRateSetting: 0\n    sampleRateOverride: 48000\n"
                "    compressionFormat: 0\n    quality: 1\n    conversionMode: 0\n"
                "    preloadAudioData: 1\n  platformSettingOverrides: {}\n"
                "  forceToMono: 1\n  normalize: 0\n  loadInBackground: 0\n"
                "  ambisonic: 0\n  3D: 1\n")
    else:
        importer = "TextScriptImporter" if kind == "text" else "DefaultImporter"
        body = f"{importer}:\n  externalObjects: {{}}\n"
    target.write_text(f"fileFormatVersion: 2\nguid: {guid}\n{body}"
                      "  userData:\n  assetBundleName:\n  assetBundleVariant:\n", encoding="utf-8")


def read_pcm(path):
    with wave.open(str(path), "rb") as stream:
        if stream.getsampwidth() != 3 or stream.getframerate() != RATE:
            raise ValueError(f"Expected original 24-bit / 48 kHz recording: {path}")
        raw = np.frombuffer(stream.readframes(stream.getnframes()), np.uint8).reshape(-1, 3).astype(np.int32)
        pcm = raw[:, 0] | (raw[:, 1] << 8) | (raw[:, 2] << 16)
        pcm = np.where(pcm & 0x800000, pcm - 0x1000000, pcm) / 8388608
        return pcm.reshape(-1, stream.getnchannels()).mean(axis=1)


def eq(signal, body, presence):
    pad = 4096
    padded = np.pad(signal, (pad, pad))
    f = np.fft.rfftfreq(len(padded), 1 / RATE)
    shape = (1 - np.exp(-(f / 135) ** 4)) * np.exp(-(f / 8400) ** 6)
    shape *= 1 + body * np.exp(-.5 * ((f - 450) / 250) ** 2)
    shape *= 1 + presence * np.exp(-.5 * ((f - 1700) / 650) ** 2)
    shape *= 1 - .13 * np.exp(-.5 * ((f - 4200) / 900) ** 2)
    return np.fft.irfft(np.fft.rfft(padded) * shape, n=len(padded))[pad:-pad]


def finish(signal):
    signal -= np.mean(signal)
    # Equal RMS for a useful A/B comparison, with a conservative peak ceiling.
    target_rms = 10 ** (-20 / 20)
    rms = np.sqrt(np.mean(signal * signal))
    peak = np.max(np.abs(signal))
    signal *= min(target_rms / max(rms, 1e-9), .65 / max(peak, 1e-9))
    # Short fades remove edit clicks while preserving the recorded vocal onset.
    fade_in = min(round(.004 * RATE), len(signal) // 4)
    fade_out = min(round(.020 * RATE), len(signal) // 4)
    signal[:fade_in] *= np.sin(np.linspace(0, np.pi / 2, fade_in)) ** 2
    signal[-fade_out:] *= np.cos(np.linspace(0, np.pi / 2, fade_out)) ** 2
    signal[0] = signal[-1] = 0
    return signal


def design_voice(source, start, end, profile):
    signal = read_pcm(SOURCE / source)[round(start * RATE):round(end * RATE)]
    signal -= np.mean(signal)
    # Small varispeed changes preserve the actor's roughness and articulation.
    speed = 2 ** (profile["semitones"] / 12)
    positions = np.arange(0, len(signal) - 1, speed)
    signal = np.interp(positions, np.arange(len(signal)), signal)
    signal = eq(signal, profile["body"], profile["presence"])
    signal /= max(np.max(np.abs(signal)), .001)
    saturated = np.tanh(signal * 2.1) / np.tanh(2.1)
    signal = (1 - profile["bite"]) * signal + profile["bite"] * saturated
    return finish(signal)


def write_wav(path, signal, asset=True):
    path.parent.mkdir(parents=True, exist_ok=True)
    pcm = np.round(signal * 32767).astype("<i2")
    with wave.open(str(path), "wb") as stream:
        stream.setnchannels(1)
        stream.setsampwidth(2)
        stream.setframerate(RATE)
        stream.writeframes(pcm.tobytes())
    if asset:
        ensure_meta(path, "audio")
    decoded = pcm.astype(float) / 32768
    assert np.all(np.isfinite(signal)) and np.max(np.abs(decoded)) < .66
    assert decoded[0] == decoded[-1] == 0 and abs(np.mean(decoded)) < .003
    return dict(file=path.relative_to(ROOT).as_posix(), seconds=round(len(pcm) / RATE, 3),
                peak_dbfs=round(float(20 * np.log10(np.max(np.abs(decoded)))), 2),
                rms_dbfs=round(float(20 * np.log10(np.sqrt(np.mean(decoded ** 2)))), 2),
                sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def player(path):
    encoded = base64.b64encode(path.read_bytes()).decode("ascii")
    return f'<audio controls preload="none" src="data:audio/wav;base64,{encoded}"></audio>'


def preview_page():
    cards = []
    descriptions = {
        "Prepare": "Otra grabación, más breve y a menor volumen para anticipar el ataque.",
        "Attack": "El mismo archivo B que elegiste.",
        "Hit": "El mismo archivo B que elegiste.",
    }
    for name, label, *_ in TAKES:
        cards.append(f'<article id="{name}"><h2>{html.escape(label)}</h2>'
                     f'<p>{descriptions[name]}</p>'
                     f'{player(DEST / ("Imp_Balanced_" + name + ".wav"))}</article>')
    page = '''<!doctype html><html lang="es"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1"><title>Imp · Pruebas de voz</title>
<style>*{box-sizing:border-box}body{margin:0;background:#171719;color:#f5eee6;font:16px system-ui,sans-serif}
main{max-width:1000px;margin:auto;padding:40px 24px}small{color:#e6aa78;letter-spacing:.13em}
h1{font-size:clamp(30px,5vw,45px);margin:12px 0}p{line-height:1.6;color:#c2b9b2}a{color:#efb185}
section,article{background:#242124;border:1px solid #44383b;border-radius:14px;padding:22px}
section{margin:26px 0}h2{font-size:19px;margin:0 0 12px}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(260px,1fr));gap:16px}
audio{width:100%;margin:10px 0}details{margin-top:18px}summary{cursor:pointer;color:#efb185}
.take{margin-top:20px}.take span{font-size:14px}footer{font-size:13px;line-height:1.7;color:#ad9e96;margin:30px 0}
</style></head><body><main><small>MISMO · IMP · VOZ B</small><h1>Nueva preparación</h1>
<p>Conservamos el ataque y el hit de B. La preparación usa una toma distinta, corta y más suave.</p>
<section><h2>Escuchar los tres gestos</h2><p>Preparación nueva → ataque B → hit B.</p>'''
    page += player(DEST / "Imp_Balanced_Audition.wav") + '</section><div class="grid">' + "".join(cards) + '</div>'
    page += '''<footer>Muestras de voz sin las capas de fuego. La voz B es la seleccionada para el Imp.
<br>Voces originales: <a href="https://opengameart.org/content/voices-sound-effects-library">Voices Sound Effects Library,
Little Robot Sound Factory</a> · <a href="https://creativecommons.org/licenses/by/3.0/">CC BY 3.0</a> ·
<a href="https://www.littlerobotsoundfactory.com">Sitio del autor</a>.
Recorte, cambio de tono y velocidad, ecualización, saturación y ajuste de nivel para Mismo.
<br>AAA Game Character Hellspawn es una referencia; estas muestras no contienen audio de ese pack.</footer></main>
<script>document.querySelectorAll('audio').forEach(a=>a.addEventListener('play',()=>document.querySelectorAll('audio').forEach(b=>{if(b!==a)b.pause()})))</script>
</body></html>'''
    (OUT / "escuchar.html").write_text(page, encoding="utf-8")


def main():
    DEST.mkdir(parents=True, exist_ok=True)
    OUT.mkdir(parents=True, exist_ok=True)
    for directory in (DEST.parent, DEST, SOURCE):
        ensure_meta(directory, "folder")
    report, reels = [], []
    for p in PROFILES:
        parts = [np.zeros(round(.15 * RATE))]
        for name, _, source, start, end in TAKES:
            signal = design_voice(source, start, end, p)
            if name == "Prepare":
                # Replace the rejected performance, leaving attack/hit untouched.
                # Ease out the crop and keep the anticipation quieter than attack.
                tail = min(round(.08 * RATE), len(signal))
                signal[-tail:] *= np.cos(np.linspace(0, np.pi / 2, tail)) ** 2
                signal *= .78
            path = DEST / f'Imp_{p["key"]}_{name}.wav'
            report.append(write_wav(path, signal) | dict(profile=p["key"], source=source, crop=[start,end]))
            parts.extend([signal, np.zeros(round(.42 * RATE))])
        reel = np.concatenate(parts)
        report.append(write_wav(DEST / f'Imp_{p["key"]}_Audition.wav', reel))
        reels.extend([reel, np.zeros(round(1.1 * RATE))])
    report.append(write_wav(DEST / "Imp_Voice_Comparison.wav", np.concatenate(reels)))
    (OUT / "audio-analysis.json").write_text(json.dumps(dict(sample_rate=RATE, channels=1, bits=16,
        version="imp-voice-auditions-2-short-prepare", selected_profile="Balanced",
        profiles=PROFILES, clips=report), indent=2), encoding="utf-8")
    preview_page()
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
