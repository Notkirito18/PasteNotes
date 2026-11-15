import pytest

from pastenotes.hotkeys import (
    MOD_ALT,
    MOD_CONTROL,
    MOD_SHIFT,
    MOD_WIN,
    HotkeyError,
    parse_hotkey,
)


def test_parse_basic_combo():
    hotkey = parse_hotkey("Ctrl+Alt+1")
    assert hotkey.mods == MOD_CONTROL | MOD_ALT
    assert hotkey.vk == ord("1")
    assert hotkey.text == "Ctrl+Alt+1"


def test_normalizes_case_and_modifier_order():
    hotkey = parse_hotkey("alt+ctrl+shift+win+f5")
    assert hotkey.mods == MOD_CONTROL | MOD_ALT | MOD_SHIFT | MOD_WIN
    assert hotkey.vk == 0x74
    assert hotkey.text == "Ctrl+Alt+Shift+Win+F5"


def test_meta_is_win():
    assert parse_hotkey("Meta+E").mods == MOD_WIN


def test_alias_keys():
    assert parse_hotkey("Ctrl+Del").text == "Ctrl+Del"
    assert parse_hotkey("Ctrl+Delete").text == "Ctrl+Del"
    assert parse_hotkey("Ctrl+PgDn").text == "Ctrl+PgDown"
    assert parse_hotkey("Ctrl+Return").text == "Ctrl+Enter"


def test_function_key_without_modifier_is_allowed():
    assert parse_hotkey("F8").vk == 0x77


def test_letter_without_modifier_is_rejected():
    with pytest.raises(HotkeyError):
        parse_hotkey("A")


def test_modifier_only_is_rejected():
    with pytest.raises(HotkeyError):
        parse_hotkey("Ctrl+Alt")


def test_multiple_keys_are_rejected():
    with pytest.raises(HotkeyError):
        parse_hotkey("Ctrl+A+B")


def test_empty_is_rejected():
    with pytest.raises(HotkeyError):
        parse_hotkey("   ")


def test_unsupported_key_is_rejected():
    with pytest.raises(HotkeyError):
        parse_hotkey("Ctrl+Banana")


def test_punctuation_keys():
    assert parse_hotkey("Ctrl+-").vk == 0xBD
    assert parse_hotkey("Ctrl+Shift+/").text == "Ctrl+Shift+/"
