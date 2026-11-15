# Rebuilding PasteNotes from text files

The GitHub repository should contain only source text so the app can be rebuilt
on another Windows PC without transferring any binary files. The icon is
generated during the build, so it does not need to be copied.

## What you need on the receiving PC

- Windows 10/11, 64-bit
- Python 3.10 or newer (tested with Python 3.14)
- Internet access for `pip` and, if needed, `winget`
- No existing PasteNotes folder. Use a new folder such as `C:\PasteNotes`.

## Required files

Create this exact folder structure and copy the contents of each file from the
GitHub **Raw** view:

```text
PasteNotes\
  build.bat
  launcher.py
  requirements.txt
  packaging\
    make_icon.py
    package_portable.py
    README_Portable.txt
  pastenotes\
    __init__.py
    __main__.py
    app.py
    colors.py
    defaults.py
    hotkeys.py
    manager.py
    paste.py
    picker.py
    storage.py
    textutils.py
```

These 17 files are enough to run and build the app.

### Optional files

```text
  README.md
  run.bat
  requirements-dev.txt
  .gitignore
  templates.txt
  REBUILD_FROM_TEXT.md
  tests\
    test_hotkeys.py
    test_storage.py
    test_textutils.py
```

- `tests\` and `requirements-dev.txt` are needed to run the test suite.
- `templates.txt` is the original reference file; the app's default templates
  are already embedded in `pastenotes\defaults.py`.
- `README.md` and `run.bat` are convenience files.
- `.gitignore` is recommended on GitHub, but is not needed to build the app.

## Publishing the source to GitHub

1. Create a new GitHub repository.
2. In the repository, use **Add file > Create new file**.
3. In the filename field, type the full path, for example:
   `pastenotes/app.py`. GitHub creates the folder automatically.
4. Paste the complete file contents and commit the file.
5. Repeat for every required file.
6. Open `pastenotes/defaults.py` on GitHub and confirm the Arabic text appears
   correctly. If it appears as `????`, the file was saved with the wrong
   encoding; replace it with UTF-8 content.
7. Do not upload generated or binary files: `dist\`, `build\`,
   `PasteNotes_Portable.zip`, `PasteNotes.spec`, `packaging\PasteNotes.ico`,
   `PasteNotesData\`, `__pycache__\`, or `.pytest_cache\`.

## Copying the files on the receiving PC

1. Create `C:\PasteNotes` and the `pastenotes`, `packaging`, and optional
   `tests` subfolders.
2. In GitHub, open a file and click **Raw**. Do not copy the normal rendered
   page and do not copy line numbers.
3. Press `Ctrl+A`, then `Ctrl+C`.
4. Open Notepad, paste, and choose **File > Save As**.
5. Select the correct subfolder.
6. Enter the exact filename and extension, for example `storage.py`.
7. Set **Save as type** to **All Files (*.*)**.
8. Set **Encoding** to **UTF-8** (not ANSI).
9. Save the file.

### If you want to save as `.txt` and rename it

1. Save the file as `storage.py.txt` with UTF-8 encoding.
2. In File Explorer, enable **View > Show > File name extensions**.
3. Rename `storage.py.txt` to `storage.py`.
4. Confirm there is no remaining `.txt` in the filename.

`defaults.py`, `templates.txt`, and the tests contain Arabic text. Always use
UTF-8 for these files. Saving them as ANSI can replace the Arabic text with
question marks.

If `build.bat` does not run correctly after copying, open it in Notepad and
save it again. Notepad writes Windows line endings, which avoids rare
line-ending problems with batch files.

## Install dependencies

Open Command Prompt or PowerShell in `C:\PasteNotes`:

```powershell
py -m pip install --upgrade pip
py -m pip install -r requirements.txt
```

If `py` is not recognized, use `python` instead, or install Python from
python.org and enable the PATH option during installation.

## Run from source

```powershell
py -m pastenotes --show
```

PasteNotes opens its manager and stays in the system tray. Use the tray menu to
pause hotkeys or exit. The default groups use `Ctrl+Alt+1` through
`Ctrl+Alt+8`.

## Verify the copied source

Install the development requirements and run the tests:

```powershell
py -m pip install -r requirements-dev.txt
py -m pytest -q
```

Expected result:

```text
26 passed
```

The tests check the 8 default groups, all 78 templates, unique group hotkeys and
template titles, and the exact Arabic greeting. A failure usually means a file
was truncated, saved with the wrong encoding, or has the wrong extension.

These instructions were validated from a clean copy containing only the listed
text files. The build generated the icon and a 20.73 MB ZIP, and the packaged-app
checks passed.

## Build the portable app

```powershell
build.bat
```

The build script:

1. Installs or updates PyInstaller.
2. Cleans old build output.
3. Checks for UPX and tries to install it with `winget` if missing.
4. Generates `packaging\PasteNotes.ico` if it is missing.
5. Builds `dist\PasteNotes\PasteNotes.exe` with PyInstaller's automatic UPX
   compression disabled.
6. Applies controlled UPX compression to safe files, trims the bundle, then
   creates `PasteNotes_Portable.zip`.

The ZIP is designed to stay under 24 MB. Keep the whole extracted
`PasteNotes` folder together; `PasteNotes.exe` needs its `_internal` folder.

If UPX is not available, install it manually and make sure `upx.exe` is on
`PATH`, then run `build.bat` again:

```powershell
winget install --id UPX.UPX -e
```

UPX is available from https://upx.github.io/ if `winget` is unavailable.

## Data and defaults

- Running from source stores data in `%APPDATA%\PasteNotes\data.json`.
- The portable build stores data in `PasteNotesData\data.json` beside the
  executable, unless that folder is not writable.
- The 8 groups and 78 templates are seeded only when no `data.json` exists.
  Existing data is never overwritten or migrated.
- To see the new defaults on a PC that already has PasteNotes data, back it up
  first, then rename or delete the existing `data.json`.
- Do not copy `PasteNotesData` into the rebuilt ZIP. It contains personal data
  and is not part of the source.

## Troubleshooting

| Problem | Fix |
| --- | --- |
| `py` is not recognized | Install Python and enable the PATH option, or use `python`. |
| `No module named PySide6` | Run `py -m pip install -r requirements.txt`. |
| Arabic appears as `????` | Resave `defaults.py` as UTF-8, then rerun the tests. |
| File is named `app.py.txt` or `app.py.py` | Enable file extensions and rename it to exactly `app.py`. |
| `build.bat` exits immediately | Run it from Command Prompt in the project folder and read the error. |
| UPX is missing or the ZIP is too large | Install UPX with `winget` or manually, add it to PATH, and rebuild. |
| A hotkey does not work | Another app owns that hotkey. Change it in the manager. |
| SmartScreen or antivirus warns | The exe is unsigned and UPX-compressed; allow it only if you trust this source. |
| Pasting fails in an elevated app | Run PasteNotes as administrator as well. |

## Minimum checklist

- [ ] Copied all 17 required files with exact names and folders.
- [ ] Saved `defaults.py` and tests as UTF-8.
- [ ] Installed `requirements.txt`.
- [ ] `py -m pastenotes --show` opens the manager.
- [ ] `py -m pytest -q` reports 26 passed.
- [ ] `build.bat` creates `PasteNotes_Portable.zip` under 24 MB.
