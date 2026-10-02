import time
import torch, soundfile as sf
from transformers import Wav2Vec2ForCTC, Wav2Vec2CTCTokenizer, Wav2Vec2FeatureExtractor, Wav2Vec2Processor
t0 = time.time()
pid = "kgnlp/allophant"
tok = Wav2Vec2CTCTokenizer.from_pretrained(pid, cache_dir=r"D:\speech-lab\models")
feat = Wav2Vec2FeatureExtractor(feature_size=1, sampling_rate=16000, padding_value=0.0,
                                do_normalize=True, return_attention_mask=False)
proc = Wav2Vec2Processor(feature_extractor=feat, tokenizer=tok)
model = Wav2Vec2ForCTC.from_pretrained(pid, cache_dir=r"D:\speech-lab\models").eval()
print(f"LOAD_S {time.time()-t0:.1f}", flush=True)
for f in ["sapi_red.wav", "sapi_cat.wav", "pregen_apple_normal.wav", "sapi_red_apple.wav",
          "stress_silence.wav"]:
    p = r"Research\Speech\Phase1_1\audio\\" + f
    audio, sr = sf.read(p)
    assert sr == 16000
    s = time.time()
    with torch.no_grad():
        logits = model(torch.tensor(audio).unsqueeze(0).float()).logits
    pred = torch.argmax(logits, dim=-1)[0].tolist()
    toks = proc.tokenizer.convert_ids_to_tokens(pred)
    # collapse CTC repeats + blanks
    out, prev = [], None
    for t in toks:
        if t != prev and t != proc.tokenizer.pad_token:
            out.append(t)
        prev = t
    print("FILE", f, "PHON=" + " ".join(out), f"T={time.time()-s:.2f}s", flush=True)
