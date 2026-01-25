# Phoenix Wright: Ace Attorney Trilogy Accessibility Mod

A screen reader accessibility mod for Phoenix Wright: Ace Attorney Trilogy. The mod outputs game text (dialogue, menus, UI elements) directly to screen readers via the UniversalSpeech library, with SAPI fallback for users without a screen reader.

## Features

- Full dialogue output with character name announcements
- Menu and UI navigation feedback
- Investigation mode with hotspot navigation
- Court record (evidence/profiles) accessibility
- Trial mode with life gauge announcements
- Support for all minigames:
  - Luminol spray blood detection
  - Fingerprint dusting
  - 3D evidence examination
  - Vase puzzle
  - Dying message connect-the-dots
  - Bug sweeper
  - Video tape examination
- Orchestra music player (game soundtrack browser)
- Psyche-Lock sequences (GS2/GS3)
- Mod translations available (see [Translations](#translations))

## Requirements

- Phoenix Wright: Ace Attorney Trilogy (Steam version)
- [MelonLoader v0.7.1](https://github.com/LavaGang/MelonLoader/releases/tag/v0.7.1)
- UniversalSpeech.dll (32-bit) in the game directory for screen reader output
- nvdaControllerClient.dll (32-bit) in the game directory for NVDA users (optional)

## Installation

You can install this in two ways, either by using the AccessMods Installer or by downloading the release zip file and installing the mod manually.

### Automatic Installation

1. Download `AccessModsInstaller.exe` from the latest release of the [AccessMods Installer](https://github.com/AccessMods/AccessModsInstaller/releases/latest)
2. Run the installer, select Phoenix Wright: Ace Attorney Trilogy, and follow the prompts
3. Launch the game

### Manual Installation

1. Download the zip file from the latest release
2. Run the included `MelonLoader.Installer.exe` and install MelonLoader to your game
3. Copy `AccessibilityMod.dll` to the `Mods` folder in your game directory
4. Copy `UnityAccessibilityLib.dll`, `UniversalSpeech.dll` and `nvdaControllerClient.dll` to your game directory
5. Copy the `Data` folder contents to `[Game Directory]/UserData/AccessibilityMod/`
6. Launch the game

## Keyboard Shortcuts

| Key       | Context           | Action                                             |
| --------- | ----------------- | -------------------------------------------------- |
| **F5**    | Global            | Hot-reload config files                            |
| **R**     | Global            | Repeat last output                                 |
| **I**     | Global            | Announce current state/context                     |
| **[ / ]** | Navigation modes  | Navigate items (hotspots, evidence, targets, etc.) |
| **U**     | Investigation     | Jump to next unexamined hotspot                    |
| **H**     | Trial | announce life gauge   |
| **F1**    | Context-sensitive    | Announce                             help hints |

## Configuration

Configuration files are stored in `[Game Directory]/UserData/AccessibilityMod/`. Press **F5** in-game to hot-reload without restarting.

```
UserData/AccessibilityMod/
├── en/                     # English (fallback)
│   ├── strings.json        # UI strings
│   ├── GS1_Names.json      # Character name mappings
│   ├── GS2_Names.json
│   ├── GS3_Names.json
│   └── EvidenceDetails/    # Evidence descriptions
│       ├── GS1/*.txt
│       ├── GS2/*.txt
│       └── GS3/*.txt
├── pt-BR/                  # Brazilian Portuguese
└── zh-Hans/                # Chinese (Simplified)
```

## Building from Source

```bash
cd AccessibilityMod
dotnet build -c Release
```

The build output is automatically copied to the game's `Mods` folder, along with the contents of the `Data` folder which are copied to `UserData/AccessibilityMod`.

### Build Requirements

- .NET Framework 3.5 targeting pack
- MelonLoader installed in the game directory

## Contributing

Contributions are welcome! See [CONTRIBUTING.md](CONTRIBUTING.md) for development setup and guidelines.

## Translations

The mod supports multiple languages. Currently included translations:

- English
- Brazilian Portuguese
- Chinese (Simplified)
- Korean

Want to help translate? See [TRANSLATORS.md](TRANSLATORS.md) for guidelines.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
