import time, wave
import webrtcvad
vad = webrtcvad.Vad(3)
for f in ["sapi_red.wav", "sapi_red_apple.wav", "pregen_apple_normal.wav",
          "stress_silence.wav", "stress_noise.wav", "stress_tone.wav"]:
    p = r"Research\Speech\Phase1_1\audio\\" + f
    w = wave.open(p, "rb")
    raw = w.readframes(w.getnframes())
    fr = int(16000 * 0.03) * 2
    n = len(raw) // fr
    s = time.time()
    hits = sum(1 for i in range(n) if vad.is_speech(raw[i*fr:(i+1)*fr], 16000))
    print("FILE", f, f"SPEECH_FRAMES={hits}/{n}", f"T={time.time()-s:.3f}s", flush=True)
