# Test corpus (every candidate, same conditions)

- A: clean adult English
- B: short one-word commands (apple/red/blue/cat/dog/book/big/small/open/close)
- C: short phrases (red apple, blue ball, open the door, I want apple, This is a cat)
- D: simple preschool sentences
- E: quiet / soft voice
- F: fast speech
- G: slow speech
- H: hesitant speech
- I: incomplete speech
- J: background noise
- K: wrong word
- L: near-miss pronunciation
- M: mixed-language contamination (Vietnamese-accented English where relevant)
- N: silence / no speech
- O: repeated attempt

Plus: LWE vocab words with V1 phoneme data (ball/apple/red/one/please/teddy),
real microphone-path captures (PC mic + phone gateway), and failure cases.
Audio manifest lives under `Research/Speech/Phase1_1/audio_manifest/` (paths +
hashes + labels only — no raw voice committed without explicit order).
