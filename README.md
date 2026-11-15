# PasteNotes

A small Windows tray app that keeps your most-used text snippets one hotkey away.

> Rebuilding this project by copying text files from GitHub? Follow [REBUILD_FROM_TEXT.md](REBUILD_FROM_TEXT.md).

Press a group hotkey from any app, pick a template (type to filter, use the arrow keys, then press Enter or click), and PasteNotes pastes it into the field you were typing in and gives focus back. Templates and groups are managed from the tray window.

## Requirements

- Windows 10/11
- Python 3.10+ (tested with 3.14)

## Setup

```powershell
py -m pip install -r requirements.txt
```

## Run

```powershell
py -m pastenotes --show
```

Or double-click `run.bat` (starts in the tray and opens the manager).

The app does not start with Windows by design; launch it when you need it.

## Portable Windows build

Python and the runtime dependencies are only needed on the build PC.

```powershell
build.bat
```

The build script installs PyInstaller and UPX when needed and creates:

- `dist\PasteNotes\` - the standalone folder (`PasteNotes.exe` plus `_internal`)
- `PasteNotes_Portable.zip` - a compact ZIP under 24 MB; extract it on another Windows PC and run `PasteNotes.exe`

The portable build keeps its data next to the executable, in `PasteNotesData\data.json` and `PasteNotesData\pasteNotes.log`, because of the included `PasteNotes.portable` marker. If the executable folder is not writable, it falls back to `%APPDATA%\PasteNotes`. To bring existing data from a source build, copy `%APPDATA%\PasteNotes\data.json` into `PasteNotesData\` before starting the portable exe.

The packaging step removes unused Qt components and UPX-compresses the bundled binaries to meet the 24 MB limit. UPX compression reduces some Windows exploit mitigations and can increase antivirus false positives. The executable is also not code-signed, so Windows SmartScreen may show a warning on first launch. Use "More info" then "Run anyway" if you trust the file.

## Usage

- Tray menu: Open PasteNotes, Pause hotkeys, Exit.
- Closing the manager window keeps PasteNotes running in the tray.
- New data files start with 8 groups and 78 organized templates (existing data is left unchanged):
  - `Ctrl+Alt+1` - WhatsApp - Customer Care (AR)
  - `Ctrl+Alt+2` - WhatsApp - Transfers (AR)
  - `Ctrl+Alt+3` - WhatsApp - Rates & Gold (AR)
  - `Ctrl+Alt+4` - WhatsApp - Cards & Wallets (AR)
  - `Ctrl+Alt+5` - Email - Operations (EN)
  - `Ctrl+Alt+6` - Refunds & Payments (EN)
  - `Ctrl+Alt+7` - Verification (EN)
  - `Ctrl+Alt+8` - Internal - Notes & Prompts (EN)
- Default templates are seeded only when no `data.json` exists. Delete or rename the data file to restore the defaults, or keep your current data and add the templates you need.
- Change a group's hotkey in the manager's hotkey field. A hotkey needs at least one modifier (Ctrl/Alt/Shift/Win) or an F-key.
- Each group can use one of 8 sticky-note colors, chosen with the swatches above the hotkey field. Different groups can share the same color; the color is shown in the picker's header and selection highlight.
- Arabic and other right-to-left text is detected automatically: the content editor switches to right-to-left alignment while you type or edit RTL text, and RTL titles/group names are aligned correctly.
- In the picker: type to filter, `Up`/`Down`/`PgUp`/`PgDn` to move, `Enter` or click to paste, `Esc` to cancel.
- "Restore previous clipboard after pasting" (on by default) puts your old clipboard content back after the template is pasted.

## Data

Running from source:

- `%APPDATA%\PasteNotes\data.json` - groups, templates, settings (backup: `data.json.bak`)
- `%APPDATA%\PasteNotes\pasteNotes.log` - diagnostic log

Portable build:

- `PasteNotesData\data.json` and `PasteNotesData\pasteNotes.log` next to `PasteNotes.exe`
- Falls back to the `%APPDATA%` paths above if the portable folder is not writable

## Development

```powershell
py -m pip install -r requirements-dev.txt
py -m pytest
```

## Known limitations

- Windows only.
- Pasting uses the clipboard plus a simulated `Ctrl+V`. Apps that cannot receive simulated input (for example, apps running as administrator while PasteNotes is not) will not receive the template.
- Clipboard restore is best-effort; unusual or custom clipboard formats may not be restored perfectly.
- Hotkeys are global; a combo already owned by another app shows a registration error in the manager.
- The portable executable is unsigned and UPX-compressed; SmartScreen, antivirus, or enterprise security software may warn, quarantine, or block it.
- No rich text, images, or cloud sync in this version.
