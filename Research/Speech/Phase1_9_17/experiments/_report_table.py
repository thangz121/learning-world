import csv

rows = list(csv.DictReader(open('Research/Speech/Phase1_9_17/FAILURE_CASE_ANALYSIS.csv', encoding='utf-8')))
keys = ('case_id', 'classification', 'final_arpabet', 'human_final_label', 'human_present',
        'baseline_final_match', 'baseline_soft', 'evidence_class', 'span_max_post',
        'da_span_max_post', 'da_margin_per_frame', 'window_full', 'window_raw')
print('== primary labeled failures ==')
for r in rows:
    if r['human_final_source'] == 'final_label' and r['classification'] not in ('OK',):
        print(' | '.join(str(r[k]) for k in keys))
print('== inferred (word verdict) ==')
for r in rows:
    if r['human_final_source'] not in ('final_label', '') and r['classification'] not in ('OK',):
        print(' | '.join(str(r[k]) for k in keys))
print('== unsupported acceptances (all 80) ==')
for r in rows:
    if r['unsupported_acceptance'] == '1':
        print(' | '.join(str(r[k]) for k in keys))
print('== assessability / human uncertain ==')
for r in rows:
    if r['classification'] in ('HUMAN_UNCERTAIN', 'ASSESSABILITY_FAILURE'):
        print(' | '.join(str(r[k]) for k in keys))
print('== aggregation labeled ==')
print(open('Research/Speech/Phase1_9_17/EXPERIMENT_RESULTS.json', encoding='utf-8').read()[:1] and '')
