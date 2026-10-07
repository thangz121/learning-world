# LICENSE FORENSICS — WP-1.9.26 Part 9

> **Tóm tắt (VI):** Đọc điều khoản thật (không chỉ trích dẫn bài báo): OpenSLR/CC BY 4.0
> (speechocean762), Zenodo CC BY 4.0 (LWE), LDC agreements (MyST, CSLU, CMU), PhonBank
> non-commercial (PERCEPT/Providence), TalkBank research terms (OCSC/CAPIL), Radboud CC BY-ND
> definition (SIAK). Phân biệt rõ DATA LICENSE vs MODEL LICENSE. Không nâng cấp "paper statement"
> thành "license permission"; ô chưa xác minh ghi UNVERIFIED.

Method: license text/agreement pages fetched 2026-10-07; no dataset downloaded. The matrix
`04_DATASETS/DATASET_LICENSE_MATRIX.csv` answers the 12 questions per dataset.

## 1. Verified license facts

| dataset | license (verified source) | train model | derived weights | commercial | note |
|---|---|---|---|---|---|
| speechocean762 | CC BY 4.0 (openslr.org/101) | yes | yes | yes (free) | attribution |
| LWE Zenodo 200495 | CC BY 4.0 (Zenodo metadata) | yes | yes | yes | project rule: test-only |
| SIAK | CC-BY-ND-4.0 (local metadata; Radboud BY-ND definition) | unclear | unclear | unclear | ND forbids distributing adapted material |
| MyST | LDC MyST agreement; research CC BY-NC-SA; commercial via Boulder Learning | yes with license | yes with license | paid license | explicit commercial route |
| CSLU Kids/OGI | LDC CSLU corpora non-commercial research agreement; commercial via OHSU | research only | no | via OHSU (terms unverified) | |
| CMU Kids | CMU Kids organization/individual agreements | research only | no | no | |
| PERCEPT-R/GFTA | PhonBank noncommercial distribution (PMC12510240) | non-commercial | no | no | research-only |
| Providence | TalkBank/PhonBank terms | research | no | no | citation required |
| OCSC | TalkBank terms; paper CC BY-NC | research | no | no | free account |
| JIBO Kids | **no LICENSE file** on the public repo; Zenodo mirror | unverified | unverified | unverified | do not assume |
| AusKidTalk | project terms not fetched | unverified | unverified | unverified | request access terms |

## 2. Data license vs model license

- The frozen production model `facebook/wav2vec2-xlsr-53-espeak-cv-ft` and the alternative
  `facebook/wav2vec2-lv-60-espeak-cv-ft` are both **apache-2.0** (verified via HF model_info in the
  local WP-1.1 record). Model license is independent of corpus licenses.
- Training on CC-BY-ND data (SIAK) and distributing derived weights is the open question; training
  on CC BY-NC data (MyST research / PERCEPT-R) and shipping a commercial model is **not** covered by
  the research license — the paid commercial license is required.
- CC BY-SA-style share-alike (none of the main candidates) would propagate to derived weights;
  no candidate here uses BY-SA except MyST's research license (BY-NC-SA) which does not apply to the
  commercial track.

## 3. Statuses

CLEAR: speechocean762. CLEAR_WITH_CONDITIONS: LWE (test-only), MyST (paid commercial).
REQUIRES_LEGAL_REVIEW: SIAK, AusKidTalk. RESTRICTED: CSLU, CMU, PERCEPT, Providence, OCSC, CAPIL.
LICENSE_UNVERIFIED: JIBO, Storiza, PF-STAR, TBALL, CID, UltraSuite, SingaKids, CFSC, NCTE.
NOT_SUITABLE: Vietnamese research/adult corpora.

## 4. Rules applied

- A public download is not a license; JIBO has no LICENSE file → LICENSE_UNVERIFIED.
- A paper statement (e.g., "publicly available") is not a license; OCSC/AusKidTalk statuses come
  from the repository terms pages.
- A research-only access is not commercial permission.
