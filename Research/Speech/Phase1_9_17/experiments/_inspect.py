import csv
from pathlib import Path

rows = list(csv.DictReader(open('Research/Speech/Phase1_9_17/artifacts/case_evidence.csv', encoding='utf-8')))
sel = [r for r in rows if r['human_present'] != '' or r['group']]
print('label  case                word    fin  hp  base  evid   soft   span_max da_max da_margin greed  verdict')
for r in sel:
    print(' '.join([
        f"{r['human_final_label'].replace('FINAL_CONSONANT_','')[:6]:6s}",
        f"{r['case_id']:20s}",
        f"{r['word']:7s}",
        f"{r['final_arpabet']:4s}",
        f"{r['human_present']:2s}",
        f"{r['baseline_final_match']:6s}",
        f"{r['evidence_class']:6s}",
        f"{r['baseline_soft']:>6s}",
        f"{r['span_max_post']:>8s}",
        f"{r['da_span_max_post']:>6s}",
        f"{r['da_margin_per_frame']:>9s}",
        f"{r['greedy_has_final']:>5s}",
        f"{r['human_verdict']}",
    ]))
