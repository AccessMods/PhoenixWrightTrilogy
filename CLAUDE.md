# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a screen reader accessibility mod for Phoenix Wright: Ace Attorney Trilogy using MelonLoader. The mod outputs game text (dialogue, menus, UI elements) directly to screen readers via the UniversalSpeech library, with SAPI fallback for users without a screen reader.

## Build Commands

```bash
# Build the mod (output goes to game's Mods folder automatically)
cd AccessibilityMod
dotnet build -c Release

# The post-build target copies AccessibilityMod.dll to $(GamePath)\Mods
# and copies Data/* files to $(GamePath)\UserData\AccessibilityMod\
```

## Utility Projects

```bash
# Validate translations against English strings.json
cd LocalizationValidator
dotnet run -- ja                    # Validate Japanese
dotnet run -- fr --strict           # Validate French, treat warnings as errors

# Build the standalone installer (publishes self-contained .exe)
cd Installer
dotnet publish -c Release
# Output: Installer/bin/Release/net8.0-windows/win-x64/publish/PWAATAccessibilityInstaller.exe
```

## Key Configuration

- **Target Framework**: `net35` (must match MelonLoader runtime)
- **GamePath**: `C:\Program Files (x86)\Steam\steamapps\common\Phoenix Wright Ace Attorney Trilogy`
- **MelonLoader References**: Use `net35` folder, not `net6`
- **UserData Config**: Runtime overrides in `$(GamePath)\UserData\AccessibilityMod\`
- **UniversalSpeech**: Requires `UniversalSpeech.dll` (32-bit) in the game directory for screen reader output
- **Logs**: MelonLoader logs to `$(GamePath)\MelonLoader\Latest.log` - use `AccessibilityMod.Logger.Msg()` for debug output

## Architecture

### Core Components

- **AccessibilityMod.Core.AccessibilityMod**: Main MelonMod entry point. Handles initialization and keyboard input via `OnUpdate()`
- **MelonLoggerAdapter**: Bridges MelonLoader logging with the MelonAccessibilityLib library
- **CoroutineRunner**: MonoBehaviour singleton for menu cursor tracking and delayed announcements

### MelonAccessibilityLib (NuGet dependency)

The following components are provided by the `MelonAccessibilityLib` NuGet package:

- **UniversalSpeechWrapper**: Low-level P/Invoke wrapper around UniversalSpeech library for text-to-speech output
- **SpeechManager**: Centralized speech output manager
- **Net35Extensions**: Polyfills for .NET 3.5 compatibility (`IsNullOrWhiteSpace`, etc.)
- **TextCleaner**: Strips formatting tags and normalizes text for screen reader output

### Keyboard Shortcuts

| Key   | Context                           | Action                                                      |
| ----- | --------------------------------- | ----------------------------------------------------------- |
| F5    | Global                            | Hot-reload config files (character names, evidence details) |
| R     | Global (except vase/court record) | Repeat last output                                          |
| I     | Global                            | Announce current state/context                              |
| [ / ] | Investigation                     | Navigate hotspots                                           |
| U     | Investigation                     | Jump to next unexamined hotspot                             |
| F1    | Investigation                     | List all hotspots                                           |
| [ / ] | Pointing mode                     | Navigate target areas                                       |
| F1    | Pointing mode                     | List all target areas                                       |
| [ / ] | Luminol mode                      | Navigate blood evidence                                     |
| [ / ] | 3D Evidence                       | Navigate examination points                                 |
| [ / ] | Fingerprint mode                  | Navigate fingerprint locations                              |
| F1    | Fingerprint mode                  | Get hint for current phase                                  |
| [ / ] | Video tape mode                   | Navigate to targets when paused                             |
| F1    | Video tape mode                   | Get hint                                                    |
| F1    | Vase puzzle                       | Get hint for current step                                   |
| F1    | Vase show (rotation)              | Get hint                                                    |
| [ / ] | Dying message                     | Navigate between dots                                       |
| F1    | Dying message                     | Get hint for spelling                                       |
| F1    | Bug sweeper                       | Announce state/hint                                         |
| F1    | Orchestra mode                    | Announce controls help                                      |
| H     | Trial (not pointing)              | Announce life gauge                                         |

### Patches (Harmony)

All patches use `[HarmonyPostfix]` (or `[HarmonyPrefix]` for capture-before-clear) to hook into game methods:

- **DialoguePatches**: Multiple hooks (`arrow`, `board`, `guideIconSet`, `ClearText`) for robust dialogue capture
- **MenuPatches**: Hooks `tanteiMenu`, `selectPlateCtrl` for detective menu and choice navigation
- **InvestigationPatches**: Hooks `inspectCtrl` for investigation mode cursor feedback
- **TrialPatches**: Hooks `lifeGaugeCtrl.ActionLifeGauge()` for health/penalty announcements
- **SaveLoadPatches**: Hooks `SaveLoadUICtrl`, `optionCtrl` for save/load and options menus
- **CourtRecordPatches**: Hooks court record navigation for evidence/profiles
- **CutscenePatches**: Handles cutscene dialogue capture
- **LuminolPatches**: Hooks luminol spray minigame for blood detection
- **VasePuzzlePatches**: Hooks vase puzzle state for accessibility hints
- **FingerprintPatches**: Hooks fingerprint dusting minigame
- **ScienceInvestigationPatches**: Hooks science investigation minigames (3D evidence)
- **DyingMessagePatches**: Hooks connect-the-dots puzzle (GS1 Episode 5)
- **BugSweeperPatches**: Hooks bug sweeper minigame (GS2/GS3)
- **GalleryPatches**: Hooks gallery menu navigation
- **GalleryOrchestraPatches**: Hooks orchestra music player
- **PsycheLockPatches**: Hooks Psyche-Lock sequences (GS2/GS3)
- **VerdictPatches**: Hooks verdict announcements
- **SafeKeypadPatches**: Hooks safe keypad input

### Services

- **LocalizationService**: Multi-language string management with English fallback. Auto-detects game language and reloads on language change.
- **L**: Shorthand alias for `LocalizationService.Get()` - use `L.Get("key")` throughout the codebase
- **CharacterNameService**: Maps sprite IDs to character names. Supports hot-reload from JSON files
- **EvidenceDetailService**: Provides accessibility descriptions for evidence detail images. Supports hot-reload from text files
- **HotspotNavigator**: Parses `GSStatic.inspect_data_` to enable list-based navigation of examination points
- **AccessibilityState**: Tracks current game mode and delegates to appropriate navigators
- **PointingNavigator**: Navigation for court map and pointing minigames
- **LuminolNavigator**: Blood evidence detection in luminol spray mode
- **VasePuzzleNavigator**: Step-by-step hints for the vase puzzle minigame
- **VaseShowNavigator**: Vase rotation puzzle (unstable jar in GS1)
- **FingerprintNavigator**: Fingerprint dusting minigame navigation
- **VideoTapeNavigator**: Video tape examination mode (scrubbing/targeting)
- **Evidence3DNavigator**: 3D evidence examination hotspot navigation
- **DyingMessageNavigator**: Connect-the-dots puzzle navigation
- **BugSweeperNavigator**: Bug sweeper minigame hints
- **GalleryOrchestraNavigator**: Orchestra music player accessibility

### Input Handling

**InputManager** processes keyboard input with mode-specific priority. Modes are checked in this order (first match wins):

1. 3D Evidence → 2. Luminol → 3. Vase Puzzle → 4. Fingerprint → 5. Video Tape → 6. Orchestra → 7. Dying Message → 8. Bug Sweeper → 9. Vase Show → 10. Pointing → 11. Investigation → 12. Trial (H key only)

Mode detection delegates to `AccessibilityState.IsIn[Mode]()` methods which check game singleton instances.

## Configuration Files

All configuration files live in `$(GamePath)/UserData/AccessibilityMod/`. Press **F5** in-game to hot-reload without restarting.

### Folder Structure

```
UserData/AccessibilityMod/
├── en/                     # English (fallback) localisation
│   ├── strings.json        # UI strings for the mod
│   ├── GS1_Names.json      # GS1 character name mappings
│   ├── GS2_Names.json      # GS2 character name mappings
│   ├── GS3_Names.json      # GS3 character name mappings
│   └── EvidenceDetails/    # Evidence detail descriptions
│       ├── GS1/*.txt
│       ├── GS2/*.txt
│       └── GS3/*.txt
├── ja/                     # Japanese localisation (same structure)
├── fr/                     # French localisation
└── [other languages]/      # de, ko, zh-Hans, zh-Hant, pt-BR, es
```

### Character Names

JSON files map sprite IDs to display names:
```json
{"1": "Phoenix", "2": "Maya", "10": "Edgeworth"}
```

### Evidence Details

Text files named by detail_id. Use `===` to separate multiple pages:
```
This is page 1 of the evidence description.
===
This is page 2.
```

### Localisation

The mod auto-detects game language via `GSStatic.global_work_.language` and loads strings from the appropriate folder with English fallback. Supported languages:
- USA → `en/`, JAPAN → `ja/`, FRANCE → `fr/`, GERMAN → `de/`
- KOREA → `ko/`, CHINA_S → `zh-Hans/`, CHINA_T → `zh-Hant/`
- Pt_BR → `pt-BR/`, ES_419 → `es/`

Use `L.Get("key")` or `L.Get("key", arg1, arg2)` for localised strings (alias for `LocalizationService.Get()`).

## Important Patterns

### Harmony Patches

When adding new patches, verify method names exist in `./Decompiled/` first. The game's API differs from typical Unity conventions. Common issues:

- Method names are often lowercase (`arrow`, `board`, `name_plate`)
- Enum parameters must match exactly (e.g., `lifeGaugeCtrl.Gauge_State`)
- Private methods can be patched but require correct signatures

### Character Name Mapping

Speaker IDs differ per game and come from two sources that must be handled differently:

**Two Speaker ID Sources:**

1. `GSStatic.message_work_.speaker_id` - Raw speaker ID from the message system
2. `_lastSpeakerId` in DialoguePatches - Captured from `name_plate()` callback's `in_name_no` parameter

**GS1/GS2 Behavior:**

- Both sources typically contain the same raw speaker ID
- `CharacterNameService` dictionaries (`GS1_NAMES`, `GS2_NAMES`) are keyed by these raw IDs
- **Problem**: `message_work_.speaker_id` can become stale during certain game modes (e.g., 3D evidence examination in GS1 Episode 5). When examining a hotspot, dialogue plays but `speaker_id` still holds the value from before entering 3D mode.
- **Solution**: Prefer `_lastSpeakerId` from `name_plate()` calls, fall back to `message_work_.speaker_id`

**GS3 Behavior:**

- GS3 uses `name_id_tbl` (in `messageBoardCtrl`) to remap raw speaker IDs to sprite indices for display
- The `name_plate()` method receives the raw ID, then remaps it internally for the sprite
- `GS3_NAMES` dictionary is keyed by raw `message_work_.speaker_id` values, NOT the remapped sprite indices
- The ID passed to `name_plate()` may differ from `message_work_.speaker_id` in some scenarios
- **Solution**: Always use `message_work_.speaker_id` for GS3

**Implementation** (in `DialoguePatches.TryOutputDialogue()`):

```csharp
if (isGS3)
    speakerId = message_work_.speaker_id & 0x7F;  // GS3: use message_work
else
    speakerId = _lastSpeakerId > 0 ? _lastSpeakerId : (message_work_.speaker_id & 0x7F);  // GS1/GS2: prefer name_plate
```

When names are wrong, check which game is active via `GSStatic.global_work_.title`. Unknown IDs are logged automatically.

### .NET 3.5 Limitations

- No `string.IsNullOrWhiteSpace` - use `Net35Extensions.IsNullOrWhiteSpace`
- No `StringBuilder.Clear()` - use `sb.Length = 0`
- No `string.Join(IEnumerable)` - use `.ToArray()` first

## Game State Access

Key global variables for accessing game state:

- `GSStatic.global_work_` - Global game state (current title, scene state)
- `GSStatic.global_work_.title` - Current game: 0=GS1, 1=GS2, 2=GS3
- `GSStatic.global_work_.r.no_0` - Scene type (4=questioning, 7=testimony)
- `GSStatic.message_work_` - Current dialogue/message state
- `GSStatic.message_work_.speaker_id` - Current speaker ID
- `GSStatic.inspect_data_` - Investigation hotspot definitions

Mode detection uses singleton instances:

- `inspectCtrl.instance?.is_play` - Investigation mode active
- `scienceInvestigationCtrl.instance?.is_play` - 3D evidence mode active
- `messageBoardCtrl.instance` - Dialogue system instance

## Navigator Pattern

All navigators follow a consistent structure:

```csharp
public static class [Xxx]Navigator {
    private static List<[Info]> _items;
    private static int _currentIndex;
    private static bool _wasActive;

    public static void Update()               // Called each frame from OnUpdate()
    public static bool Is[Xxx]Active()        // Check if mode is active
    public static void Refresh[Items]()       // Parse game data into _items
    public static void NavigateNext()         // [ key - move forward
    public static void NavigatePrevious()     // ] key - move backward
    public static void Announce[State/Hint]() // H key - context help
}
```

Position descriptions use the game's internal coordinate system (1920x1080, resolution-independent) mapped to "top/middle/bottom left/center/right".

## Decompiled Reference

The `./Decompiled/` folder contains decompiled game code for reference. Key files:

- `messageBoardCtrl.cs` - Dialogue display system
- `inspectCtrl.cs` - Investigation cursor and hotspots
- `GSStatic.cs` - Global game state
- `lifeGaugeCtrl.cs` - Trial health gauge
- `tanteiMenu.cs`, `selectPlateCtrl.cs` - Menu systems

**Important**: Verify method names in decompiled code before creating Harmony patches. The game uses lowercase method names (`arrow`, `board`, `name_plate`) unlike typical Unity conventions.
