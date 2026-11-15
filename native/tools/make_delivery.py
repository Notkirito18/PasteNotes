"""Assemble the two text files delivered to the target PC.

Run from anywhere:
    py native/tools/make_delivery.py
"""

from __future__ import annotations

import hashlib
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SOURCE_DIR = ROOT / "native" / "src"
DELIVERY_DIR = ROOT / "native" / "delivery"

HEADER = """// PasteNotes native Windows build.
// Generated single-file source. Build with Build.bat.
// The default groups and templates are stored in the compressed blob below.

"""

BUILD_BAT = r"""@echo off
setlocal EnableExtensions
cd /d "%~dp0"

set "WORK=%TEMP%\PasteNotesBuild"

set "SRC=%~dp0PasteNotes.cs"
if not exist "%SRC%" set "SRC=%~dp0PasteNotes.cs.txt"
if not exist "%SRC%" (
    echo.
    echo PasteNotes.cs was not found.
    echo Put PasteNotes.cs.txt or PasteNotes.cs next to this Build.bat file.
    echo.
    goto :fail
)

set "FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319"
if not exist "%FW%\csc.exe" set "FW=%WINDIR%\Microsoft.NET\Framework\v4.0.30319"
if not exist "%FW%\csc.exe" (
    echo.
    echo The .NET Framework 4 C# compiler was not found.
    echo PasteNotes needs Windows 10 or 11 with .NET Framework 4.
    echo.
    goto :fail
)
if not exist "%FW%\System.Web.Extensions.dll" (
    echo.
    echo System.Web.Extensions.dll was not found in the .NET Framework folder.
    echo.
    goto :fail
)

if exist "%WORK%" rmdir /s /q "%WORK%" >nul 2>&1
mkdir "%WORK%" >nul 2>&1
copy /y "%SRC%" "%WORK%\PasteNotes.cs" >nul

echo Compiling PasteNotes...
"%FW%\csc.exe" /nologo /noconfig /target:winexe /platform:anycpu /langversion:5 /optimize+ /out:"%~dp0PasteNotes.new.exe" /reference:"%FW%\System.dll" /reference:"%FW%\System.Core.dll" /reference:"%FW%\System.Drawing.dll" /reference:"%FW%\System.Windows.Forms.dll" /reference:"%FW%\System.Web.Extensions.dll" "%WORK%\PasteNotes.cs"
if errorlevel 1 (
    echo.
    echo Compilation failed. The error messages are above.
    goto :fail
)

echo Running the built-in self-test...
if exist "%TEMP%\PasteNotesSelfTest.log" del /f /q "%TEMP%\PasteNotesSelfTest.log" >nul 2>&1
"%~dp0PasteNotes.new.exe" --selftest
if errorlevel 1 (
    echo.
    echo Self-test failed. Details:
    if exist "%TEMP%\PasteNotesSelfTest.log" type "%TEMP%\PasteNotesSelfTest.log"
    echo.
    goto :fail
)

move /y "%~dp0PasteNotes.new.exe" "%~dp0PasteNotes.exe" >nul
if errorlevel 1 (
    echo.
    echo PasteNotes.exe is in use. Close PasteNotes and run Build.bat again.
    echo.
    goto :fail
)
if not exist "%~dp0PasteNotes.portable" type nul > "%~dp0PasteNotes.portable"

rmdir /s /q "%WORK%" >nul 2>&1

echo.
echo Build complete: %~dp0PasteNotes.exe
echo Data will be kept in %~dp0PasteNotesData
echo.
if /i "%~1"=="--no-pause" goto :done
pause
goto :done

:fail
if exist "%~dp0PasteNotes.new.exe" del /f /q "%~dp0PasteNotes.new.exe" >nul 2>&1
if defined WORK if exist "%WORK%" rmdir /s /q "%WORK%" >nul 2>&1
echo.
if /i "%~1"=="--no-pause" exit /b 1
pause
exit /b 1

:done
endlocal
exit /b 0
"""


def strip_leading_usings(text: str) -> str:
    lines = text.splitlines()
    index = 0
    while index < len(lines):
        stripped = lines[index].strip()
        if stripped == "":
            index += 1
            continue
        if stripped.startswith("using ") and stripped.endswith(";"):
            index += 1
            continue
        break
    return "\n".join(lines[index:])


def build_merged_source() -> str:
    files = sorted(SOURCE_DIR.glob("*.cs"))
    if not files:
        raise RuntimeError(f"No C# source files found in {SOURCE_DIR}")

    parts = [HEADER]
    for path in files:
        text = path.read_text(encoding="utf-8-sig")
        if not path.name.startswith("01_"):
            text = strip_leading_usings(text)
        text = text.strip()
        parts.append(f"// ----- {path.name} -----\n{text}\n")
    return "\n".join(parts) + "\n"


def main() -> int:
    source = build_merged_source()
    source.encode("ascii")
    BUILD_BAT.encode("ascii")

    DELIVERY_DIR.mkdir(parents=True, exist_ok=True)
    source_path = DELIVERY_DIR / "PasteNotes.cs.txt"
    build_path = DELIVERY_DIR / "Build.bat.txt"

    source_path.write_text(source, encoding="ascii", newline="\r\n")
    build_path.write_text(BUILD_BAT, encoding="ascii", newline="\r\n")

    for path in (source_path, build_path):
        data = path.read_bytes()
        digest = hashlib.sha256(data).hexdigest()
        print(f"{path.name}: {len(data)} bytes, sha256 {digest}")
    print(f"Output: {DELIVERY_DIR}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
