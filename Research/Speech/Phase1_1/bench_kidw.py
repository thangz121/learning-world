import time
import torch
from transformers import WhisperProcessor, WhisperForConditionalGeneration
import soundfile as sf
t0 = time.time()
pid = "SatwikDutta/kid-whisper-tiny-en-myst"
proc = WhisperProcessor.from_pretrained(pid, cache_dir=r"D:\speech-lab\models")
model = WhisperForConditionalGeneration.from_pretrained(pid, cache_dir=r"D:\speech-lab\models").eval()
print(f"LOAD_S {time.time()-t0:.1f}", flush=True)
for f in ["sapi_red.wav", "sapi_blue.wav", "sapi_cat.wav", "sapi_red_apple.wav",
          "pregen_apple_normal.wav", "stress_silence.wav"]:
    p = r"Research\Speech\Phase1_1\audio\\" + f
    audio, sr = sf.read(p)
    inp = proc(audio, sampling_rate=16000, return_tensors="pt").input_features
    s = time.time()
    with torch.no_grad():
        ids = model.generate(inp, language="en", task="transcribe")
    txt = proc.batch_decode(ids, skip_special_tokens=True)[0]
    print("FILE", f, "TEXT=[" + txt.strip() + "]", f"T={time.time()-s:.2f}s", flush=True)
