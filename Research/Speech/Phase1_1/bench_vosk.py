import time, wave, json
from vosk import Model, KaldiRecognizer
t0 = time.time()
m = Model(model_path=r"D:\speech-lab\models\vosk-model-small-en-us-0.15")
print(f"LOAD_S {time.time()-t0:.1f}", flush=True)
for f in ["sapi_red.wav", "sapi_blue.wav", "sapi_cat.wav", "sapi_red_apple.wav",
          "pregen_apple_normal.wav", "stress_silence.wav", "stress_noise.wav"]:
    p = r"Research\Speech\Phase1_1\audio\\" + f
    w = wave.open(p, "rb")
    r = KaldiRecognizer(m, 16000)
    s = time.time()
    r.AcceptWaveform(w.readframes(w.getnframes()))
    out = json.loads(r.FinalResult())
    print("FILE", f, "TEXT=[" + out.get("text", "") + "]", f"T={time.time()-s:.2f}s", flush=True)
