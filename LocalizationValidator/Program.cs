namespace LocalizationValidator;

class Program
{
    private static readonly string[] ValidLanguages =
    {
        "ja",
        "fr",
        "de",
        "ko",
        "zh-Hans",
        "zh-Hant",
        "pt-BR",
        "es",
    };

    static int Main(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
        {
            PrintHelp();
            return args.Length == 0 ? 1 : 0;
        }

        // Parse arguments
        string? targetLanguage = null;
        string dataPath = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "AccessibilityMod",
            "Data"
        );
        bool strict = false;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--data-path" && i + 1 < args.Length)
            {
                dataPath = args[++i];
            }
            else if (args[i] == "--strict")
            {
                strict = true;
            }
            else if (!args[i].StartsWith("-"))
            {
                targetLanguage = args[i];
            }
        }

        if (string.IsNullOrEmpty(targetLanguage))
        {
            Console.Error.WriteLine("Error: Target language is required.");
            PrintHelp();
            return 1;
        }

        if (!ValidLanguages.Contains(targetLanguage))
        {
            Console.Error.WriteLine($"Error: Invalid language '{targetLanguage}'.");
            Console.Error.WriteLine($"Valid languages: {string.Join(", ", ValidLanguages)}");
            return 1;
        }

        // Resolve paths
        dataPath = Path.GetFullPath(dataPath);
        var englishPath = Path.Combine(dataPath, "en", "strings.json");
        var targetPath = Path.Combine(dataPath, targetLanguage, "strings.json");

        if (!File.Exists(englishPath))
        {
            Console.Error.WriteLine($"Error: English strings.json not found at: {englishPath}");
            return 1;
        }

        if (!File.Exists(targetPath))
        {
            Console.Error.WriteLine(
                $"Error: {targetLanguage}/strings.json not found at: {targetPath}"
            );
            return 1;
        }

        Console.WriteLine($"Validating: {targetLanguage}/strings.json against en/strings.json");
        Console.WriteLine($"Data path: {dataPath}");
        Console.WriteLine();

        // Parse files
        var englishJson = File.ReadAllText(englishPath);
        var targetJson = File.ReadAllText(targetPath);

        var englishStrings = SimpleJsonParser.ParseStringDictionary(englishJson);
        var targetStrings = SimpleJsonParser.ParseStringDictionary(targetJson);

        if (englishStrings.Count == 0)
        {
            Console.Error.WriteLine("Error: Failed to parse English strings.json (no keys found)");
            return 1;
        }

        // Validate
        var validator = new StringsValidator();
        var result = validator.Validate(englishStrings, targetStrings);

        // Output results
        PrintResults(result, strict);

        // Return exit code
        bool failed = strict ? (result.HasErrors || result.WarningCount > 0) : result.HasErrors;
        return failed ? 1 : 0;
    }

    static void PrintHelp()
    {
        Console.WriteLine("LocalizationValidator - Validate translated strings.json files");
        Console.WriteLine();
        Console.WriteLine("Usage: LocalizationValidator <target-language> [options]");
        Console.WriteLine();
        Console.WriteLine("Arguments:");
        Console.WriteLine(
            "  <target-language>    Language code to validate (ja, fr, de, ko, zh-Hans, zh-Hant, pt-BR, es)"
        );
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine(
            "  --data-path <path>   Path to Data folder (default: ../AccessibilityMod/Data)"
        );
        Console.WriteLine("  --strict             Treat warnings as errors");
        Console.WriteLine("  --help, -h           Show this help");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  dotnet run -- ja");
        Console.WriteLine("  dotnet run -- fr --strict");
        Console.WriteLine("  dotnet run -- de --data-path C:\\Path\\To\\Data");
    }

    static void PrintResults(ValidationResult result, bool strict)
    {
        var errors = result.Issues.Where(i => i.Type == IssueType.Error).ToList();
        var warnings = result.Issues.Where(i => i.Type == IssueType.Warning).ToList();

        if (errors.Count > 0)
        {
            Console.WriteLine("=== ERRORS ===");
            foreach (var issue in errors.OrderBy(i => i.Key))
            {
                Console.WriteLine($"[{issue.Category}] {issue.Key} - {issue.Message}");
            }
            Console.WriteLine();
        }

        if (warnings.Count > 0)
        {
            Console.WriteLine("=== WARNINGS ===");
            foreach (var issue in warnings.OrderBy(i => i.Key))
            {
                Console.WriteLine($"[{issue.Category}] {issue.Key} - {issue.Message}");
            }
            Console.WriteLine();
        }

        Console.WriteLine("=== SUMMARY ===");
        Console.WriteLine($"English keys: {result.EnglishKeyCount}");
        Console.WriteLine($"Translated keys: {result.TranslatedKeyCount}");
        Console.WriteLine($"Errors: {result.ErrorCount}");
        Console.WriteLine($"Warnings: {result.WarningCount}");
        Console.WriteLine();

        bool failed = strict ? (result.HasErrors || result.WarningCount > 0) : result.HasErrors;
        if (failed)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Validation FAILED");
            Console.ResetColor();
        }
        else if (result.WarningCount > 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Validation PASSED with warnings");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Validation PASSED");
            Console.ResetColor();
        }
    }
}
