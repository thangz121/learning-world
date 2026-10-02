import time, wave, struct
import torch
from silero_vad import load_silero_vad, get_speech_timestamps
import sherpa_onnx

vad = load_silero_vad()
d = r"D:\speech-lab\models\sherpa-onnx-whisper-tiny.en"
rec = sherpa_onnx.OfflineRecognizer.from_whisper(
    encoder=d + "\\tiny.en-encoder.int8.onnx", decoder=d + "\\tiny.en-decoder.int8.onnx",
    tokens=d + "\\tiny.en-tokens.txt", language="en", task="transcribe", num_threads=4)

for f in ["sapi_red.wav", "sapi_red_apple.wav", "pregen_apple_normal.wav"]:
    p = r"Research\Speech\Phase1_1\audio\\" + f
    w = wave.open(p, "rb")
    n = w.getnframes()
    pcm = [x / 32768.0 for x in struct.unpack("<" + "h" * n, w.readframes(n))]
    audio = torch.tensor(pcm)
    s = time.time()
    ts = get_speech_timestamps(audio, vad, return_seconds=True)
    # pad 0.15s each side, merge, clip
    cuts = []
    for a in ts:
        a0, a1 = max(0.0, a["start"] - 0.15), min(len(pcm) / 16000, a["end"] + 0.15)
        cuts.append((int(a0 * 16000), int(a1 * 16000)))
    gated = []
    for a0, a1 in cuts:
        gated.extend(pcm[a0:a1])
    st = rec.create_stream()
    st.accept_waveform(16000, gated)
    rec.decode_stream(st)
    print("COMBO", f, "SEG=" + str([(round(a['start'], 2), round(a['end'], 2)) for a in ts]),
          "TEXT=[" + st.result.text.strip() + "]", f"T={time.time()-s:.2f}s", flush=True)
