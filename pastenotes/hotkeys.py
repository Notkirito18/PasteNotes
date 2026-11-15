from __future__ import annotations

import ctypes
import re
from ctypes import wintypes
from dataclasses import dataclass

from PySide6.QtCore import QObject, Qt, Signal
from PySide6.QtWidgets import QWidget

MOD_ALT = 0x0001
MOD_CONTROL = 0x0002
MOD_SHIFT = 0x0004
MOD_WIN = 0x0008
MOD_NOREPEAT = 0x4000

WM_HOTKEY = 0x0312
ERROR_HOTKEY_ALREADY_REGISTERED = 1409

_MODIFIERS = {
    "CTRL": MOD_CONTROL,
    "CONTROL": MOD_CONTROL,
    "ALT": MOD_ALT,
    "SHIFT": MOD_SHIFT,
    "WIN": MOD_WIN,
    "WINDOWS": MOD_WIN,
    "META": MOD_WIN,
    "SUPER": MOD_WIN,
}

_SPECIAL_KEYS: dict[str, tuple[int, str]] = {
    "BACKSPACE": (0x08, "Backspace"),
    "TAB": (0x09, "Tab"),
    "ENTER": (0x0D, "Enter"),
    "RETURN": (0x0D, "Enter"),
    "ESC": (0x1B, "Esc"),
    "ESCAPE": (0x1B, "Esc"),
    "SPACE": (0x20, "Space"),
    "PAGEUP": (0x21, "PgUp"),
    "PGUP": (0x21, "PgUp"),
    "PAGEDOWN": (0x22, "PgDown"),
    "PGDN": (0x22, "PgDown"),
    "END": (0x23, "End"),
    "HOME": (0x24, "Home"),
    "LEFT": (0x25, "Left"),
    "UP": (0x26, "Up"),
    "RIGHT": (0x27, "Right"),
    "DOWN": (0x28, "Down"),
    "INSERT": (0x2D, "Ins"),
    "INS": (0x2D, "Ins"),
    "DELETE": (0x2E, "Del"),
    "DEL": (0x2E, "Del"),
    "PRINT": (0x2C, "Print"),
    "PAUSE": (0x13, "Pause"),
    "MENU": (0x5D, "Menu"),
    "HELP": (0x2F, "Help"),
    "CAPSLOCK": (0x14, "CapsLock"),
    "NUMLOCK": (0x90, "NumLock"),
    "SCROLLLOCK": (0x91, "ScrollLock"),
}

_PUNCTUATION_KEYS: dict[str, tuple[int, str]] = {
    "-": (0xBD, "-"),
    "=": (0xBB, "="),
    "[": (0xDB, "["),
    "]": (0xDD, "]"),
    "\\": (0xDC, "\\"),
    ";": (0xBA, ";"),
    "'": (0xDE, "'"),
    ",": (0xBC, ","),
    ".": (0xBE, "."),
    "/": (0xBF, "/"),
    "`": (0xC0, "`"),
}

_KEY_ALIASES: dict[str, tuple[int, str]] = {
    **_SPECIAL_KEYS,
    **_PUNCTUATION_KEYS,
}


class HotkeyError(ValueError):
    pass


@dataclass(frozen=True)
class Hotkey:
    mods: int
    vk: int
    text: str


def _key_from_name(name: str) -> tuple[int, str]:
    raw = name.strip()
    upper = raw.upper()
    if upper in _KEY_ALIASES:
        return _KEY_ALIASES[upper]
    if len(raw) == 1:
        char = raw.upper()
        if "A" <= char <= "Z":
            return ord(char), char
        if "0" <= char <= "9":
            return ord(char), char
    if re.fullmatch(r"[Ff]\d{1,2}", raw):
        number = int(raw[1:])
        if 1 <= number <= 24:
            return 0x70 + number - 1, f"F{number}"
    raise HotkeyError(f"Unsupported key: {name}")


def format_hotkey(mods: int, key_display: str) -> str:
    parts = []
    if mods & MOD_CONTROL:
        parts.append("Ctrl")
    if mods & MOD_ALT:
        parts.append("Alt")
    if mods & MOD_SHIFT:
        parts.append("Shift")
    if mods & MOD_WIN:
        parts.append("Win")
    parts.append(key_display)
    return "+".join(parts)


