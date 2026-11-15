from __future__ import annotations

import ctypes
import logging
import time
from ctypes import wintypes

import win32clipboard

log = logging.getLogger(__name__)

user32 = ctypes.WinDLL("user32", use_last_error=True)
kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)

user32.GetForegroundWindow.restype = wintypes.HWND
user32.IsWindow.argtypes = (wintypes.HWND,)
user32.IsWindow.restype = wintypes.BOOL
user32.SetForegroundWindow.argtypes = (wintypes.HWND,)
user32.SetForegroundWindow.restype = wintypes.BOOL
user32.BringWindowToTop.argtypes = (wintypes.HWND,)
user32.BringWindowToTop.restype = wintypes.BOOL
user32.GetWindowThreadProcessId.argtypes = (
    wintypes.HWND,
    ctypes.POINTER(wintypes.DWORD),
)
user32.GetWindowThreadProcessId.restype = wintypes.DWORD
user32.AttachThreadInput.argtypes = (wintypes.DWORD, wintypes.DWORD, wintypes.BOOL)
user32.AttachThreadInput.restype = wintypes.BOOL
user32.SwitchToThisWindow.argtypes = (wintypes.HWND, wintypes.BOOL)
user32.SwitchToThisWindow.restype = None
kernel32.GetCurrentThreadId.restype = wintypes.DWORD

_VK_CONTROL = 0x11
_VK_V = 0x56
_KEYEVENTF_KEYUP = 0x0002
_INPUT_KEYBOARD = 1
_ULONG_PTR = ctypes.c_size_t

_CLIPBOARD_OPEN_RETRIES = 10
_CLIPBOARD_RETRY_DELAY = 0.02
_CLIPBOARD_RESTORE_DELAY = 0.35

_SKIP_CLIPBOARD_FORMATS = {
    2,  # CF_BITMAP, handle based
    3,  # CF_METAFILEPICT, handle based
    9,  # CF_PALETTE, handle based
    0x0080,  # CF_OWNERDISPLAY
    0x0081,  # CF_DSPTEXT
    0x0082,  # CF_DSPBITMAP
    0x0083,  # CF_DSPMETAFILEPICT
    0x008E,  # CF_DSPENHMETAFILE
}


class KEYBDINPUT(ctypes.Structure):
    _fields_ = [
        ("wVk", wintypes.WORD),
        ("wScan", wintypes.WORD),
        ("dwFlags", wintypes.DWORD),
        ("time", wintypes.DWORD),
        ("dwExtraInfo", _ULONG_PTR),
    ]


class MOUSEINPUT(ctypes.Structure):
    _fields_ = [
        ("dx", wintypes.LONG),
        ("dy", wintypes.LONG),
        ("mouseData", wintypes.DWORD),
        ("dwFlags", wintypes.DWORD),
        ("time", wintypes.DWORD),
        ("dwExtraInfo", _ULONG_PTR),
    ]


class HARDWAREINPUT(ctypes.Structure):
    _fields_ = [
        ("uMsg", wintypes.DWORD),
        ("wParamL", wintypes.WORD),
        ("wParamH", wintypes.WORD),
    ]


class INPUT(ctypes.Structure):
    class _INPUT_UNION(ctypes.Union):
        _fields_ = [
            ("mi", MOUSEINPUT),
            ("ki", KEYBDINPUT),
            ("hi", HARDWAREINPUT),
        ]

    _anonymous_ = ("input",)
    _fields_ = [("type", wintypes.DWORD), ("input", _INPUT_UNION)]


user32.SendInput.argtypes = (
    wintypes.UINT,
    ctypes.POINTER(INPUT),
    ctypes.c_int,
)
user32.SendInput.restype = wintypes.UINT


def get_foreground_window() -> int:
    return int(user32.GetForegroundWindow() or 0)


def is_window(hwnd: int) -> bool:
    return bool(hwnd) and bool(user32.IsWindow(hwnd))


