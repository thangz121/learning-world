# ADR-004 — Content-Driven + Active/Passive vocab

Decision: thêm từ = thêm JSON, không sửa code. Slice chỉ 15 Active học thật + 35 Passive trang trí.
Context: 50 từ cùng lúc làm Slice loãng, khó đo recall.
Alternatives: 50 từ đều Active (chứng minh scale nhưng playtest nhiễu), 5 từ (quá ít để thấy repetition).
Why: đo được “nhớ 15 từ + transfer không visual” thay vì “chạm 50 vật”. Passive promote lên Active chỉ bằng `"active": true`.
Consequences: schema bắt buộc `active`, `semantic.tags`, `speech.expectedForms+phonetic`. Lead check `VocabActive15_HasSemantic`.
