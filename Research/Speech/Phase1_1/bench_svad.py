import time, wave, struct
import torch
from silero_vad import load_silero_vad, get_speech_timestamps
t0 = time.time()
model = load_silero_vad()
print(f"LOAD_S {time.time()-t0:.1f}", flush=True)
for f in ["sapi_red.wav", "sapi_red_apple.wav", "pregen_apple_normal.wav",
          "stress_silence.wav", "stress_noise.wav", "stress_tone.wav"]:
    p = r"Research\Speech\Phase1_1\audio\\" + f
    w = wave.open(p, "rb")
    n = w.getnframes()
    audio = torch.tensor([x / 32768.0 for x in struct.unpack("<" + "h" * n, w.readframes(n))])
    s = time.time()
    ts = get_speech_timestamps(audio, model, return_seconds=True)
    print("FILE", f, "SEG=" + str([(round(a["start"], 2), round(a["end"], 2)) for a in ts]),
          f"T={time.time()-s:.2f}s", flush=True)
