# LEGAL QUESTIONS FOR COUNSEL — WP-1.9.26

Send this list to a qualified legal reviewer (IP/data licensing). Context: Little World English is
a commercial children's English-learning product; the speech research lab wants to train a
research model (B2) and potentially ship derived model weights.

1. **CC-BY-ND (SIAK)**: Does training a model on CC-BY-ND audio and distributing the derived model
   weights (or using them in a commercial product) constitute distributing "adapted material"
   prohibited by the ND clause? Is there a defensible TDM (text-and-data-mining) position?
2. **CC BY-NC (OCSC paper; PERCEPT-R; MyST research license)**: May these corpora be used to train
   a model whose weights are later used in a commercial product, if the research use itself is
   non-commercial? What separation (research artifacts vs product artifacts) is required?
3. **MyST commercial license**: confirm the license grants (a) model training, (b) derived-weight
   distribution, (c) embedding in a commercial app, (d) creation of derived phone-level
   annotations, and (e) whether redistribution of the corpus to contractors is allowed.
4. **TalkBank/CHILDES terms**: do they permit training and derived-weight distribution? What
   attribution wording satisfies the citation rule for OCSC/CAPIL/Providence?
5. **LDC agreements (CSLU Kids, CMU Kids)**: can a research-only license be used to train a model
   that is later commercialized? Is the OHSU commercial route needed before training or before
   deployment?
6. **JIBO Kids**: the public repository has no LICENSE file — what rights are implied by public
   availability? Is author confirmation required for research/commercial use?
7. **AusKidTalk**: what are the data access terms, and do they permit derived weights for a
   commercial product?
8. **Derived annotations**: if we create phone-level annotations of a licensed corpus, who owns
   them and can they be redistributed?
9. **Child data / privacy**: for a prospective Vietnamese-L1 collection, what consent/assent,
   storage, and retention terms are required for research and for commercial model training?
10. **Outputs**: if a model trained on restricted data is used only internally (no weight
    distribution, no corpus redistribution), does that change the analysis?

Until answers are received: SIAK = REQUIRES_LEGAL_REVIEW; MyST = commercial license needed;
PERCEPT/OCSC/CAPIL/Providence = research-only; JIBO = LICENSE_UNVERIFIED.
