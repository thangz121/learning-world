import time, sys, whisper
mp = sys.argv[1] if len(sys.argv) > 1 else "tiny"
files = sys.argv[2:]
t0 = time.time()
model = whisper.load_model(mp, download_root=r"D:\speech-lab\models")
print(f"LOAD_S {time.time()-t0:.1f}", flush=True)
for f in files:
    s = time.time()
    r = model.transcribe(f, language="en", fp16=False)
    dt = time.time() - s
    print(f"FILE {f} TEXT=[{r['text'].strip()}] LANG={r.get('language')} T={dt:.2f}s", flush=True)
