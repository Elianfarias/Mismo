"""Original deterministic goblin Foley and creature vocals. Requires only numpy.

Run from any directory with Python. Replaces only the seven named WAVs, preserves
existing GUIDs, and writes a listening reel / measurements outside Assets.
"""
from pathlib import Path
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


def metal(duration, base):
    t = time(duration)
    result = np.zeros(len(t))
    for ratio, amp, decay in [(1, 1, .09), (1.47, .5, .065), (2.09, .35, .04), (2.63, .16, .02)]:
        result += amp * np.sin(2 * np.pi * base * ratio * t + .15) * np.exp(-t / decay)
    return result * (1 - np.exp(-t / .0015))


def grunt(duration, pitch, intensity=1, vowel=0):
    """Irregular glottal pulses, three broad formants, breath and a short rasp."""
    t = time(duration)
    jitter = np.interp(t, np.linspace(0, duration, 35), RNG.normal(0, 1, 35))
    f0 = pitch * (1.14 - .32 * t / duration + .035 * jitter + .018 * np.sin(2 * np.pi * 31 * t))
    phase = 2 * np.pi * np.cumsum(f0) / RATE
    voice = np.zeros(len(t))
    formants = [(610 + 100 * vowel, 210, 1), (1320 - 110 * vowel, 330, .62), (2480, 460, .27)]
    for harmonic in range(1, 38):
        frequency = f0 * harmonic
        weight = sum(gain * np.exp(-.5 * ((frequency - center) / width) ** 2)
                     for center, width, gain in formants)
        voice += np.sin(harmonic * phase + .08 * harmonic) * weight / harmonic ** .65
    voice *= .72 + .20 * np.sin(phase / 2 + 1) + .08 * np.sin(2 * np.pi * 47 * t)
    voice += .25 * filtered_noise(t, 650, 4500)
    env = envelope(t, .012, duration / 3.7)
    env *= np.minimum((duration - t) / .045, 1)
    return intensity * np.tanh(voice * 1.5) * env


def whoosh(duration, weight=1):
    t = time(duration)
    x = t / duration
    airy = filtered_noise(t, 850, 9000)
    body = filtered_noise(t, 95, 1500)
    arc = (1 - np.exp(-t / .009)) * np.exp(-t / (duration / 3))
    blade = np.sin(2 * np.pi * (900 * t - 850 / (2 * duration) * t * t))
    return weight * arc * (.43 * airy * (1 - x) + .70 * body + .025 * blade)


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
    clips = {}
    s = np.zeros(round(.40 * RATE))
    add(s, leather(.24), gain=.17)
    add(s, metal(.20, 940), .045, .025)
    add(s, grunt(.29, 185, vowel=.2), .075, .25)
    clips["Goblin_Slash_Prepare"] = finish(s, .42)

    s = np.zeros(round(.34 * RATE))
    add(s, whoosh(.31), gain=.67)
    add(s, grunt(.22, 207, vowel=.7), .015, .29)
    add(s, metal(.18, 1210), .055, .023)
    clips["Goblin_Slash_Execute"] = finish(s, .68)

    s = np.zeros(round(.46 * RATE))
    add(s, leather(.24), gain=.18)
    add(s, grunt(.40, 151, vowel=-.6), .025, .60)
    add(s, stamp(.17, 120), .075, .10)
    clips["Goblin_Charge_Prepare"] = finish(s, .56)

    s = np.zeros(round(.43 * RATE))
    add(s, whoosh(.39), gain=.38)
    add(s, grunt(.32, 190, vowel=-.2), .005, .50)
    add(s, stamp(.16, 132), .045, .17)
    add(s, leather(.13), .07, .08)
    add(s, stamp(.15, 118), .235, .13)
    clips["Goblin_Charge_Execute"] = finish(s, .72)

    for index, (duration, pitch, vowel) in enumerate([(.27, 214, .8), (.31, 172, -.5), (.25, 239, .2)], 1):
        s = grunt(duration, pitch, vowel=vowel)
        add(s, leather(.08), gain=.055)
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
    for action in ["Slash", "Charge"]:
        sequence = np.zeros(round(1.65 * RATE))
        add(sequence, clips[f"Goblin_{action}_Prepare"], gain=.6)
        add(sequence, clips[f"Goblin_{action}_Execute"], .5, .78)
        add(sequence, clips["Goblin_Hit_01"], 1.06, .58)
        schedule.append(dict(at=round(sum(len(x) for x in reel) / RATE, 3), name=f"Sequence_{action}"))
        reel.append(sequence)
    OUT.mkdir(parents=True, exist_ok=True)
    write_wav(OUT / "Goblin_Audio_Preview.wav", np.concatenate(reel))
    (OUT / "audio-analysis.json").write_text(json.dumps(dict(sample_rate=RATE, channels=1, bits=16,
        clips=report, preview=schedule), indent=2), encoding="utf-8")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
