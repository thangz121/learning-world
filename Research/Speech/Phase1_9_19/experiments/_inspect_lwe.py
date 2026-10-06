import csv
from collections import defaultdict

rows = list(csv.DictReader(open('Research/Speech/Phase1_9_19/artifacts/window_rows_lwe.csv', encoding='utf-8')))
by = defaultdict(list)
for r in rows:
    by[r['token_id']].append(r)

st = {r['token_id']: r for r in csv.DictReader(open('Research/Speech/Phase1_9_19/WINDOW_STABILITY.csv', encoding='utf-8'))}
known = {'child_07_one', 'child_01_nine', 'child_01_four', 'child_03_four', 'child_06_four',
         'child_02_four', 'child_07_seven', 'child_01_seven', 'child_01_ten', 'child_02_ten',
         'child_04_four', 'child_06_six'}
print('token                 hp  cause        full   raw    max    min    span_full span_raw  gained_windows')
for tid in sorted(by):
    rs = by[tid]
    hp = rs[0]['human_present']
    if hp == '' and tid not in known:
        continue
    ev = {r['window_type']: float(r['deletion_aware_evidence']) for r in rs}
    sp = {r['window_type']: float(r['span_post']) for r in rs}
    gained = [k for k, v in ev.items() if v >= 0.30]
    print('%-20s %-3s %-12s %-6.3f %-6.3f %-6.3f %-6.3f %-9.4f %-8.4f %s' % (
        tid, hp, st[tid]['root_cause'], ev.get('full', 0), ev.get('raw', 0),
        max(ev.values()), min(ev.values()), sp.get('full', 0), sp.get('raw', 0),
        ','.join(gained)))
