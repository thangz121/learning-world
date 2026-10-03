---
language:
- en
license: apache-2.0
size_categories:
- 1K<n<10K
task_categories:
- automatic-speech-recognition
pretty_name: speechocean762
tags:
- pronunciation-scoring
- arxiv:2104.01378
configs:
- config_name: default
  data_files:
  - split: train
    path: data/train-*
  - split: test
    path: data/test-*
dataset_info:
  features:
  - name: accuracy
    dtype: int64
  - name: completeness
    dtype: float64
  - name: fluency
    dtype: int64
  - name: prosodic
    dtype: int64
  - name: text
    dtype: string
  - name: total
    dtype: int64
  - name: words
    list:
    - name: accuracy
      dtype: int64
    - name: phones
      sequence: string
    - name: phones-accuracy
      sequence: float64
    - name: stress
      dtype: int64
    - name: text
      dtype: string
    - name: total
      dtype: int64
    - name: mispronunciations
      list:
      - name: canonical-phone
        dtype: string
      - name: index
        dtype: int64
      - name: pronounced-phone
        dtype: string
  - name: speaker
    dtype: string
  - name: gender
    dtype: string
  - name: age
    dtype: int64
  - name: audio
    dtype: audio
  splits:
  - name: train
    num_bytes: 333140374
    num_examples: 2500
  - name: test
    num_bytes: 311843439
    num_examples: 2500
  download_size: 644387157
  dataset_size: 644983813
---
# speechocean762: A non-native English corpus for pronunciation scoring task

## Introduction
Pronunciation scoring is a crucial technology in computer-assisted language learning (CALL) systems. The pronunciation quality scores might be given at phoneme-level, word-level, and sentence-level for a typical pronunciation scoring task.

This corpus aims to provide a free public dataset for the pronunciation scoring task.
Key features:
* It is available for free download for both commercial and non-commercial purposes.
* The speaker variety encompasses young children and adults.
* The manual annotations are in multiple aspects at sentence-level, word-level and phoneme-level.

This corpus consists of 5000 English sentences. All the speakers are non-native, and their mother tongue is Mandarin. Half of the speakers are Children, and the others are adults. The information of age and gender are provided.

Five experts made the scores. To avoid subjective bias, each expert scores independently under the same metric.

## Uses
```python
>>> from datasets import load_dataset

>>> test_set = load_dataset("mispeech/speechocean762", split="test")

>>> len(test_set)
2500

>>> next(iter(test_set))
{'accuracy': 9,
 'completeness': 10.0,
 'fluency': 9,
 'prosodic': 9,
 'text': 'MARK IS GOING TO SEE ELEPHANT',
 'total': 9,
 'words': [{'accuracy': 10,
   'phones': ['M', 'AA0', 'R', 'K'],
   'phones-accuracy': [2.0, 2.0, 1.8, 2.0],
   'stress': 10,
   'text': 'MARK',
   'total': 10,
   'mispronunciations': []},
  {'accuracy': 10,
   'phones': ['IH0', 'Z'],
   'phones-accuracy': [2.0, 1.8],
   'stress': 10,
   'text': 'IS',
   'total': 10,
   'mispronunciations': []},
  {'accuracy': 10,
   'phones': ['G', 'OW0', 'IH0', 'NG'],
   'phones-accuracy': [2.0, 2.0, 2.0, 2.0],
   'stress': 10,
   'text': 'GOING',
   'total': 10,
   'mispronunciations': []},
  {'accuracy': 10,
   'phones': ['T', 'UW0'],
   'phones-accuracy': [2.0, 2.0],
   'stress': 10,
   'text': 'TO',
   'total': 10,
   'mispronunciations': []},
  {'accuracy': 10,
   'phones': ['S', 'IY0'],
   'phones-accuracy': [2.0, 2.0],
   'stress': 10,
   'text': 'SEE',
   'total': 10,
   'mispronunciations': []},
  {'accuracy': 10,
   'phones': ['EH1', 'L', 'IH0', 'F', 'AH0', 'N', 'T'],
   'phones-accuracy': [2.0, 2.0, 2.0, 2.0, 2.0, 2.0, 2.0],
   'stress': 10,
   'text': 'ELEPHANT',
   'total': 10,
   'mispronunciations': []}],
 'speaker': '0003',
 'gender': 'm',
 'age': 6,
 'audio': {'path': '000030012.wav',
  'array': array([-0.00119019, -0.00500488, -0.00283813, ...,  0.00274658,
          0.        ,  0.00125122]),
  'sampling_rate': 16000}}
```

