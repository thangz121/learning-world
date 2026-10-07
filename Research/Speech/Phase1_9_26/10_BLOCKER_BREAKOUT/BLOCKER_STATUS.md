# BLOCKER STATUS — WP-1.9.26 Part 31

> **Tóm tắt (VI):** Trạng thái từng blocker sau WP này: nhãn người (A) — **cơ chế đã sẵn sàng**,
> còn thiếu reviewer; /r/ (B) — cần 15+15 nhãn, có acoustic mới; TYPE-B (C) — 0/4 xác nhận, 4 nhãn
> quyết định; data-volume (D) — route nghiên cứu sẵn (OCSC/JIBO), route thương mại có phí (MyST);
> speaker-diversity (E) — giải được bằng OCSC/JIBO research; age-domain (F) — OCSC 4–9/JIBO 4–7 giải
> phần lớn (research); phone-label (G) — vẫn là blocker chính; license (H) — MyST paid/CC BY 4.0
> clear, còn lại research/ND; Việt-L1 (I) — không có corpus công khai; B2 readiness (J) — pilot sẵn
> sàng về mặt thiết kế, chờ nhãn.

| blocker | before WP-1.9.26 | after WP-1.9.26 | removal route | status |
|---|---|---|---|---|
| A human labels | no tooling, 23 candidates | validated blind pipeline + 276 candidates + protocol; 0 labels | recruit 2 reviewers (server ready) | **REMOVABLE (human action)** |
| B /r/ | 24 cases, no acoustics | 24 cases + F1/F2/F3/RMS/voiced; label requirements 15+15 | /r/ review + PERCEPT-R research benchmark | **PARTIALLY REMOVABLE** |
| C TYPE-B | 0/4, unresolved | 0/4; 3/4 representation-dependent; 4 labels in pack | 4 listening labels | **REMOVABLE (4 labels)** |
| D data volume | "no dataset 10–30h" | OCSC 303 spk 4–9, JIBO 110 spk 4–7, AusKidTalk 136h, MyST 470h (paid) | TalkBank registration / MyST license | **REMOVABLE (research) / PAID (commercial)** |
| E speaker diversity | 45 local speakers | OCSC 303 + JIBO 110 + AusKidTalk 620 (research) | registration | **REMOVABLE (research)** |
| F age domain | no 4–6 at scale | OCSC 4–9 (303), JIBO 4–7 (110), CAPIL 5–6 (30), LWE 4.9 | registration | **PARTIALLY REMOVABLE (research)** |
| G phone labels | none at scale | still none; generated+verified strategy defined | forced alignment + human verification | **NOT REMOVED (main gap)** |
| H license | child corpora blocked | MyST paid route verified; speechocean762 CC BY 4.0 clear; SIAK legal review open | license purchase / counsel | **PARTIALLY REMOVABLE** |
| I Vietnamese-L1 | no public corpus | confirmed no public corpus; 2 research datasets identified; collection protocol designed | partnership / local collection | **NOT REMOVED (long-term)** |
| J B2 readiness | NOT READY | pilot design frozen; success criteria frozen; encoder comparison done | execute pilot after labels | **PILOT-READY (design)** |

## Overall

The single dominant blocker is now **G (phone-level labels at scale)**, not raw child speech.
The cheapest immediate unblock is **A/C/B: the 276-candidate human review round** (no licensing),
followed by the local pilot.