def force_foreground(hwnd: int) -> bool:
    if not is_window(hwnd):
        return False
    if get_foreground_window() == hwnd:
        return True

    foreground = get_foreground_window()
    current_thread = kernel32.GetCurrentThreadId()
    foreground_thread = (
        user32.GetWindowThreadProcessId(foreground, None) if foreground else 0
    )
    target_thread = user32.GetWindowThreadProcessId(hwnd, None)

    attached: list[int] = []
    for other_thread in {foreground_thread, target_thread}:
        if other_thread and other_thread != current_thread:
            if user32.AttachThreadInput(current_thread, other_thread, True):
                attached.append(other_thread)

    try:
        user32.SetForegroundWindow(hwnd)
        user32.BringWindowToTop(hwnd)
    finally:
        for other_thread in attached:
            user32.AttachThreadInput(current_thread, other_thread, False)

    if get_foreground_window() != hwnd:
        try:
            user32.SwitchToThisWindow(hwnd, True)
        except (AttributeError, ctypes.ArgumentError):
            pass

    return get_foreground_window() == hwnd


def _open_clipboard() -> bool:
    for _ in range(_CLIPBOARD_OPEN_RETRIES):
        try:
            win32clipboard.OpenClipboard()
            return True
        except Exception:
            time.sleep(_CLIPBOARD_RETRY_DELAY)
    return False


def _close_clipboard() -> None:
    try:
        win32clipboard.CloseClipboard()
    except Exception:
        log.debug("Clipboard was already closed", exc_info=True)


def snapshot_clipboard() -> dict[int, object] | None:
    if not _open_clipboard():
        return None
    try:
        snapshot: dict[int, object] = {}
        fmt = 0
        while True:
            fmt = win32clipboard.EnumClipboardFormats(fmt)
            if fmt == 0:
                break
            if fmt in _SKIP_CLIPBOARD_FORMATS:
                continue
            try:
                snapshot[fmt] = win32clipboard.GetClipboardData(fmt)
            except Exception:
                continue
        return snapshot
    except Exception:
        log.warning("Could not snapshot the clipboard", exc_info=True)
        return None
    finally:
        _close_clipboard()


def set_clipboard_text(text: str) -> bool:
    if not _open_clipboard():
        return False
    try:
        win32clipboard.EmptyClipboard()
        win32clipboard.SetClipboardData(win32clipboard.CF_UNICODETEXT, text)
        return True
    except Exception:
        log.warning("Could not write text to the clipboard", exc_info=True)
        return False
    finally:
        _close_clipboard()


def restore_clipboard(snapshot: dict[int, object] | None) -> None:
    if snapshot is None:
        return
    if not _open_clipboard():
        log.warning("Could not reopen the clipboard to restore it")
        return
    try:
        win32clipboard.EmptyClipboard()
        for fmt, data in snapshot.items():
            try:
                win32clipboard.SetClipboardData(fmt, data)
            except Exception:
                continue
    except Exception:
        log.warning("Could not restore the clipboard", exc_info=True)
    finally:
        _close_clipboard()


def _key_event(vk: int, keyup: bool = False) -> INPUT:
    event = INPUT()
    event.type = _INPUT_KEYBOARD
    event.ki.wVk = vk
    event.ki.dwFlags = _KEYEVENTF_KEYUP if keyup else 0
    return event


def send_paste() -> bool:
    events = (
        _key_event(_VK_CONTROL),
        _key_event(_VK_V),
        _key_event(_VK_V, keyup=True),
        _key_event(_VK_CONTROL, keyup=True),
    )
    buffer = (INPUT * len(events))(*events)
    sent = user32.SendInput(len(events), buffer, ctypes.sizeof(INPUT))
    return sent == len(events)


def paste_text(text: str, target_hwnd: int = 0, restore_previous: bool = True) -> bool:
    snapshot = snapshot_clipboard() if restore_previous else None
    can_restore = restore_previous and snapshot is not None

    if not set_clipboard_text(text):
        return False

    pasted = False
    try:
        if target_hwnd and is_window(target_hwnd):
            if not force_foreground(target_hwnd):
                log.warning("Could not restore focus to the target window")
            time.sleep(0.06)
        pasted = send_paste()
        if not pasted:
            log.warning("SendInput failed while sending Ctrl+V")
        return pasted
    finally:
        if can_restore:
            if pasted:
                time.sleep(_CLIPBOARD_RESTORE_DELAY)
            restore_clipboard(snapshot)
