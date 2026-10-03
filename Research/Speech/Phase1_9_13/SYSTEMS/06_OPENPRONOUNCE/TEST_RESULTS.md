# OpenPronounce — Test Results (E4/E6)

16 cases ran on the LWE corpus. Raw JSON: `RAW_OUTPUT/openpronounce/*.json`.

| test | target | condition | score | PER | errors | note |
|---|---|---|---:|---:|---:|---|
| adult_red | red | correct | 100.0 | 0.0 | 0 | |
| adult_blue | blue | correct | 23.2 | 0.67 | 1 | same word our scorer finds hard |
| adult_cat | cat | correct | 100.0 | 0.0 | 0 | |
| adult_apple | apple | correct | 53.3 | 1.0 | 1 | |
| adult_big | big | correct | 65.6 | 0.67 | 0 | |
| adult_book | book | correct | 25.5 | 0.67 | 1 | |
| adult_dog | dog | correct | 80.7 | 0.33 | 0 | |
| adult_red_apple | red apple | correct | 100.0 | 0.0 | 0 | |
| adult_red_vs_blue | blue | wrong word | 6.8 | 1.0 | 1 | correctly rejected |
| child_07_four | four | final /r/ absent (human) | 20.5 | 0.5 | 1 | detected |
| child_06_four | four | final /r/ absent (human) | 24.7 | 0.5 | 1 | detected |
| child_09_four | four | final /r/ absent (human) | 12.2 | 1.5 | 1 | detected |
| child_05_four | four | unlabeled | 3.1 | 1.0 | 1 | |
| child_07_one | one | final present (human) | **2.5** | 1.33 | 1 | **FALSE LOW** |
| child_02_eight | eight | final present (human) | 20.0 | 0.5 | 0 | low |
| silence_1s_vs_red | red | silence | 0.0 | 1.0 | 1 | correctly zero |

Conclusion: independent MIT system also fails on ~4yo speech (human-correct child token 2.5),
while detecting the final-/r/ deletions. No safe drop-in replacement; useful cross-check.
