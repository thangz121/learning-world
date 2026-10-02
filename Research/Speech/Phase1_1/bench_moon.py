import time, wave, struct
import sherpa_onnx
d = r"D:\speech-lab\models\sherpa-onnx-moonshine-tiny-en-int8"
t0 = time.time()
rec = sherpa_onnx.OfflineRecognizer.from_moonshine(
    preprocessor=d + "\\preprocess.onnx", encoder=d + "\\encode.int8.onnx",
    uncached_decoder=d + "\\uncached_decode.int8.onnx",
    cached_decoder=d + "\\cached_decode.int8.onnx", tokens=d + "\\tokens.txt",
    num_threads=4)
print(f"LOAD_S {time.time()-t0:.1f}", flush=True)
for f in ["sapi_red.wav", "sapi_blue.wav", "sapi_cat.wav", "sapi_red_apple.wav",
          "pregen_apple_normal.wav", "stress_silence.wav"]:
    p = r"Research\Speech\Phase1_1\audio\\" + f
    w = wave.open(p, "rb")
    n = w.getnframes()
    pcm = [x / 32768.0 for x in struct.unpack("<" + "h" * n, w.readframes(n))]
    s = time.time()
    st = rec.create_stream()
    st.accept_waveform(16000, pcm)
    rec.decode_stream(st)
    print("FILE", f, "TEXT=[" + st.result.text.strip() + "]", f"T={time.time()-s:.2f}s", flush=True)
