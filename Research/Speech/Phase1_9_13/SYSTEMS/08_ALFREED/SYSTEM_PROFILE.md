# Alignment-Free line — System Profile

| Item | Value | Evidence |
|---|---|---|
| Named "ALFreeD" | NOT FOUND | E1 (search) |
| GOP-AF/GOP-SA | arXiv 2507.16838 | E2 |
| VoxTutor | github.com/ranafaraz/VoxTutor, MIT | E3/E4 |
| GOP-AF idea | compute GOP over full observation sequence + context, no committed segmentation; includes deletion/insertion | E2 |
| GOP-AF evaluation | CMU Kids + SpeechOcean762; SOTA phoneme-level MDD | E2 |
| VoxTutor design | synthetic learner utterances from phoneme templates; known injected mispronunciations; AUROC vs known truth | E3/E4 |
| VoxTutor result | forced alignment survives rate warp; GOP normalization survives noise; need both | E4 (reproduced) |
| Child relevance | CMU Kids used in GOP-AF paper (children) | E2 |
| Runnable here | VoxTutor yes; GOP-AF no public code found | E4/E1 |
