using System.Text.Json;
using System.Text.Json.Serialization;

namespace IndianFuzzyNameMatcher;

// ─────────────────────────────────────────────────────────────────────────────
//  ValidatorConfig
//
//  Loads configuration in this order (later overrides earlier):
//    1. Defaults defined here in code
//    2. config.json in the same directory as the executable
//    3. Command-line arguments  (--threshold 80)
//
//  To change the default threshold without recompiling:
//    • Edit config.json  →  { "threshold": 75 }
//    • Or run with arg   →  PanNameValidator.exe --threshold 75
//
//  config.json is gitignored by default so each environment can have its own.
//  Commit config.json.example to document available keys.
// ─────────────────────────────────────────────────────────────────────────────

public sealed class ValidatorConfig
{
    // ── Match threshold ───────────────────────────────────────────────────────

    /// <summary>
    /// Minimum composite score (0.0–1.0) for a name pair to be considered a match.
    /// Production value was 0.70 with a ±0.05 fallback band that re-invoked the
    /// external API for ambiguous scores before rejecting.
    /// </summary>
    [JsonPropertyName("threshold")]
    public double Threshold { get; set; } = 0.72;

    /// <summary>
    /// Show individual algorithm scores after each comparison.
    /// Set to false for a cleaner output in batch/scripted use.
    /// </summary>
    [JsonPropertyName("showAlgoBreakdown")]
    public bool ShowAlgoBreakdown { get; set; } = true;

    // ── Algorithm weights ─────────────────────────────────────────────────────
    //
    // These six weights control how much each similarity algorithm contributes
    // to the final composite score. They must sum to 1.0.
    //
    // Tuning guide:
    //   - Raise EditDistance / JaroWinkler  → better at typos and transpositions
    //   - Raise Phonetic                    → better at transliteration variants
    //   - Raise NGram / Cosine / Jaccard    → better at partial-word overlap
    //
    // The current defaults favour position-aware algorithms because bigram methods
    // (NGram, Cosine, Jaccard) penalise transpositions too harshly — swapping two
    // adjacent characters destroys multiple bigrams even when the edit distance is 1.

    /// <summary>Damerau-Levenshtein edit distance, normalised to 0–1.</summary>
    [JsonPropertyName("weightEditDistance")]
    public double WeightEditDistance { get; set; } = 0.30;

    /// <summary>Jaro-Winkler — rewards shared prefix and handles transpositions.</summary>
    [JsonPropertyName("weightJaroWinkler")]
    public double WeightJaroWinkler { get; set; } = 0.30;

    /// <summary>Indian phonetic normalizer — collapses transliteration variants before comparison.</summary>
    [JsonPropertyName("weightPhonetic")]
    public double WeightPhonetic { get; set; } = 0.20;

    /// <summary>Bigram N-Gram overlap — catches partial character matches.</summary>
    [JsonPropertyName("weightNgram")]
    public double WeightNgram { get; set; } = 0.08;

    /// <summary>Cosine similarity on bigram frequency vectors — order-independent.</summary>
    [JsonPropertyName("weightCosine")]
    public double WeightCosine { get; set; } = 0.06;

    /// <summary>Jaccard — word-level for multi-token names, bigram-level for single tokens.</summary>
    [JsonPropertyName("weightJaccard")]
    public double WeightJaccard { get; set; } = 0.06;

    // ── Loader ───────────────────────────────────────────────────────────────

    private static readonly string ConfigFilePath =
        Path.Combine(AppContext.BaseDirectory, "config.json");

    /// <summary>
    /// Load config from defaults → config.json → CLI args.
    /// </summary>
    public static ValidatorConfig Load(string[] args)
    {
        var config = new ValidatorConfig();

        // 1. config.json (optional — silently skipped if missing)
        if (File.Exists(ConfigFilePath))
        {
            try
            {
                var json    = File.ReadAllText(ConfigFilePath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var fromFile = JsonSerializer.Deserialize<ValidatorConfig>(json, options);
                if (fromFile is not null) config = fromFile;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"  [config] Could not read config.json: {ex.Message}");
                Console.ResetColor();
            }
        }

        // 2. CLI args — override individual values
        //    Supported: --threshold <value>   --no-breakdown
        for (int i = 0; i < args.Length; i++)
        {
            if ((args[i] == "--threshold" || args[i] == "-t") && i + 1 < args.Length)
            {
                if (double.TryParse(args[i + 1], out double t))
                    config.Threshold = t > 1 ? t / 100.0 : t;
                i++;
            }
            else if (args[i] == "--no-breakdown")
            {
                config.ShowAlgoBreakdown = false;
            }
        }

        config.Threshold = Math.Clamp(config.Threshold, 0.01, 1.0);
        return config;
    }
}