def parse_hotkey(text: str) -> Hotkey:
    if not text or not text.strip():
        raise HotkeyError("No hotkey set")

    parts = [part.strip() for part in text.split("+")]
    parts = [part for part in parts if part]
    if not parts:
        raise HotkeyError("No hotkey set")

    mods = 0
    key_name: str | None = None
    for part in parts:
        upper = part.upper()
        if upper in _MODIFIERS:
            mods |= _MODIFIERS[upper]
            continue
        if key_name is not None:
            raise HotkeyError("Hotkey must contain exactly one key")
        key_name = part

    if key_name is None:
        raise HotkeyError("Hotkey must include a non-modifier key")

    vk, display = _key_from_name(key_name)
    if mods == 0 and not (0x70 <= vk <= 0x87):
        raise HotkeyError(
            "Use at least one modifier (Ctrl/Alt/Shift/Win) or an F-key"
        )
    return Hotkey(mods, vk, format_hotkey(mods, display))


def normalize_hotkey(text: str) -> str:
    return parse_hotkey(text).text


class _HotkeyHost(QWidget):
    hotkey_pressed = Signal(int)

    def __init__(self) -> None:
        super().__init__(None)
        self.setAttribute(Qt.WA_DontShowOnScreen, True)
        self.setWindowTitle("PasteNotes hotkey host")
        self.winId()

    def nativeEvent(self, event_type, message):
        try:
            msg = wintypes.MSG.from_address(int(message))
        except (TypeError, ValueError):
            return super().nativeEvent(event_type, message)
        if msg.message == WM_HOTKEY:
            self.hotkey_pressed.emit(int(msg.wParam))
            return True, 0
        return super().nativeEvent(event_type, message)


class HotkeyManager(QObject):
    hotkey_pressed = Signal(str)

    def __init__(self, parent=None) -> None:
        super().__init__(parent)
        self._user32 = ctypes.WinDLL("user32", use_last_error=True)
        self._user32.RegisterHotKey.argtypes = (
            wintypes.HWND,
            ctypes.c_int,
            wintypes.UINT,
            wintypes.UINT,
        )
        self._user32.RegisterHotKey.restype = wintypes.BOOL
        self._user32.UnregisterHotKey.argtypes = (wintypes.HWND, ctypes.c_int)
        self._user32.UnregisterHotKey.restype = wintypes.BOOL

        self._host = _HotkeyHost()
        self._host.hotkey_pressed.connect(self._dispatch)
        self._hwnd = int(self._host.winId())
        self._registered: dict[int, str] = {}
        self._next_id = 1

    def _dispatch(self, hotkey_id: int) -> None:
        group_id = self._registered.get(hotkey_id)
        if group_id is not None:
            self.hotkey_pressed.emit(group_id)

    def unregister_all(self) -> None:
        for hotkey_id in list(self._registered):
            self._user32.UnregisterHotKey(self._hwnd, hotkey_id)
        self._registered.clear()
        self._next_id = 1

    def apply(self, groups: list[tuple[str, str, str]]) -> dict[str, str | None]:
        self.unregister_all()
        results: dict[str, str | None] = {}
        used: dict[str, str] = {}

        for group_id, group_name, hotkey_text in groups:
            try:
                hotkey = parse_hotkey(hotkey_text)
            except HotkeyError as exc:
                results[group_id] = str(exc)
                continue

            if hotkey.text in used:
                results[group_id] = f"Conflicts with group '{used[hotkey.text]}'"
                continue
            used[hotkey.text] = group_name

            hotkey_id = self._next_id
            self._next_id += 1
            registered = self._user32.RegisterHotKey(
                self._hwnd,
                hotkey_id,
                hotkey.mods | MOD_NOREPEAT,
                hotkey.vk,
            )
            if not registered:
                error = ctypes.get_last_error()
                if error == ERROR_HOTKEY_ALREADY_REGISTERED:
                    results[group_id] = "Hotkey is already in use by another app"
                else:
                    results[group_id] = f"Could not register hotkey (error {error})"
                continue

            self._registered[hotkey_id] = group_id
            results[group_id] = None

        return results
