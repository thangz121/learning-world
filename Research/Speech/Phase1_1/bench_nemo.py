import time
import nemo.collections.asr as nemo_asr
t0 = time.time()
m = nemo_asr.models.EncDecRNNTBPEModel.from_pretrained("nvidia/parakeet-tdt-0.6b-v2")
m.eval()
print(f"LOAD_S {time.time()-t0:.1f}", flush=True)
files = ["sapi_red.wav", "sapi_blue.wav", "sapi_cat.wav", "sapi_red_apple.wav",
         "pregen_apple_normal.wav", "stress_silence.wav"]
paths = [r"D:\Vscode\little-world-english\Research\Speech\Phase1_1\audio\\" + f for f in files]
s = time.time()
outs = m.transcribe(paths)
print(f"BATCH_T {time.time()-s:.2f}s", flush=True)
for f, o in zip(files, outs):
    print("FILE", f, "TEXT=[" + str(o).strip() + "]", flush=True)
