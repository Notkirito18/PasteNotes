PasteNotes Portable
===================

No installation is required.

1. Keep the entire folder together (PasteNotes.exe and the _internal folder).
2. Double-click PasteNotes.exe.
3. PasteNotes starts in the system tray and opens the manager window.
4. Closing the manager window keeps PasteNotes running in the tray. Use the
   tray menu to reopen it or to exit.

Data
----
This portable build keeps your data next to the executable, in:

    PasteNotesData\data.json
    PasteNotesData\pasteNotes.log

Because "PasteNotes.portable" is present next to PasteNotes.exe, the app uses
that folder instead of your Windows profile. Back up the PasteNotesData folder
to keep a copy of your groups and templates, and copy it to another PasteNotes
portable folder to bring your notes with you.

If the app cannot write next to the executable (for example, because it is on
a read-only drive), it falls back to:

    %APPDATA%\PasteNotes

To bring existing data from an installed/development copy, copy
%APPDATA%\PasteNotes\data.json into PasteNotesData\ before starting the app.

Usage
-----
- Tray menu: Open PasteNotes, Pause hotkeys, Exit.
- New data files start with 8 groups and 78 organized templates:
    Ctrl+Alt+1  WhatsApp - Customer Care (AR)
    Ctrl+Alt+2  WhatsApp - Transfers (AR)
    Ctrl+Alt+3  WhatsApp - Rates & Gold (AR)
    Ctrl+Alt+4  WhatsApp - Cards & Wallets (AR)
    Ctrl+Alt+5  Email - Operations (EN)
    Ctrl+Alt+6  Refunds & Payments (EN)
    Ctrl+Alt+7  Verification (EN)
    Ctrl+Alt+8  Internal - Notes & Prompts (EN)
  Defaults are seeded only when no data.json exists. Existing data is left
  unchanged.
- In the picker: type to search, Up/Down/PgUp/PgDn to move, Enter or click to
  paste, Esc to cancel.
- Each group has one of 8 colors, selected with the swatches in the manager.
- Arabic and other right-to-left text is detected automatically in the content
  editor.

Limitations
-----------
- Windows 10/11, 64-bit.
- Pasting uses the clipboard plus a simulated Ctrl+V. Apps running elevated
  (as administrator) will not receive pastes unless PasteNotes is elevated too.
- Clipboard restore is best-effort for unusual clipboard formats.
- Global hotkeys may conflict with other applications; the manager shows the
  conflict if registration fails.
- The executable is not code-signed and uses UPX compression, so Windows
  SmartScreen or antivirus software may warn, quarantine, or block it.
  Use "More info" and "Run anyway", or allow the folder, if you trust this
  copy. Some managed/work PCs may block unsigned executables.
