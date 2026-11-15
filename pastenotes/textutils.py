from __future__ import annotations

import unicodedata

from PySide6.QtCore import Qt
from PySide6.QtWidgets import QPlainTextEdit

_RTL_BIDI_CLASSES = {"R", "AL"}


def direction_for_text(text: str) -> Qt.LayoutDirection | None:
    for char in text:
        bidi_class = unicodedata.bidirectional(char)
        if bidi_class in _RTL_BIDI_CLASSES:
            return Qt.RightToLeft
        if bidi_class == "L":
            return Qt.LeftToRight
    return None


def is_rtl_text(text: str) -> bool:
    return direction_for_text(text) == Qt.RightToLeft


def text_alignment(text: str):
    if is_rtl_text(text):
        return Qt.AlignRight | Qt.AlignVCenter
    return Qt.AlignLeft | Qt.AlignVCenter


class AutoDirectionPlainTextEdit(QPlainTextEdit):
    def __init__(self, parent=None) -> None:
        super().__init__(parent)
        self._text_direction = Qt.LeftToRight
        self.textChanged.connect(self._update_direction)
        self.cursorPositionChanged.connect(self._update_direction)
        self._update_direction()

    def _detect_direction(self) -> Qt.LayoutDirection | None:
        cursor = self.textCursor()
        direction = direction_for_text(cursor.block().text())
        if direction is not None:
            return direction

        block = cursor.block().previous()
        while block.isValid():
            direction = direction_for_text(block.text())
            if direction is not None:
                return direction
            block = block.previous()

        block = self.document().firstBlock()
        while block.isValid():
            direction = direction_for_text(block.text())
            if direction is not None:
                return direction
            block = block.next()

        return None

    def _update_direction(self) -> None:
        direction = self._detect_direction()
        if direction is None or direction == self._text_direction:
            return
        self._text_direction = direction
        option = self.document().defaultTextOption()
        option.setTextDirection(direction)
        option.setAlignment(
            Qt.AlignRight if direction == Qt.RightToLeft else Qt.AlignLeft
        )
        self.document().setDefaultTextOption(option)
