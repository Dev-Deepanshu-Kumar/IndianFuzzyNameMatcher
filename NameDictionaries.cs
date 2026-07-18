namespace PanNameValidator;

// ─────────────────────────────────────────────────────────────────────────────
//  NameDictionaries
//
//  Central place for all name normalisation data.
//  Add new entries here — NameValidator picks them up automatically.
//
//  ABBREVIATION EXPANSIONS
//    Key   = short form that appears in a name
//    Value = canonical full form to expand it to before comparison
//    Only expanded when the other name actually contains the full form.
//
//  TITLES / SALUTATIONS / SUFFIXES
//    Stripped from the start and end of names before comparison.
//    Common across Hindi, Urdu, Tamil, Bengali, Marathi naming conventions.
// ─────────────────────────────────────────────────────────────────────────────

public static class NameDictionaries
{
    // ── Abbreviation expansions ───────────────────────────────────────────────
    // Format: shortened token → canonical expansion
    // These are only applied when the opposing name contains the full form,
    // so expanding "MD" to "MOHAMMAD" only fires if the other name has "MOHAMMAD".

    // ── Abbreviation expansions ───────────────────────────────────────────────
    //
    // ONLY culturally-specific contractions of a single word belong here.
    // Do NOT add multi-word initials like AK=ANIL KUMAR or SK=SANTOSH KUMAR —
    // the permutation algorithm in NameValidator already generates all first-letter
    // combinations automatically. Adding them here creates false positives.
    //
    // Rule of thumb: if it can be derived by taking first letters of a name,
    // it does NOT belong here.

    public static readonly IReadOnlyDictionary<string, string> Expansions =
        new Dictionary<string, string>
        {
            // ── Islamic / Urdu — common contractions of MOHAMMAD ─────────────
            // These are cultural shortenings, not derivable by any initial rule.
            ["MD"]       = "MOHAMMAD",
            ["MOHD"]     = "MOHAMMAD",
            ["MOHAMAD"]  = "MOHAMMAD",
            ["MUHAMMED"] = "MOHAMMAD",
            ["MOHAMMED"] = "MOHAMMAD",
            ["MUHAMAD"]  = "MOHAMMAD",
            ["MAHAMMAD"] = "MOHAMMAD",
            ["MAHAMAD"]  = "MOHAMMAD",

            // ── Hindi / North Indian — regional word shortenings ──────────────
            ["KR"]       = "KUMAR",       // KR DEEPANSHU → KUMAR DEEPANSHU
            ["KRI"]      = "KUMARI",
            ["KM"]       = "KUMARI",
            ["KU"]       = "KUMARI",
            ["PT"]       = "PANDIT",
            ["PD"]       = "PRASAD",
            ["PRS"]      = "PRASAD",
            ["LAL"]      = "LAL",         // keep — prevents accidental title-strip
            ["RAM"]      = "RAM",

            // ── Bengali / Odia surname particles ─────────────────────────────
            // Not titles, not initials — keeping them prevents false stripping.
            ["DAS"]      = "DAS",
            ["DEY"]      = "DEY",
            ["ROY"]      = "ROY",
            ["SEN"]      = "SEN",
            ["GHOSH"]    = "GHOSH",
            ["BOSE"]     = "BOSE",
            ["MITRA"]    = "MITRA",
            ["NATH"]     = "NATH",
            ["PAUL"]     = "PAUL",
        };

    // ── Titles, salutations, and suffixes ─────────────────────────────────────
    // Stripped from the beginning and end of a name before comparison.
    // Covers common Indian, English, and legal/professional prefixes.

    public static readonly IReadOnlySet<string> Titles = new HashSet<string>
    {
        // ── English ───────────────────────────────────────────────────────────
        "DR", "MR", "MRS", "MS", "MISS", "MASTER", "JR", "SR",

        // ── Hindi / Sanskrit ──────────────────────────────────────────────────
        "SHRI", "SHRIMATI", "SHRIMAN",
        "SMT",              // Srimati
        "KU",               // Kumari
        "KUMARI",
        "KUMAR",

        // ── Islamic ───────────────────────────────────────────────────────────
        "SYED", "SAYYID",
        "SHEIKH", "SHAIKH", "SHAIK",
        "MAULANA", "MAULVI", "MUFTI",
        "HAJI", "HAJJ",
        "AL", "BIN", "BINT", "IBN",

        // ── South Indian ──────────────────────────────────────────────────────
        "THIRU",            // Tamil equivalent of Shri
        "THIRUMATHI",       // Tamil equivalent of Shrimati
        "SELVI",            // Tamil Miss

        // ── Professional / legal ──────────────────────────────────────────────
        "ADV", "ADVOCATE",
        "CA", "CS",
        "ENG", "ENGR",
        "PROF", "PROFESSOR",
        "SIR", "DAME",

        // ── Political ─────────────────────────────────────────────────────────
        "MLA", "MP", "IAS", "IPS", "IFS",

        // ── Religious ─────────────────────────────────────────────────────────
        "SWAMI", "PANDIT", "PUJARI",
        "FATHER", "SISTER", "BROTHER",
        "PASTOR", "BISHOP",

        // ── Suffixes (appear at end of name) ──────────────────────────────────
        "JI",               // respectful suffix — Ramji → Ram
        "BHAI",             // brother suffix
        "DEVI",             // goddess suffix common in women's names
        "BAI",              // regional women's suffix (Rajasthan, MP)
    };
}
