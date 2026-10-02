import time
from pywhispercpp.model import Model
t0 = time.time()
m = Model("tiny", models_dir=r"D:\speech-lab\models\pwc")
print(f"LOAD_S {time.time()-t0:.1f}", flush=True)
for f in ["sapi_red.wav", "sapi_blue.wav", "sapi_cat.wav", "sapi_red_apple.wav",
          "pregen_apple_normal.wav", "stress_silence.wav"]:
    p = r"Research\Speech\Phase1_1\audio\\" + f
    s = time.time()
    segs = m.transcribe(p, language="en")
    txt = " ".join(x.text.strip() for x in segs)
    print("FILE", f, "TEXT=[" + txt + "]", f"T={time.time()-s:.2f}s", flush=True)
print("MODEL", m.model_path if hasattr(m, "model_path") else "tiny", flush=True)
