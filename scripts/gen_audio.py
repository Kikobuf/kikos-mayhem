"""Regenerates the built-in ascension audio in src/KikosMayhem/Resources/Audio.

Usage: pip install piper-tts scipy numpy; download en_GB-alan-medium.onnx(+.json) from
https://huggingface.co/rhasspy/piper-voices, set PIPER_VOICE to its path, then: python scripts/gen_audio.py src/KikosMayhem/Resources/Audio
Voice: Piper TTS run through a telephone filter. SFX: synthesized with numpy.
"""
import subprocess, sys, wave, os
import numpy as np
from scipy.signal import butter, sosfilt, resample_poly

OUT = sys.argv[1]
VOICE = os.environ.get("PIPER_VOICE", "en_GB-alan-medium.onnx")
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(7)


def save(name, x, rate):
    x = np.clip(x, -1, 1)
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(rate)
        w.writeframes((x * 32767).astype("<i2").tobytes())


def tts(text):
    tmp = os.path.join(OUT, "_line.tmp.wav")
    subprocess.run([sys.executable, "-m", "piper", "-m", VOICE, "-f", tmp, "--length-scale", "1.08"],
                   input=text.encode(), check=True, capture_output=True)
    with wave.open(tmp) as w:
        rate = w.getframerate()
        x = np.frombuffer(w.readframes(w.getnframes()), "<i2").astype(np.float32) / 32768
    x = resample_poly(x, 16000, rate)
    # trim silence
    idx = np.where(np.abs(x) > 0.02)[0]
    x = x[max(idx[0] - 400, 0): idx[-1] + 800]
    return x


def telephone(x, rate=16000):
    sos = butter(4, [320, 3300], btype="band", fs=rate, output="sos")
    y = sosfilt(sos, x)
    y = np.tanh(y * 2.2) / np.tanh(2.2)          # handset grit
    y = y / (np.max(np.abs(y)) + 1e-9) * 0.85
    y += rng.normal(0, 0.006, len(y))             # line hiss
    return y


def fade(x, rate, ms=6):
    n = int(rate * ms / 1000)
    x = x.copy(); x[:n] *= np.linspace(0, 1, n); x[-n:] *= np.linspace(1, 0, n)
    return x


# ── voice lines ──
SPOKEN = ["Alpha", "Bravo", "Charlie", "Delta", "Echo", "Foxtrot", "Golf", "Hotel", "Imminent", "Juliet"]
for i, word in enumerate(SPOKEN):
    save(f"nato_{i}", fade(telephone(tts(word + ".")), 16000), 16000)
save("phone_intro", fade(telephone(tts("This line is not secure. Recontainment override code follows.")), 16000), 16000)
save("phone_repeat", fade(telephone(tts("I repeat.")), 16000), 16000)
save("phone_outro", fade(telephone(tts("End of transmission.")), 16000), 16000)

# ── SFX ──
R = 24000
t = lambda s: np.arange(int(R * s)) / R

# Old bell phone: 440+480 Hz trilled at 20 Hz, 2 s ring then 4 s quiet (loops).
tt = t(2.0)
bell = (np.sin(2 * np.pi * 440 * tt) + np.sin(2 * np.pi * 480 * tt) + 0.3 * np.sin(2 * np.pi * 1320 * tt))
bell *= (0.5 + 0.5 * np.sign(np.sin(2 * np.pi * 20 * tt))) * 0.35
save("phone_ring", np.concatenate([fade(bell, R, 15), np.zeros(int(R * 4.0))]), R)

# Handset pick-up click + a little line noise.
click = np.zeros(int(R * 0.35)); n = int(R * 0.012)
click[:n] = rng.normal(0, 0.7, n) * np.linspace(1, 0, n)
click[int(R * 0.05):] += rng.normal(0, 0.01, len(click) - int(R * 0.05))
save("phone_pickup", click, R)

# Hang-up: click then three busy tones.
busy = []
for _ in range(3):
    b = t(0.25); busy += [fade(0.25 * (np.sin(2 * np.pi * 480 * b) + np.sin(2 * np.pi * 620 * b)), R), np.zeros(int(R * 0.25))]
save("phone_hangup", np.concatenate([click[: int(R * 0.1)], np.zeros(int(R * 0.2))] + busy), R)

# Keypad: short DTMF-style blip, accept chime, deny buzz.
b = t(0.09)
save("keypad_beep", fade(0.35 * (np.sin(2 * np.pi * 1209 * b) + np.sin(2 * np.pi * 852 * b)), R), R)
a1, a2 = t(0.12), t(0.22)
save("keypad_accept", np.concatenate([fade(0.5 * np.sin(2 * np.pi * 880 * a1), R), fade(0.5 * np.sin(2 * np.pi * 1320 * a2), R)]), R)
d = t(0.6)
save("keypad_deny", fade(0.4 * np.sign(np.sin(2 * np.pi * 140 * d)) * (0.6 + 0.4 * np.sin(2 * np.pi * 8 * d)), R, 20), R)

# PC: two quick terminal beeps.
p = t(0.07)
beep = fade(0.4 * np.sign(np.sin(2 * np.pi * 1000 * p)) * 0.6, R)
save("pc_beep", np.concatenate([beep, np.zeros(int(R * 0.06)), beep]), R)

# Server rack hum: 60/120/180 Hz mains hum + filtered fan noise, 3 s seamless loop.
h = t(3.0)
hum = 0.18 * np.sin(2 * np.pi * 60 * h) + 0.12 * np.sin(2 * np.pi * 120 * h) + 0.05 * np.sin(2 * np.pi * 180 * h)
L, xf = len(h), int(R * 0.3)
raw = sosfilt(butter(2, [200, 1800], btype="band", fs=R, output="sos"), rng.normal(0, 1, L + xf + R))[R:]
raw = raw / np.max(np.abs(raw)) * 0.12
fan = raw[:L].copy(); w = np.linspace(0, 1, xf)
fan[:xf] = fan[:xf] * w + raw[L:L + xf] * (1 - w)  # crossfade so the loop seam is inaudible
save("server_rack_hum", hum + fan, R)

print("ok", sorted(os.listdir(OUT)))
