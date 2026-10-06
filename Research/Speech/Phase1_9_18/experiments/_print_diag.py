import json

j = json.load(open('Research/Speech/Phase1_9_18/EXPERIMENT_RESULTS.json', encoding='utf-8'))
v2 = j['variants_v2']
for tag, v in v2['diagnostic_points'].items():
    print('===', tag, '(diagnostic only)')
    for ds in ('dev', 'test_speaker_disjoint', 'lwe_external'):
        m = v[ds]
        print('  %-22s n=%5d rec=%s afp=%s absdet=%s uncP=%s uncA=%s cov=%s' % (
            ds, m['n'], m['present_recall'], m['absent_false_present'],
            m['absent_decided'], m['uncertain_present_rate'],
            m['uncertain_absent_rate'], m['decision_coverage']))
print()
for name in ('A_baseline', 'B_delaware_free'):
    print('===', name)
    for ds in ('dev', 'test_speaker_disjoint', 'lwe_external'):
        m = v2[name][ds]
        print('  %-22s n=%5d rec=%s afp=%s absdet=%s cov=%s' % (
            ds, m['n'], m['present_recall'], m['absent_false_present'],
            m['absent_decided'], m['decision_coverage']))
