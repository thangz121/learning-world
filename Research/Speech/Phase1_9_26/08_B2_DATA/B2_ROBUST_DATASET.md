# B2 ROBUST DATASET — WP-1.9.26 Part 17

> **Tóm tắt (VI):** B2-ROBUST = pilot + corpus nghiên cứu tuổi 4–7 (OCSC 303 speakers / JIBO 110)
> + PERCEPT-R cho /r/; 20–40h, 100+ speakers, nhãn human trên subset, license research-only.
> Đủ cho một nghiên cứu adaptation nghiêm túc (không thương mại) và kiểm định speaker/age
> generalization.

| property | B2-ROBUST |
|---|---|
| hours | 20–40 h usable child speech |
| speakers | 100+ (OCSC 303 ages 4–9 + JIBO 110 ages 4–7 + local 45) |
| ages | 4–9 (target 4–6 well covered) |
| phone counts | generated alignments for all; human labels on ≥300 target tokens |
| /r/ count | PERCEPT-R research benchmark + ≥50 labelled /r/ from local/OCSC/JIBO |
| labels | 200 PRESENT + 150 ABSENT listening labels (IDEAL set); generated alignments elsewhere |
| annotation confidence | HUMAN-LISTENING on the labelled subset; AUTOMATIC kept separate |
| license | TalkBank research terms (OCSC/CAPIL/Providence) + PhonBank (PERCEPT-R) + local; **non-commercial** |
| GPU | required for encoder adaptation; ASUS has no GPU (16 GB RAM, CPU-only) |
| expected use | research-grade adaptation study, speaker/age generalization, /r/ benchmark |

**Constraint:** this dataset cannot be shipped in a commercial product under the audited terms.
It is the right vehicle for a *research* B2 that establishes whether adaptation works; the
commercial path then requires MyST's paid license or a new collection.
