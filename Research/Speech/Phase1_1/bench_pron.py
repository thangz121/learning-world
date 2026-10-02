import time
import numpy as np, librosa
import torch, soundfile as sf
from transformers import Wav2Vec2Model, Wav2Vec2FeatureExtractor

def handcrafted(p):
    y, sr = librosa.load(p, sr=16000, mono=True)
    mfcc = librosa.feature.mfcc(y=y, sr=sr, n_mfcc=13)
    rms = librosa.feature.rms(y=y)[0]
    return dict(dur=round(float(librosa.get_duration(y=y, sr=sr)), 2),
                mfcc0_mean=round(float(mfcc[0].mean()), 3),
                rms_mean=round(float(rms.mean()), 4),
                zcr=round(float(librosa.feature.zero_crossing_rate(y)[0].mean()), 4))

files = ["sapi_red.wav", "sapi_cat.wav", "pregen_apple_normal.wav",
         "sapi_red_apple.wav", "stress_silence.wav", "stress_noise.wav"]
for f in files:
    p = r"Research\Speech\Phase1_1\audio\\" + f
    s = time.time()
    print("FEAT", f, handcrafted(p), f"T={time.time()-s:.2f}s", flush=True)

t0 = time.time()
fe = Wav2Vec2FeatureExtractor.from_pretrained("facebook/wav2vec2-base",
                                              cache_dir=r"D:\speech-lab\models")
model = Wav2Vec2Model.from_pretrained("facebook/wav2vec2-base",
                                      cache_dir=r"D:\speech-lab\models").eval()
print(f"W2V_LOAD_S {time.time()-t0:.1f}", flush=True)
for f in files:
    p = r"Research\Speech\Phase1_1\audio\\" + f
    audio, sr = sf.read(p)
    inp = fe(audio, sampling_rate=sr, return_tensors="pt")
    s = time.time()
    with torch.no_grad():
        out = model(**inp).last_hidden_state
    emb = out.mean(dim=1)[0]
    print("W2V", f, f"dim={emb.shape[0]}emb_mean={emb.mean():.4f}emb_std={emb.std():.4f}",
          f"T={time.time()-s:.2f}s", flush=True)
