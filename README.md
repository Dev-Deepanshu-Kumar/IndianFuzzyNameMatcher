# PAN Name Validator

A weighted string-similarity engine for matching Indian names — built to solve a real identity-verification problem in a digital lending platform.

---

## Background

In digital lending, applicants submit their PAN card details as part of identity verification. The challenge: ensuring the name entered by the applicant actually matches the name registered on the PAN card.

This sounds straightforward but Indian names are surprisingly varied in how they are spelled and written:

- Transliteration variants — `Deepanshu` / `Dipanshu`, `Mohammad` / `Mohd` / `MD`
- Aspirated consonants — `Dheeraj` / `Diraj`, `Thakur` / `Takur`
- Long vowels — `Susheel` / `Sushil`, `Sooraj` / `Suraj`
- Word order — `Deepanshu Kumar` / `Kumar Deepanshu`
- Abbreviations — `D Kumar`, `DK`
- Titles and salutations — `Dr.`, `Mr.`, `Shri`, `Smt.`

At the time, a third-party API was being called on every form submission to score name similarity. While it worked, every call had an associated cost, and under heavy marketing campaigns that cost scaled proportionally.

The observation was that real-world name variations follow predictable linguistic patterns, and a well-tuned combination of string-similarity algorithms — calibrated specifically for Indian names — could handle the majority of cases locally, at zero marginal cost per call.

---

## Approach

Six complementary algorithms are run in parallel, each catching a different class of variation:

| Algorithm | What it catches |
|---|---|
| **Jaro-Winkler** | Transpositions, missing characters, shared prefix bonus |
| **Indian Phonetic** | Transliteration variants (`ee→i`, `dh→d`, aspirated consonants, gemination) |
| **Damerau-Levenshtein** | Edit distance with transposition support, normalised to 0–1 |
| **N-Gram (bigram)** | Partial character overlap, insertions |
| **Cosine (bigram)** | Character frequency similarity, order-independent |
| **Jaccard** | Word-level overlap for multi-token names; bigram Jaccard for single tokens |

A weighted composite score is computed. Scores above the threshold are a match. The weights favour position-aware algorithms (Jaro-Winkler and edit distance) which handle transpositions correctly — bigram methods are down-weighted because they penalise transposed characters harshly even when the actual edit distance is 1.

Before fuzzy scoring, deterministic rules handle clear cases:
- **Exact match** — skip fuzzy entirely
- **Reversed word order** — `Kumar Deepanshu` == `Deepanshu Kumar`
- **Abbreviations** — `D Kumar`, `DK`, `Deepanshu K` are all detected
- **Common-word isolation** — for two-token names sharing one word, only the differing token is scored

The solution was validated against 30,000+ historical records, reviewed with the architecture team for weight tuning, and approved by the risk management team before production deployment.

A fallback band was maintained: scores near the threshold boundary continued to call the external API as a safety net, ensuring edge cases were not rejected solely based on the local algorithm.

---

## Configuration

Edit `config.json` to change defaults without recompiling:

```json
{
  "threshold": 72,
  "showAlgoBreakdown": true
}
```

Or pass CLI arguments at runtime:

```bash
dotnet run -- --threshold 75
dotnet run -- --threshold 80 --no-breakdown
```

**Threshold guide:**
- `65–70` — lenient, good for datasets with high transliteration variance
- `72` — default, balanced
- `75–80` — stricter, fewer false positives
- `85+` — very strict, near-identical names only

---

## Running

```bash
cd PanNameValidator
dotnet run
```

Requires **.NET 8 SDK**. No external NuGet dependencies.

---

## Project Structure

```
PanNameValidator/
├── Program.cs            Entry point — loads config, starts UI
├── ConsoleUI.cs          Interactive REPL — prompts, colours, bar charts
├── NameValidator.cs      Core matching logic and all similarity algorithms
├── ValidatorConfig.cs    Config loader — defaults → config.json → CLI args
├── config.json           Runtime configuration (copied to output dir on build)
├── config.json.example   Documented reference for available config keys
└── README.md             This file
```
