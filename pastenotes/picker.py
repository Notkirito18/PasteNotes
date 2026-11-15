from __future__ import annotations

from PySide6.QtCore import QEvent, Qt, Signal
from PySide6.QtGui import QCursor, QGuiApplication
from PySide6.QtWidgets import (
    QFrame,
    QLabel,
    QLineEdit,
    QListWidget,
    QListWidgetItem,
    QVBoxLayout,
    QWidget,
)

from .colors import TEXT_ON_GROUP_COLOR, color_hex
from .paste import force_foreground
from .storage import Group
from .textutils import text_alignment


def _style(color_key: str) -> str:
    accent = color_hex(color_key)
    on_accent = TEXT_ON_GROUP_COLOR
    return f"""
QFrame#container {{
    background-color: #1e2330;
    border: 1px solid {accent};
    border-radius: 10px;
}}
QLabel#header {{
    background-color: {accent};
    color: {on_accent};
    font-size: 14px;
    font-weight: 600;
    padding: 12px 14px;
    border-top-left-radius: 9px;
    border-top-right-radius: 9px;
}}
QLabel#hint {{
    color: #8d96a8;
    font-size: 11px;
    padding: 6px 14px 10px 14px;
}}
QLineEdit {{
    background-color: #121620;
    color: #e8ebf2;
    border: 1px solid #343d51;
    border-radius: 6px;
    padding: 7px 9px;
    margin: 8px 12px 8px 12px;
    selection-background-color: {accent};
    selection-color: {on_accent};
}}
QLineEdit:focus {{
    border-color: {accent};
}}
QListWidget {{
    background: transparent;
    color: #d7dce6;
    border: none;
    margin: 0 8px 0 8px;
    outline: none;
}}
QListWidget::item {{
    padding: 9px 10px;
    border-radius: 6px;
}}
QListWidget::item:hover {{
    background-color: #2a3142;
}}
QListWidget::item:selected {{
    background-color: {accent};
    color: {on_accent};
}}
"""


class Picker(QWidget):
    selected = Signal(str)
    cancelled = Signal(bool)

    def __init__(self, group: Group, parent=None) -> None:
        super().__init__(parent, Qt.Popup | Qt.FramelessWindowHint)
        self.setAttribute(Qt.WA_TranslucentBackground)
        self.setWindowTitle("PasteNotes")
        self._group = group
        self._finished = False
        self._build()
        self._populate()
        self.resize(480, 440)
        self.setStyleSheet(_style(self._group.color))

    def _build(self) -> None:
        outer = QVBoxLayout(self)
        outer.setContentsMargins(0, 0, 0, 0)

        container = QFrame()
        container.setObjectName("container")
        outer.addWidget(container)

        layout = QVBoxLayout(container)
        layout.setContentsMargins(0, 0, 0, 0)
        layout.setSpacing(0)

        header = QLabel(self._group.name)
        header.setObjectName("header")
        header.setAlignment(text_alignment(self._group.name))
        layout.addWidget(header)

        self.search = QLineEdit()
        self.search.setPlaceholderText("Search templates...")
        self.search.installEventFilter(self)
        self.search.textChanged.connect(self._apply_filter)
        layout.addWidget(self.search)

        self.list = QListWidget()
        self.list.setFocusPolicy(Qt.NoFocus)
        self.list.itemClicked.connect(self._accept_item)
        layout.addWidget(self.list, 1)

        hint = QLabel("Type to search  ·  ↑ ↓ move  ·  Enter paste  ·  Esc cancel")
        hint.setObjectName("hint")
        layout.addWidget(hint)

    def _populate(self) -> None:
        self.list.clear()
        if not self._group.templates:
            item = QListWidgetItem("No templates in this group")
            item.setFlags(Qt.NoItemFlags)
            self.list.addItem(item)
            return
        for template in self._group.templates:
            title = template.title or "Untitled template"
            item = QListWidgetItem(title)
            item.setData(Qt.UserRole, template)
            item.setTextAlignment(text_alignment(title))
            preview = template.content.strip().replace("\r", " ").replace("\n", " ")
            if preview:
                item.setToolTip(preview[:800])
            self.list.addItem(item)
        self.list.setCurrentRow(0)

    def open_at_cursor(self) -> None:
        position = QCursor.pos()
        screen = QGuiApplication.screenAt(position) or QGuiApplication.primaryScreen()
        area = screen.availableGeometry()
        width, height = self.width(), self.height()

        x = position.x() + 14
        y = position.y() + 18
        if x + width > area.right():
            x = area.right() - width
        if y + height > area.bottom():
            y = position.y() - height - 14
        x = max(area.left(), min(x, area.right() - width))
        y = max(area.top(), min(y, area.bottom() - height))

        self.move(x, y)
        self.show()
        self.raise_()
        self.activateWindow()
        self.search.setFocus()
        self.search.selectAll()
        force_foreground(int(self.winId()))

    def eventFilter(self, obj, event) -> bool:
        if obj is self.search and event.type() == QEvent.KeyPress:
            key = event.key()
            if key in (Qt.Key_Up, Qt.Key_Down):
                self._move_current(1 if key == Qt.Key_Down else -1)
                return True
            if key == Qt.Key_PageDown:
                self._move_current(8)
                return True
            if key == Qt.Key_PageUp:
                self._move_current(-8)
                return True
            if key in (Qt.Key_Return, Qt.Key_Enter):
                self._accept_item(self.list.currentItem())
                return True
            if key == Qt.Key_Escape:
                self._cancel(refocus=True)
                return True
        return super().eventFilter(obj, event)

    def _visible_rows(self) -> list[int]:
        return [
            row
            for row in range(self.list.count())
            if not self.list.item(row).isHidden()
        ]

    def _move_current(self, delta: int) -> None:
        rows = self._visible_rows()
        if not rows:
            return
        current = self.list.currentRow()
        if current not in rows:
            self.list.setCurrentRow(rows[0])
            return
        index = rows.index(current)
        target = max(0, min(len(rows) - 1, index + delta))
        self.list.setCurrentRow(rows[target])

    def _apply_filter(self, text: str) -> None:
        query = text.strip().lower()
        first_visible: int | None = None
        for row in range(self.list.count()):
            item = self.list.item(row)
            template = item.data(Qt.UserRole)
            if template is None:
                item.setHidden(bool(query))
                continue
            haystack = f"{template.title}\n{template.content}".lower()
            hidden = bool(query) and query not in haystack
            item.setHidden(hidden)
            if not hidden and first_visible is None:
                first_visible = row
        if first_visible is None:
            self.list.clearSelection()
            self.list.setCurrentItem(None)
        else:
            self.list.setCurrentRow(first_visible)

    def _accept_item(self, item: QListWidgetItem | None) -> None:
        if self._finished or item is None:
            return
        template = item.data(Qt.UserRole)
        if template is None:
            return
        self._finished = True
        self.hide()
        self.selected.emit(template.content)

    def _cancel(self, refocus: bool) -> None:
        if self._finished:
            return
        self._finished = True
        self.hide()
        self.cancelled.emit(refocus)

    def hideEvent(self, event) -> None:
        super().hideEvent(event)
        if not self._finished:
            self._finished = True
            self.cancelled.emit(False)
