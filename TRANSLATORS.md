# Translation Guide

This guide explains how to add translations for the Phoenix Wright Accessibility Mod.

## Overview

The mod supports multiple languages by loading text from language-specific folders. When the game runs, the mod detects the game's language setting and loads the appropriate translation files. If a translation doesn't exist for the current language, English is used as a fallback.

## Folder Structure

Each language has its own folder under `AccessibilityMod/Data/`:

```
AccessibilityMod/Data/
├── en/                          # English (base/fallback language)
│   ├── strings.json             # UI strings and announcements
│   ├── GS1_Names.json           # Character names for Game 1
│   ├── GS2_Names.json           # Character names for Game 2
│   ├── GS3_Names.json           # Character names for Game 3
│   └── EvidenceDetails/         # Evidence detail descriptions
│       ├── GS1/
│       │   ├── 1.txt
│       │   ├── 2.txt
│       │   └── ...
│       ├── GS2/
│       └── GS3/
├── fr/                          # French
├── de/                          # German
├── ja/                          # Japanese
├── ko/                          # Korean
├── zh-Hans/                     # Simplified Chinese
├── zh-Hant/                     # Traditional Chinese
├── pt-BR/                       # Brazilian Portuguese
└── es/                          # Spanish (Latin America)
```

## Language Codes

The mod uses these language codes based on the game's language setting:

| Game Language           | Folder Name |
| ----------------------- | ----------- |
| English (USA)           | `en`        |
| Japanese                | `ja`        |
| French                  | `fr`        |
| German                  | `de`        |
| Korean                  | `ko`        |
| Simplified Chinese      | `zh-Hans`   |
| Traditional Chinese     | `zh-Hant`   |
| Brazilian Portuguese    | `pt-BR`     |
| Spanish (Latin America) | `es`        |

## How to Create a Translation

1. **Copy the English folder**: Create a copy of the `en/` folder and rename it to your language code (e.g., `fr/` for French).

2. **Translate the files**: Edit each file in your new folder to replace English text with your translation.

3. **Test your translation**: Set your game language to match your translation and run the game with the mod.

4. **Submit a pull request**: Add your translation folder to the repository.

## File Formats

### strings.json

This file contains all the mod's UI strings and announcements. It's a JSON file with key-value pairs.

**Format:**

```json
{
  "key.name": "Translated text here",
  "key.with.placeholder": "Text with {0} placeholder"
}
```

**Rules:**

- Keys must remain exactly as they are in the English file (don't translate keys)
- Placeholders like `{0}`, `{1}` must be preserved in your translation
- Lines starting with `_` are comments/section markers and don't need translation
- Keep the JSON syntax valid (quotes, commas, braces)

**Example:**

```json
{
  "trial.penalty": "Penalty! {0} remaining",
  "navigation.item_x_of_y": "Item {0} of {1}"
}
```

### Character Name Files (GS1_Names.json, GS2_Names.json, GS3_Names.json)

These files map character sprite IDs to display names. The game already localizes character names in dialogue, but these are used for announcements when the game doesn't provide a name.

**Format:**

```json
{
  "2": "Phoenix Wright",
  "4": "Maya Fey"
}
```

**Rules:**

- Keys are sprite IDs (numbers as strings) - might be different between localizations
- Values are character names in your language
- Use the official localized names from your version of the game

**Note:** Most character names should match the official game localization. Check your language's version of the game for the correct names.

### Evidence Detail Files (EvidenceDetails/GS1/\*.txt, etc.)

These files provide accessible descriptions for evidence detail images. The game shows these as images with no text, so we provide hand-written descriptions.

**Format:**

- Plain text files named by detail ID (e.g., `9.txt`)
- Multiple pages separated by `===` on its own line

**Example (9.txt):**

```
Case Summary:
12/28, 2001
Elevator, District Court.
Air in elevator was oxygen depleted at time of incident.
No clues found on the scene.
===
Victim Data:
Gregory Edgeworth (Age 35)
Defense attorney. Trapped in elevator returning from a lost trial with son Miles (Age 9).
One bullet found in heart. The murder weapon was fired twice.
===
Suspect Data:
Yanni Yogi (Age 37)
Court bailiff, trapped with the Edgeworths. Memory loss due to oxygen deprivation.
After his arrest, fiancee Polly Jenkins committed suicide.
```

**Rules:**

- Filename must be the detail ID number (e.g., `9.txt`)
- Use `===` on its own line to separate pages
- Describe the visual content of each page in the evidence detail view
- Dates, names, and facts should match the game's localization for your language

## Testing Your Translation

1. Copy your translation folder to the game's UserData folder:

   ```
   [Game Install]/UserData/AccessibilityMod/[your-language-code]/
   ```

2. Set the game's language to match your translation.

3. Launch the game with MelonLoader.

4. Press **F5** in-game to hot-reload translation files after making changes.

5. Test various scenarios:
   - Menu navigation
   - Dialogue (character names)
   - Investigation mode
   - Trial sequences
   - Evidence examination
   - Mini-games (fingerprinting, luminol, etc.)

## Partial Translations

You don't need to translate everything at once. The mod falls back to English for any missing files:

- If `fr/strings.json` is missing, English strings are used
- If `fr/GS2_Names.json` is missing, English names are used for GS2
- If `fr/EvidenceDetails/GS1/9.txt` is missing, the English description is used

This means you can start with just `strings.json` and add other files later.

## Tips

- **Use the English files as reference**: They contain all the keys and show the expected format.
- **Preserve placeholders**: `{0}`, `{1}`, etc. are replaced with values at runtime. Your translation must include them.
- **Match game terminology**: Use the same terms the official game localization uses (e.g., "Court Record" vs "Evidence").
- **Test edge cases**: Some strings are only shown in specific scenarios (mini-games, puzzles).
- **Keep formatting minimal**: The text is read by screen readers, so keep it clear and concise.

## Questions?

If you have questions about translating specific strings or need context for what a string is used for, please open an issue on the repository.
