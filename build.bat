@echo off
setlocal
cd /d "%~dp0"

echo [1/6] Installing or updating PyInstaller...
py -m pip install --disable-pip-version-check --upgrade "pyinstaller>=6.16"
if errorlevel 1 goto :error

echo [2/6] Cleaning previous output...
if exist build rmdir /s /q build
if exist dist rmdir /s /q dist
if exist PasteNotes_Portable.zip del /q PasteNotes_Portable.zip

echo [3/6] Checking UPX...
where upx >nul 2>&1
if errorlevel 1 (
    echo UPX not found; installing it with winget...
    winget install --id UPX.UPX -e --accept-source-agreements --accept-package-agreements --silent
    if errorlevel 1 echo WARNING: UPX installation failed. The 24 MB packaging step may fail.
)

echo [4/6] Building PasteNotes.exe...
if not exist "packaging\PasteNotes.ico" py packaging\make_icon.py
if errorlevel 1 goto :error
py -m PyInstaller --noconfirm --clean --noupx --onedir --windowed --name PasteNotes --icon "packaging\PasteNotes.ico" launcher.py
if errorlevel 1 goto :error

echo [5/6] Trimming, compressing, and packaging...
py packaging\package_portable.py
if errorlevel 1 goto :error

echo [6/6] Build complete.
echo   Folder: dist\PasteNotes
echo   Zip:    PasteNotes_Portable.zip
exit /b 0

:error
echo.
echo Build failed.
exit /b 1