## The scoring metric
The experts score at three levels: phoneme-level, word-level, and sentence-level.

### Sentence level
Score the accuracy, fluency, completeness and prosodic at the sentence level.

#### Accuracy
Score range: 0 - 10
* 9-10: The overall pronunciation of the sentence is excellent, with accurate phonology and no obvious pronunciation mistakes
* 7-8: The overall pronunciation of the sentence is good, with a few pronunciation mistakes
* 5-6: The overall pronunciation of the sentence is understandable, with many pronunciation mistakes and accent, but it does not affect the understanding of basic meanings
* 3-4: Poor, clumsy and rigid pronunciation of the sentence as a whole, with serious pronunciation mistakes
* 0-2: Extremely poor pronunciation and only one or two words are recognizable

#### Completeness
Score range: 0.0 - 1.0
The percentage of the words with good pronunciation.

#### Fluency
Score range: 0 - 10
* 8-10: Fluent without noticeable pauses or stammering
* 6-7: Fluent in general, with a few pauses, repetition, and stammering
* 4-5: the speech is a little influent, with many pauses, repetition, and stammering
* 0-3: intermittent, very influent speech, with lots of pauses, repetition, and stammering

#### Prosodic
Score range: 0 - 10
* 9-10: Correct intonation at a stable speaking speed, speak with cadence, and can speak like a native
* 7-8:  Nearly correct intonation at a stable speaking speed, nearly smooth and coherent, but with little stammering and few pauses
* 5-6: Unstable speech speed, many stammering and pauses with a poor sense of rhythm
* 3-4: Unstable speech speed, speak too fast or too slow, without the sense of rhythm
* 0-2: Poor intonation and lots of stammering and pauses, unable to read a complete sentence

### Word level
Score the accuracy and stress of each word's pronunciation.

#### Accuracy
Score range: 0 - 10
* 10: The pronunciation of the word is perfect
* 7-9: Most phones in this word are pronounced correctly but have accents
* 4-6: Less than 30% of phones in this word are wrongly pronounced
* 2-3: More than 30% of phones in this word are wrongly pronounced. In another case, the word is mispronounced as some other word. For example, the student mispronounced the word "bag" as "bike"
* 1: The pronunciation is hard to distinguish
* 0: no voice

#### Stress
Score range: {5, 10}
* 10: The stress is correct, or this is a mono-syllable word
* 5: The stress is wrong

### Phoneme level
Score the pronunciation goodness of each phoneme within the words.

Score range: 0-2
* 2: pronunciation is correct
* 1: pronunciation is right but has a heavy accent
* 0: pronunciation is incorrect or missed

For the phones with an accuracy score lower than 0.5, an extra "mispronunciations" indicates which is the most likely phoneme that the current phone was actually pronounced.
An example:

```json
{
    "text": "LISA",
    "accuracy": 5,
    "phones": ["L", "IY1", "S", "AH0"],
    "phones-accuracy": [0.4, 2, 2, 1.2],
    "mispronunciations": [
        {
            "canonical-phone": "L",
            "index": 0,
            "pronounced-phone": "D"
        }
    ],
    "stress": 10,
    "total": 6
}
```

## Citation
Please cite our paper if you find this work useful:

```bibtext
@inproceedings{speechocean762,
  title={speechocean762: An Open-Source Non-native English Speech Corpus For Pronunciation Assessment},
  booktitle={Proc. Interspeech 2021},
  year=2021,
  author={Junbo Zhang, Zhiwen Zhang, Yongqing Wang, Zhiyong Yan, Qiong Song, Yukai Huang, Ke Li, Daniel Povey, Yujun Wang}
}
```