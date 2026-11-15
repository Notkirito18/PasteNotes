from PySide6.QtCore import Qt

from pastenotes.textutils import direction_for_text, is_rtl_text, text_alignment


def test_arabic_is_rtl():
    assert direction_for_text("مرحبا بالعالم") == Qt.RightToLeft
    assert is_rtl_text("مرحبا بالعالم")


def test_hebrew_is_rtl():
    assert direction_for_text("שלום") == Qt.RightToLeft


def test_english_is_ltr():
    assert direction_for_text("Hello world") == Qt.LeftToRight
    assert not is_rtl_text("Hello world")


def test_numbers_and_punctuation_are_neutral():
    assert direction_for_text("123 - !?") is None
    assert direction_for_text("") is None


def test_alignment_follows_direction():
    rtl_alignment = text_alignment("مرحبا")
    ltr_alignment = text_alignment("Hello")
    assert bool(rtl_alignment & Qt.AlignRight)
    assert bool(ltr_alignment & Qt.AlignLeft)
