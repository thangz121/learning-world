import time, wave, struct
from faster_whisper import WhisperModel
t0 = time.time()
m = WhisperModel("tiny", device="cpu", compute_type="int8",
                 download_root=r"D:\speech-lab\models\fw")
print(f"LOAD_S {time.time()-t0:.1f}", flush=True)
for f in ["sapi_red.wav", "sapi_blue.wav", "sapi_cat.wav", "sapi_red_apple.wav",
          "pregen_apple_normal.wav", "stress_silence.wav"]:
    p = r"Research\Speech\Phase1_1\audio\\" + f
    w = wave.open(p, "rb")
    n = w.getnframes()
    pcm = struct.unpack("<" + "h" * n, w.readframes(n))
    import numpy as np
    audio = __import__("numpy").array(pcm, dtype=np.float32) / 32768.0
    s = time.time()
    segs, info = m.transcribe(audio, language="en")
    txt = " ".join(x.text.strip() for x in segs)
    print(f"FILE {f} TEXT=[{txt}] LANG={info.language} P={info.language_probability:.2f} T={time.time()-s:.2f}s", flush=True)
