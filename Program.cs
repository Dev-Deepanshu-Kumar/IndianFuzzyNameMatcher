using PanNameValidator;

// ─────────────────────────────────────────────────────────────────────────────
//  PAN Name Validator — Interactive Console
//
//  Configuration (in order of precedence):
//    1. Defaults in ValidatorConfig.cs
//    2. config.json  (edit this to change defaults without recompiling)
//    3. CLI args     --threshold <value>   --no-breakdown
//
//  Examples:
//    dotnet run
//    dotnet run -- --threshold 75
//    dotnet run -- --threshold 80 --no-breakdown
// ─────────────────────────────────────────────────────────────────────────────

var config = ValidatorConfig.Load(args);
ConsoleUI.Run(config);
