import time
import torch, torchaudio
from speechbrain.inference import EncoderDecoderASR
t0 = time.time()
asr = EncoderDecoderASR.from_hparams(
    source=r"D:\speech-lab\models\sb-asr",
    savedir=r"D:\speech-lab\models\sb-asr",
    run_opts={"device": "cpu"})
print(f"LOAD_S {time.time()-t0:.1f}", flush=True)
for f in ["sapi_red.wav", "sapi_red_apple.wav", "pregen_apple_normal.wav", "stress_silence.wav"]:
    p = r"Research\Speech\Phase1_1\audio\\" + f
    s = time.time()
    txt = asr.transcribe_file(p)
    print("FILE", f, "TEXT=[" + txt.strip() + "]", f"T={time.time()-s:.2f}s", flush=True)
