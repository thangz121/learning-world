$ErrorActionPreference = 'Stop'
$repo = 'D:\Vscode\little-world-english'
$models = 'D:\speech-lab\models'
$py = 'D:\speech-lab\venvs\p0\Scripts\python.exe'

Write-Host '[1/4] dirs'
New-Item -ItemType Directory -Force -Path $models, 'D:\speech-lab\data' | Out-Null

Write-Host '[2/4] cmudict'
Copy-Item "$repo\Research\Speech\Phase1_9_30\MIGRATION\assets\cmudict.dict" $models -Force

Write-Host '[3/4] HF models (lv-60 default cache; xlsr -> D:\speech-lab\models)'
& $py -c "from huggingface_hub import snapshot_download as d; d('facebook/wav2vec2-lv-60-espeak-cv-ft'); d('facebook/wav2vec2-xlsr-53-espeak-cv-ft', cache_dir=r'D:\speech-lab\models')"

Write-Host '[4/4] pilot validate'
& $py "$repo\Research\Speech\Phase1_9_27\experiments\pilot_runner.py" --stage validate

Write-Host 'SETUP DONE. Con thieu: tai audio theo MIGRATION_NEW_MACHINE.md muc 3.'
