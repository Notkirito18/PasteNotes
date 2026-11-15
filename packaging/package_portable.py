from __future__ import annotations

import os
import shutil
import subprocess
import sys
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
APP_DIR = ROOT / "dist" / "PasteNotes"
INTERNAL_DIR = APP_DIR / "_internal"
ZIP_PATH = ROOT / "PasteNotes_Portable.zip"
MAX_ZIP_BYTES = 24 * 1024 * 1024

REMOVE_FILES = (
    "PySide6/opengl32sw.dll",
    "PySide6/Qt6OpenGL.dll",
    "PySide6/Qt6Pdf.dll",
    "PySide6/Qt6Qml.dll",
    "PySide6/Qt6QmlMeta.dll",
    "PySide6/Qt6QmlModels.dll",
    "PySide6/Qt6QmlWorkerScript.dll",
    "PySide6/Qt6Quick.dll",
    "PySide6/Qt6Svg.dll",
    "PySide6/Qt6VirtualKeyboard.dll",
)

REMOVE_DIRS = (
    "PySide6/translations",
    "PySide6/plugins/imageformats",
    "PySide6/plugins/iconengines",
    "PySide6/plugins/networkinformation",
    "PySide6/plugins/tls",
    "PySide6/plugins/generic",
    "PySide6/plugins/platforminputcontexts",
)

REMOVE_PLATFORM_EXTRAS = ("qdirect2d.dll", "qminimal.dll", "qoffscreen.dll")

UPX_SKIP = {
    "python314.dll",
    "python3.dll",
    "vcruntime140.dll",
    "vcruntime140_1.dll",
    "msvcp140.dll",
    "msvcp140_1.dll",
    "msvcp140_2.dll",
    "qwindows.dll",
    "qmodernwindowsstyle.dll",
}


def find_upx() -> Path | None:
    configured = os.environ.get("UPX_EXE")
    if configured and Path(configured).is_file():
        return Path(configured)
    found = shutil.which("upx")
    if found:
        return Path(found)
    local_appdata = os.environ.get("LOCALAPPDATA")
    if local_appdata:
        packages = Path(local_appdata) / "Microsoft" / "WinGet" / "Packages"
        matches = sorted(packages.glob("UPX.UPX_*/upx-*/upx.exe"))
        if matches:
            return matches[-1]
    for env_name in ("ProgramFiles", "ProgramFiles(x86)"):
        base = os.environ.get(env_name)
        if base:
            candidate = Path(base) / "upx" / "upx.exe"
            if candidate.is_file():
                return candidate
    return None


def remove_path(path: Path) -> None:
    if path.is_dir():
        shutil.rmtree(path)
    elif path.exists():
        path.unlink()


def upx_targets() -> list[Path]:
    targets = []
    for path in INTERNAL_DIR.rglob("*"):
        if not path.is_file() or path.suffix.lower() not in {".dll", ".pyd"}:
            continue
        if path.name.lower() in UPX_SKIP:
            continue
        targets.append(path)
    return sorted(targets)


def create_zip() -> None:
    if ZIP_PATH.exists():
        ZIP_PATH.unlink()
    with zipfile.ZipFile(ZIP_PATH, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for path in sorted(APP_DIR.rglob("*")):
            if path.is_file():
                archive.write(path, Path("PasteNotes") / path.relative_to(APP_DIR))


def main() -> int:
    if not (APP_DIR / "PasteNotes.exe").is_file():
        print(f"Build not found: {APP_DIR / 'PasteNotes.exe'}", file=sys.stderr)
        return 1

    for relative in REMOVE_FILES:
        remove_path(INTERNAL_DIR / relative)
    for relative in REMOVE_DIRS:
        remove_path(INTERNAL_DIR / relative)
    platforms = INTERNAL_DIR / "PySide6" / "plugins" / "platforms"
    platforms.mkdir(parents=True, exist_ok=True)
    for name in REMOVE_PLATFORM_EXTRAS:
        remove_path(platforms / name)

    upx = find_upx()
    if upx is None:
        print(
            "UPX is required for the 24 MB portable build but was not found.\n"
            "Install it with: winget install --id UPX.UPX -e",
            file=sys.stderr,
        )
        return 1

    failures: list[tuple[Path, str]] = []
    targets = upx_targets()
    for index, path in enumerate(targets, start=1):
        result = subprocess.run(
            [str(upx), "--best", "--lzma", "--force", "--quiet", str(path)],
            capture_output=True,
            text=True,
        )
        if result.returncode != 0:
            message = (result.stderr or result.stdout).strip().splitlines()
            failures.append((path, message[-1] if message else f"exit {result.returncode}"))
        if index % 10 == 0:
            print(f"  compressed {index}/{len(targets)}")

    remove_path(APP_DIR / "PasteNotesData")
    shutil.copyfile(ROOT / "packaging" / "README_Portable.txt", APP_DIR / "README.txt")
    (APP_DIR / "PasteNotes.portable").write_text("", encoding="utf-8")
    create_zip()

    size = ZIP_PATH.stat().st_size
    if failures:
        print("UPX warnings:", file=sys.stderr)
        for path, message in failures:
            print(f"  {path.relative_to(INTERNAL_DIR)}: {message}", file=sys.stderr)

    print(f"Portable folder: {APP_DIR}")
    print(f"Portable zip:    {ZIP_PATH}")
    print(f"Zip size:        {size / (1024 * 1024):.2f} MB")
    if size > MAX_ZIP_BYTES:
        print("ERROR: ZIP exceeds the 24 MB limit.", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
