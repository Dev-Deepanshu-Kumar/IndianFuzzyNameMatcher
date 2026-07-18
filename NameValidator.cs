using System.Text.RegularExpressions;

namespace PanNameValidator;

// ─────────────────────────────────────────────────────────────────────────────
//  NameValidator
//  Core matching logic. Call ValidateName() with two raw name strings.
//
//  ValidateName() returns a MatchResult containing:
//    IsMatch       — true/false verdict at the configured threshold
//    Score         — 0.0–1.0 composite fuzzy score
//    SpecialReason — set when a deterministic rule matched (no fuzzy scoring)
//    AlgoScores    — individual scores for each similarity measure
//
//  Weights and threshold come from ValidatorConfig — nothing is hardcoded here.
//  To tune behaviour: edit config.json or see ValidatorConfig.cs for all options.
// ─────────────────────────────────────────────────────────────────────────────

public sealed class NameValidator
{
    private readonly ValidatorConfig _config;

    // Expansions and titles live in NameDictionaries.cs — edit there to add entries.

    // ── Constructor ──────────────────────────────────────────────────────────

    /// <param name="config">
    /// Pass a loaded ValidatorConfig to use config.json values and CLI overrides.
    /// Omit (or pass null) to use all defaults — useful in unit tests.
    /// </param>
    public NameValidator(ValidatorConfig? config = null)
    {
        _config = config ?? new ValidatorConfig();
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <param name="threshold">
    /// Override the threshold for this single call.
    /// If null, uses the value from ValidatorConfig (config.json / CLI arg / default).
    /// </param>
    public MatchResult ValidateName(string name1, string name2, double? threshold = null)
    {
        double effectiveThreshold = threshold ?? _config.Threshold;

        var n1 = Sanitize(name1);
        var n2 = Sanitize(name2);

        if (string.IsNullOrWhiteSpace(n1) || string.IsNullOrWhiteSpace(n2))
            return MatchResult.Special(false, "One or both names are empty.");

        // ── Deterministic checks (no fuzzy scoring needed) ────────────────────

        if (n1 == n2)
            return MatchResult.Special(true, "Exact match.");

        // Collect all normalizations applied to both names
        var notes = new List<string>();
        var s1    = NormalizeTokens(n1, notes);
        var s2    = NormalizeTokens(n2, notes);

        var words1 = DeduplicateConsecutive(s1.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        var words2 = DeduplicateConsecutive(s2.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        // Reversed word order: KUMAR DEEPANSHU == DEEPANSHU KUMAR
        if (words1.Length == words2.Length &&
            string.Concat(words1.Reverse()) == string.Concat(words2))
            return MatchResult.Special(true, "Name tokens match in reversed order.", notes);

        // Abbreviation: D KUMAR, DK, Deepanshu K, etc.
        string longer  = n1.Length >= n2.Length ? n1 : n2;
        string shorter = (n1.Length >= n2.Length ? n2 : n1).Replace(" ", "");

        if (GenerateAbbreviations(longer).Contains(shorter))
            return MatchResult.Special(true, "One name is an abbreviation of the other.", notes);

        if (GenerateAbbreviatedPermutations(longer).Contains(shorter))
            return MatchResult.Special(true, "One name is an abbreviated permutation of the other.", notes);

        // ── Common-word isolation for two-token names ─────────────────────────
        // DEEPANSHU KUMAR vs DIPANSHU KUMAR → score only DEEPANSHU vs DIPANSHU
        if (words1.Length == 2 && words2.Length == 2)
        {
            var common      = words1.FirstOrDefault(w => words2.Contains(w));
            bool hasInitial = words1.Any(w => w.Length == 1) || words2.Any(w => w.Length == 1);

            if (common != null && !hasInitial)
            {
                var r1 = string.Concat(words1.Where(w => w != common));
                var r2 = string.Concat(words2.Where(w => w != common));
                return ComputeFuzzyResult(r1, r2, effectiveThreshold, notes);
            }
        }

        return ComputeFuzzyResult(s1, s2, effectiveThreshold, notes);
    }

    // ── Fuzzy scoring ─────────────────────────────────────────────────────────

    private MatchResult ComputeFuzzyResult(string n1, string n2, double threshold, List<string>? notes = null)
    {
        var a = n1.Replace(" ", "");
        var b = n2.Replace(" ", "");

        double editScore     = NormalisedEditDistance(a, b);
        double ngramScore    = NgramSimilarity(a, b);
        double phoneticScore = PhoneticScore(a, b);
        double jaroScore     = JaroWinkler(a, b);
        double cosineScore   = CosineSimilarity(a, b);
        double jaccardScore  = JaccardSimilarity(n1, n2); // word-aware, spaced form

        // Composite — weights come from config, not hardcoded here
        double composite =
            editScore     * _config.WeightEditDistance +
            ngramScore    * _config.WeightNgram        +
            phoneticScore * _config.WeightPhonetic     +
            jaroScore     * _config.WeightJaroWinkler  +
            cosineScore   * _config.WeightCosine       +
            jaccardScore  * _config.WeightJaccard;

        var algos = new Dictionary<string, double>
        {
            ["Jaro-Winkler"]          = jaroScore,
            ["Indian Phonetic"]       = phoneticScore,
            ["N-Gram (bigram)"]       = ngramScore,
            ["Edit Distance (norm.)"] = editScore,
            ["Cosine (bigram)"]       = cosineScore,
            ["Jaccard"]               = jaccardScore,
        };

        return new MatchResult
        {
            IsMatch            = composite >= threshold,
            Score              = composite,
            Threshold          = threshold,
            SpecialReason      = null,
            AlgoScores         = algos,
            NormalizationNotes = notes ?? [],
        };
    }

    // ── String preprocessing ──────────────────────────────────────────────────

    private static string Sanitize(string name) =>
        Regex.Replace(name.ToUpperInvariant().Trim(), @"[^A-Z0-9 ]", "");

    private static string NormalizeTokens(string name, List<string> notes)
    {
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();

        // Expand known abbreviations (first and last token only)
        if (NameDictionaries.Expansions.TryGetValue(words[0], out var exp0) && exp0 != words[0])
        {
            notes.Add($"'{words[0]}' expanded to '{exp0}'");
            words[0] = exp0;
        }
        if (words.Count > 1 && NameDictionaries.Expansions.TryGetValue(words[^1], out var expLast) && expLast != words[^1])
        {
            notes.Add($"'{words[^1]}' expanded to '{expLast}'");
            words[^1] = expLast;
        }

        // Strip titles from start
        if (words.Count > 0 && NameDictionaries.Titles.Contains(words[0]))
        {
            notes.Add($"Title '{words[0]}' stripped");
            words.RemoveAt(0);
        }

        // Strip titles from end
        if (words.Count > 0 && NameDictionaries.Titles.Contains(words[^1]))
        {
            notes.Add($"Suffix '{words[^1]}' stripped");
            words.RemoveAt(words.Count - 1);
        }

        return string.Join(" ", words);
    }

    private static string[] DeduplicateConsecutive(string[] words) =>
        words.Aggregate(new List<string>(), (acc, w) =>
        {
            if (acc.Count == 0 || acc[^1] != w) acc.Add(w);
            return acc;
        }).ToArray();

    // ── Abbreviation generators ───────────────────────────────────────────────

    private static HashSet<string> GenerateAbbreviations(string name)
    {
        var tokens = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var result = new HashSet<string>();
        for (int r = 0; r <= tokens.Length; r++)
            foreach (var perm in Permutations(tokens, r))
                result.Add(string.Concat(perm));
        return result;
    }

    private static HashSet<string> GenerateAbbreviatedPermutations(string name)
    {
        var tokens = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var result = new HashSet<string>();

        for (int iter = 1; iter < tokens.Length; iter++)
            for (int i = 0; i <= tokens.Length - iter; i++)
            {
                var t = tokens.ToArray();
                for (int j = i; j < i + iter; j++)
                    t[j] = t[j].Length > 0 ? t[j][0].ToString() : "";

                foreach (var abbr in GenerateAbbreviations(string.Join(" ", t)))
                    result.Add(abbr);
            }

        return result;
    }

    private static IEnumerable<IEnumerable<T>> Permutations<T>(IEnumerable<T> elements, int r)
    {
        if (r == 0) { yield return []; yield break; }
        var arr = elements.ToArray();
        for (int i = 0; i < arr.Length; i++)
            foreach (var perm in Permutations(arr.Where((_, idx) => idx != i), r - 1))
                yield return arr[i..].Take(1).Concat(perm);
    }

    // ── Similarity algorithms ─────────────────────────────────────────────────

    private static double JaroWinkler(string s1, string s2)
    {
        if (s1 == s2) return 1.0;
        int l1 = s1.Length, l2 = s2.Length;
        if (l1 == 0 || l2 == 0) return 0.0;

        int window = Math.Max(Math.Max(l1, l2) / 2 - 1, 0);
        bool[] f1 = new bool[l1], f2 = new bool[l2];
        int matches = 0;

        for (int i = 0; i < l1; i++)
        {
            int lo = Math.Max(0, i - window), hi = Math.Min(i + window + 1, l2);
            for (int j = lo; j < hi; j++)
            {
                if (f2[j] || s1[i] != s2[j]) continue;
                f1[i] = f2[j] = true; matches++; break;
            }
        }

        if (matches == 0) return 0.0;

        int t = 0, k = 0;
        for (int i = 0; i < l1; i++)
        {
            if (!f1[i]) continue;
            while (!f2[k]) k++;
            if (s1[i] != s2[k]) t++;
            k++;
        }

        double jaro    = (matches / (double)l1 + matches / (double)l2 + (matches - t / 2.0) / matches) / 3.0;
        int    prefix  = 0;
        for (int i = 0; i < Math.Min(4, Math.Min(l1, l2)); i++)
        {
            if (s1[i] == s2[i]) prefix++; else break;
        }
        return jaro + prefix * 0.1 * (1 - jaro);
    }

    private static double NormalisedEditDistance(string s1, string s2)
    {
        int l1 = s1.Length, l2 = s2.Length;
        int[,] d = new int[l1 + 1, l2 + 1];
        for (int i = 0; i <= l1; i++) d[i, 0] = i;
        for (int j = 0; j <= l2; j++) d[0, j] = j;

        for (int i = 1; i <= l1; i++)
            for (int j = 1; j <= l2; j++)
            {
                int cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                if (i > 1 && j > 1 && s1[i - 1] == s2[j - 2] && s1[i - 2] == s2[j - 1])
                    d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + cost);
            }

        return 1.0 - (double)d[l1, l2] / Math.Max(l1, l2);
    }

    private static double NgramSimilarity(string s1, string s2, int n = 2)
    {
        if (s1 == s2) return 1.0;
        if (s1.Length < n || s2.Length < n) return 0.0;

        var g1     = GetNgrams(s1, n);
        var g2     = GetNgrams(s2, n);
        var counts = new Dictionary<string, int>();
        foreach (var g in g2) counts[g] = counts.GetValueOrDefault(g) + 1;

        int shared = 0;
        foreach (var g in g1)
            if (counts.TryGetValue(g, out int c) && c > 0) { shared++; counts[g]--; }

        return 2.0 * shared / (g1.Count + g2.Count);
    }

    private static List<string> GetNgrams(string s, int n)
    {
        var result = new List<string>(Math.Max(0, s.Length - n + 1));
        for (int i = 0; i <= s.Length - n; i++)
            result.Add(s.Substring(i, n));
        return result;
    }

    private static double CosineSimilarity(string s1, string s2)
    {
        if (s1 == s2) return 1.0;
        if (s1.Length < 2 || s2.Length < 2) return 0.0;

        var v1 = BigramFrequency(s1);
        var v2 = BigramFrequency(s2);

        double dot = 0, mag1 = 0, mag2 = 0;
        foreach (var kv in v1) { dot += kv.Value * v2.GetValueOrDefault(kv.Key); mag1 += kv.Value * kv.Value; }
        foreach (var kv in v2) mag2 += kv.Value * kv.Value;

        return (mag1 == 0 || mag2 == 0) ? 0.0 : dot / (Math.Sqrt(mag1) * Math.Sqrt(mag2));
    }

    private static Dictionary<string, int> BigramFrequency(string s)
    {
        var v = new Dictionary<string, int>();
        for (int i = 0; i < s.Length - 1; i++)
        {
            var bg = s.Substring(i, 2);
            v[bg] = v.GetValueOrDefault(bg) + 1;
        }
        return v;
    }

    private static double JaccardSimilarity(string name1, string name2)
    {
        var words1 = name1.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var words2 = name2.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Multi-token: word-level Jaccard
        if (words1.Length > 1 || words2.Length > 1)
        {
            var set1 = new HashSet<string>(words1);
            var set2 = new HashSet<string>(words2);
            int intersection = set1.Intersect(set2).Count();
            int union        = set1.Count + set2.Count - intersection;
            return union == 0 ? 0.0 : (double)intersection / union;
        }

        // Single-token: character bigram Jaccard.
        // Word-level gives 0 for any two different single words regardless of similarity,
        // so bigram overlap is a better measure for closely-spelled single-token names.
        if (name1.Length < 2 || name2.Length < 2) return 0.0;

        var bg1          = new HashSet<string>(GetNgrams(name1, 2));
        var bg2          = new HashSet<string>(GetNgrams(name2, 2));
        int bgIntersect  = bg1.Intersect(bg2).Count();
        int bgUnion      = bg1.Count + bg2.Count - bgIntersect;
        return bgUnion == 0 ? 0.0 : (double)bgIntersect / bgUnion;
    }

    // ── Indian phonetic normalizer ────────────────────────────────────────────
    //
    // Collapses common transliteration variants so phonetically identical names
    // produce the same key regardless of spelling.
    //
    // Rules applied:
    //   Long vowels  : ee/ii → i,  aa → a,  oo/uu → u
    //   Aspirated    : dh/th/gh/kh/bh → d/t/g/k/b
    //   Variants     : ph → f,  w → v,  z → j
    //   Gemination   : doubled consonants → single  (rr → r, ss → s, etc.)
    //   Trailing 'a' : stripped  (Ram/Rama, Shiv/Shiva → same key)

    internal static string GetIndianPhoneticKey(string name)
    {
        string s = name.ToLower();
        s = s.Replace("ee", "i").Replace("ii", "i").Replace("ea", "i");
        s = s.Replace("aa", "a");
        s = s.Replace("oo", "u").Replace("uu", "u");
        s = s.Replace("dh", "d").Replace("gh", "g").Replace("kh", "k")
             .Replace("th", "t").Replace("bh", "b");
        s = s.Replace("ph", "f").Replace("w", "v").Replace("z", "j");
        s = Regex.Replace(s, @"c(?!h)", "k");
        s = Regex.Replace(s, @"(.)\1+", "$1");
        if (s.Length > 3 && s.EndsWith('a')) s = s[..^1];
        return s;
    }

    private static double PhoneticScore(string name1, string name2)
    {
        if (name1.Length <= 1 || name2.Length <= 1) return 0.0;
        return JaroWinkler(GetIndianPhoneticKey(name1), GetIndianPhoneticKey(name2));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
//  MatchResult — returned by ValidateName()
// ─────────────────────────────────────────────────────────────────────────────

public sealed class MatchResult
{
    public bool    IsMatch       { get; init; }
    public double  Score         { get; init; }
    public double  Threshold     { get; init; }
    public string? SpecialReason { get; init; }
    public Dictionary<string, double> AlgoScores     { get; init; } = [];

    /// <summary>
    /// Normalizations applied before comparison — e.g. "KR → KUMAR", "DR stripped".
    /// Empty when names were compared as-is.
    /// </summary>
    public List<string> NormalizationNotes { get; init; } = [];

    public bool IsSpecialMatch => SpecialReason is not null;

    public static MatchResult Special(bool isMatch, string reason, List<string>? notes = null) => new()
    {
        IsMatch            = isMatch,
        Score              = isMatch ? 1.0 : 0.0,
        Threshold          = 0,
        SpecialReason      = reason,
        AlgoScores         = [],
        NormalizationNotes = notes ?? [],
    };
}
