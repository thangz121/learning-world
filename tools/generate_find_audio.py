import json
from pathlib import Path
import wave
import numpy as np
import soundfile as sf
import librosa
from piper import PiperVoice, SynthesisConfig

REPO = Path(__file__).resolve().parents[1]
SHIP = REPO / 'Assets' / 'StreamingAssets' / 'audio'
CONTENT_MANIFEST = REPO / 'Content' / 'audio_manifest.json'
MIRROR = SHIP / 'manifest.json'
PIPER_MODEL = Path(r'D:\speech-lab\models\piper\en_US-libritts_r-medium.onnx')
SPEAKER_ID = 100
TARGET_SR = 24000

words = [
    'chair', 'table', 'desk', 'books',
    'car', 'truck',
    'tree', 'rock', 'flower', 'mushroom',
    'apple', 'ball', 'banana', 'orange',
    'carrot', 'tomato', 'watermelon', 'grape'
]

voice = PiperVoice.load(str(PIPER_MODEL), config_path=str(PIPER_MODEL) + '.json')
cfg = SynthesisConfig(speaker_id=SPEAKER_ID, length_scale=1.0)

tmp_wav = REPO / 'tmp_synth.wav'
new_entries = []

for w in words:
    en_word = 'grapes' if w == 'grape' else w
    phrase = f'Find the {en_word}!'
    file_name = f'find_{w}.mp3'
    dst_mp3 = SHIP / file_name
    
    with wave.open(str(tmp_wav), 'wb') as wf:
        voice.synthesize_wav(phrase, wf, syn_config=cfg)
        
    x, sr = sf.read(str(tmp_wav), dtype='float32')
    if x.ndim > 1:
        x = x.mean(axis=1)
    pad = int(0.06 * sr)
    x = np.concatenate([np.zeros(pad, dtype='float32'), x, np.zeros(pad, dtype='float32')])
    peak = float(np.max(np.abs(x))) or 1.0
    x = x / peak * 0.75
    x24 = librosa.resample(x, orig_sr=sr, target_sr=TARGET_SR)
    sf.write(str(dst_mp3), x24, TARGET_SR, format='MP3')
    
    new_entries.append({
        'id': f'find_{w}',
        'text': phrase,
        'voice': 'listen_v1',
        'lang': 'en-US',
        'rate': 0.85,
        'pitch': 0.0,
        'style': 'Clear',
        'format': 'Mp3_44100',
        'file': file_name
    })
    print(f'Generated {file_name} for "{phrase}"')

if tmp_wav.exists():
    tmp_wav.unlink()

# Update manifests
if CONTENT_MANIFEST.exists():
    with open(CONTENT_MANIFEST, 'r', encoding='utf-8') as f:
        data = json.load(f)
    if isinstance(data, list):
        entries = [e for e in data if not e['id'].startswith('find_')]
        entries.extend(new_entries)
        with open(CONTENT_MANIFEST, 'w', encoding='utf-8') as f:
            json.dump(entries, f, indent=2, ensure_ascii=False)
        print(f'Updated {CONTENT_MANIFEST}')

if MIRROR.exists():
    with open(MIRROR, 'r', encoding='utf-8') as f:
        data = json.load(f)
    if isinstance(data, dict):
        entries = data.get('entries', [])
        entries = [e for e in entries if not e['id'].startswith('find_')]
        entries.extend(new_entries)
        data['entries'] = entries
        with open(MIRROR, 'w', encoding='utf-8') as f:
            json.dump(data, f, indent=2, ensure_ascii=False)
        print(f'Updated {MIRROR}')

print('All find_* audio successfully generated!')
