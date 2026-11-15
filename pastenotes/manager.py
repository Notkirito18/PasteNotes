from __future__ import annotations

import logging

from PySide6.QtCore import Qt, QTimer, Signal
from PySide6.QtGui import QColor, QIcon, QKeySequence, QPainter, QPixmap
from PySide6.QtWidgets import (
    QCheckBox,
    QHBoxLayout,
    QInputDialog,
    QKeySequenceEdit,
    QLabel,
    QLineEdit,
    QListWidget,
    QListWidgetItem,
    QMainWindow,
    QMessageBox,
    QPushButton,
    QSplitter,
    QVBoxLayout,
    QWidget,
)

from .colors import GROUP_COLORS, color_hex, color_label, normalize_color
from .storage import DataStore, Group, Template
from .textutils import AutoDirectionPlainTextEdit, text_alignment

log = logging.getLogger(__name__)

_COLOR_ICON_CACHE: dict[str, QIcon] = {}


def _color_icon(color_key: str) -> QIcon:
    cached = _COLOR_ICON_CACHE.get(color_key)
    if cached is not None:
        return cached
    pixmap = QPixmap(20, 20)
    pixmap.fill(Qt.transparent)
    painter = QPainter(pixmap)
    painter.setRenderHint(QPainter.Antialiasing)
    painter.setPen(Qt.NoPen)
    painter.setBrush(QColor(color_hex(color_key)))
    painter.drawRoundedRect(2, 2, 16, 16, 5, 5)
    painter.end()
    icon = QIcon(pixmap)
    _COLOR_ICON_CACHE[color_key] = icon
    return icon


class ManagerWindow(QMainWindow):
    hotkeys_changed = Signal()

    def __init__(self, store: DataStore, parent=None) -> None:
        super().__init__(parent)
        self.store = store
        self._group: Group | None = None
        self._template: Template | None = None
        self._loading = False
        self._statuses: dict[str, str | None] = {}

        self._save_timer = QTimer(self)
        self._save_timer.setSingleShot(True)
        self._save_timer.setInterval(400)
        self._save_timer.timeout.connect(self._save_now)

        self._hotkey_timer = QTimer(self)
        self._hotkey_timer.setSingleShot(True)
        self._hotkey_timer.setInterval(250)
        self._hotkey_timer.timeout.connect(self.hotkeys_changed.emit)

        self._build()
        self.load_settings_ui()
        self._reload_groups(self.store.groups[0].id if self.store.groups else None)

    def _build(self) -> None:
        self.setWindowTitle("PasteNotes")
        self.resize(940, 640)

        central = QWidget()
        self.setCentralWidget(central)
        root = QHBoxLayout(central)
        root.setContentsMargins(10, 10, 10, 10)

        splitter = QSplitter(Qt.Horizontal)
        root.addWidget(splitter)
        splitter.addWidget(self._build_group_panel())
        splitter.addWidget(self._build_template_panel())
        splitter.setStretchFactor(0, 0)
        splitter.setStretchFactor(1, 1)
        splitter.setSizes([260, 680])

        self.statusBar().showMessage("Ready")

    def _build_group_panel(self) -> QWidget:
        panel = QWidget()
        layout = QVBoxLayout(panel)
        layout.setContentsMargins(0, 0, 8, 0)
        layout.addWidget(QLabel("Groups"))

        self.group_list = QListWidget()
        self.group_list.currentItemChanged.connect(self._on_group_selected)
        layout.addWidget(self.group_list, 1)

        first_row = QHBoxLayout()
        add = QPushButton("Add")
        add.clicked.connect(self._add_group)
        rename = QPushButton("Rename")
        rename.clicked.connect(self._rename_group)
        remove = QPushButton("Delete")
        remove.clicked.connect(self._delete_group)
        first_row.addWidget(add)
        first_row.addWidget(rename)
        first_row.addWidget(remove)
        layout.addLayout(first_row)

        second_row = QHBoxLayout()
        move_up = QPushButton("Move up")
        move_up.clicked.connect(lambda: self._move_group(-1))
        move_down = QPushButton("Move down")
        move_down.clicked.connect(lambda: self._move_group(1))
        second_row.addWidget(move_up)
        second_row.addWidget(move_down)
        layout.addLayout(second_row)

        return panel

    def _build_template_panel(self) -> QWidget:
        panel = QWidget()
        layout = QVBoxLayout(panel)
        layout.setContentsMargins(8, 0, 0, 0)

        color_row = QHBoxLayout()
        color_row.addWidget(QLabel("Group color:"))
        self._color_buttons: dict[str, QPushButton] = {}
        for key, hex_value in GROUP_COLORS:
            button = QPushButton()
            button.setCheckable(True)
            button.setFixedSize(24, 24)
            button.setToolTip(color_label(key))
            button.setStyleSheet(
                "QPushButton {"
                f"background-color: {hex_value};"
                "border: 1px solid #4a5265;"
                "border-radius: 12px;"
                "}"
                "QPushButton:checked { border: 2px solid #ffffff; }"
                "QPushButton:hover { border: 2px solid #c9cfdb; }"
            )
            button.clicked.connect(
                lambda checked=False, color=key: self._set_group_color(color)
            )
            self._color_buttons[key] = button
            color_row.addWidget(button)
        color_row.addStretch(1)
        layout.addLayout(color_row)

        hotkey_row = QHBoxLayout()
        hotkey_row.addWidget(QLabel("Group hotkey:"))
        self.hotkey_edit = QKeySequenceEdit()
        try:
            self.hotkey_edit.setMaximumSequenceLength(1)
        except AttributeError:
            pass
        self.hotkey_edit.keySequenceChanged.connect(self._on_hotkey_changed)
        hotkey_row.addWidget(self.hotkey_edit, 1)
        clear_hotkey = QPushButton("Clear")
        clear_hotkey.clicked.connect(lambda: self.hotkey_edit.clear())
        hotkey_row.addWidget(clear_hotkey)
        self.hotkey_status = QLabel("")
        hotkey_row.addWidget(self.hotkey_status)
        layout.addLayout(hotkey_row)

        layout.addWidget(QLabel("Templates"))
        self.template_list = QListWidget()
        self.template_list.currentItemChanged.connect(self._on_template_selected)
        layout.addWidget(self.template_list, 1)

        buttons = QHBoxLayout()
        add = QPushButton("Add")
        add.clicked.connect(self._add_template)
        duplicate = QPushButton("Duplicate")
        duplicate.clicked.connect(self._duplicate_template)
        remove = QPushButton("Delete")
        remove.clicked.connect(self._delete_template)
        move_up = QPushButton("Move up")
        move_up.clicked.connect(lambda: self._move_template(-1))
        move_down = QPushButton("Move down")
        move_down.clicked.connect(lambda: self._move_template(1))
        buttons.addWidget(add)
        buttons.addWidget(duplicate)
        buttons.addWidget(remove)
        buttons.addWidget(move_up)
        buttons.addWidget(move_down)
        buttons.addStretch(1)
        layout.addLayout(buttons)

        layout.addWidget(QLabel("Title"))
        self.title_edit = QLineEdit()
        self.title_edit.textChanged.connect(self._on_title_changed)
        layout.addWidget(self.title_edit)

        layout.addWidget(QLabel("Content"))
        self.content_edit = AutoDirectionPlainTextEdit()
        self.content_edit.textChanged.connect(self._on_content_changed)
        layout.addWidget(self.content_edit, 2)

        bottom = QHBoxLayout()
        self.restore_check = QCheckBox("Restore previous clipboard after pasting")
        self.restore_check.toggled.connect(self._on_restore_toggled)
        bottom.addWidget(self.restore_check)
        bottom.addStretch(1)
        self.saved_label = QLabel("All changes saved")
        self.saved_label.setStyleSheet("color: #6b7280;")
        bottom.addWidget(self.saved_label)
        layout.addLayout(bottom)

        return panel

    def load_settings_ui(self) -> None:
        self._loading = True
        self.restore_check.setChecked(
            bool(self.store.settings.get("restore_clipboard", True))
        )
        self._loading = False

    def _reload_groups(self, select_id: str | None = None) -> None:
        self._loading = True
        self.group_list.clear()
        for group in self.store.groups:
            item = QListWidgetItem(group.name)
            item.setData(Qt.UserRole, group.id)
            item.setIcon(_color_icon(group.color))
            item.setTextAlignment(text_alignment(group.name))
            self.group_list.addItem(item)
        self._loading = False

        if not self.store.groups:
            self._group = None
            self._template = None
            self.hotkey_edit.blockSignals(True)
            self.hotkey_edit.clear()
            self.hotkey_edit.blockSignals(False)
            self.hotkey_edit.setEnabled(False)
            self._reload_templates()
            self._refresh_hotkey_status()
            self._refresh_color_buttons()
            return

        row = 0
        if select_id is not None:
            for index in range(self.group_list.count()):
                if self.group_list.item(index).data(Qt.UserRole) == select_id:
                    row = index
                    break
        self.group_list.setCurrentRow(row)

    def _on_group_selected(self, current: QListWidgetItem | None, previous) -> None:
        if self._loading or current is None:
            return
        group_id = current.data(Qt.UserRole)
        self._group = self.store.find_group(group_id)
        self._loading = True
        if self._group is None:
            self._template = None
            self.template_list.clear()
            self.title_edit.clear()
            self.content_edit.clear()
            self.hotkey_edit.clear()
            self.hotkey_edit.setEnabled(False)
        else:
            self.hotkey_edit.setEnabled(True)
            sequence = QKeySequence()
            if self._group.hotkey:
                try:
                    sequence = QKeySequence(self._group.hotkey)
                except (TypeError, ValueError):
                    sequence = QKeySequence()
            self.hotkey_edit.setKeySequence(sequence)
            self._reload_templates()
        self._loading = False
        self._refresh_hotkey_status()
        self._refresh_color_buttons()

    def _reload_templates(self, select_id: str | None = None) -> None:
        self._loading = True
        self.template_list.clear()
        templates = self._group.templates if self._group else []
        for template in templates:
            title = template.title or "Untitled template"
            item = QListWidgetItem(title)
            item.setData(Qt.UserRole, template.id)
            item.setTextAlignment(text_alignment(title))
            self.template_list.addItem(item)
        self._loading = False

        if not templates:
            self._template = None
            self.title_edit.clear()
            self.content_edit.clear()
            self.title_edit.setEnabled(False)
            self.content_edit.setEnabled(False)
            return

        self.title_edit.setEnabled(True)
        self.content_edit.setEnabled(True)
        row = 0
        if select_id is not None:
            for index in range(self.template_list.count()):
                if self.template_list.item(index).data(Qt.UserRole) == select_id:
                    row = index
                    break
        self.template_list.setCurrentRow(row)

    def _on_template_selected(self, current: QListWidgetItem | None, previous) -> None:
        if self._loading or current is None or self._group is None:
            return
        template_id = current.data(Qt.UserRole)
        self._template = self.store.find_template(self._group.id, template_id)
        self._loading = True
        if self._template is not None:
            self.title_edit.setText(self._template.title)
            self.content_edit.setPlainText(self._template.content)
        else:
            self.title_edit.clear()
            self.content_edit.clear()
        self._loading = False

    def _on_title_changed(self, text: str) -> None:
        if self._loading or self._template is None:
            return
        self._template.title = text
        self._update_template_item(self._template)
        self._schedule_save()

    def _on_content_changed(self) -> None:
        if self._loading or self._template is None:
            return
        self._template.content = self.content_edit.toPlainText()
        self._schedule_save()

    def _update_template_item(self, template: Template) -> None:
        title = template.title or "Untitled template"
        for index in range(self.template_list.count()):
            item = self.template_list.item(index)
            if item.data(Qt.UserRole) == template.id:
                item.setText(title)
                item.setTextAlignment(text_alignment(title))
                break

    def _schedule_save(self) -> None:
        self.saved_label.setText("Saving...")
        self.saved_label.setStyleSheet("color: #b45309;")
        self._save_timer.start()

    def _save_now(self) -> None:
        try:
            self.store.save()
        except OSError:
            log.exception("Could not save PasteNotes data")
            self.saved_label.setText("Save failed")
            self.saved_label.setStyleSheet("color: #b91c1c; font-weight: 600;")
            return
        self.saved_label.setText("All changes saved")
        self.saved_label.setStyleSheet("color: #6b7280;")

    def flush_save(self) -> None:
        if self._save_timer.isActive():
            self._save_timer.stop()
        self._save_now()

    def set_hotkey_statuses(self, statuses: dict[str, str | None]) -> None:
        self._statuses = statuses
        self._refresh_hotkey_status()

    def _refresh_hotkey_status(self) -> None:
        if self._group is None:
            self.hotkey_status.clear()
            self.hotkey_status.setStyleSheet("")
            return
        message = self._statuses.get(self._group.id)
        if self._group.id in self._statuses and message is None:
            self.hotkey_status.setText("Active")
            self.hotkey_status.setStyleSheet("color: #2e7d32; font-weight: 600;")
            self.hotkey_status.setToolTip("")
        elif message is None:
            self.hotkey_status.clear()
            self.hotkey_status.setStyleSheet("")
        elif message == "No hotkey set":
            self.hotkey_status.setText("Not set")
            self.hotkey_status.setStyleSheet("color: #b45309;")
        elif message == "Paused":
            self.hotkey_status.setText("Paused")
            self.hotkey_status.setStyleSheet("color: #6b7280;")
        else:
            self.hotkey_status.setText(message)
            self.hotkey_status.setStyleSheet("color: #b91c1c; font-weight: 600;")
            self.hotkey_status.setToolTip(message)

    def _on_hotkey_changed(self, sequence: QKeySequence) -> None:
        if self._loading or self._group is None:
            return
        text = sequence.toString(QKeySequence.PortableText).replace("Meta+", "Win+")
        self._group.hotkey = text
        self._schedule_save()
        self.hotkey_status.setText("Checking...")
        self.hotkey_status.setStyleSheet("color: #6b7280;")
        self._hotkey_timer.start()

    def _on_restore_toggled(self, checked: bool) -> None:
        if self._loading:
            return
        self.store.settings["restore_clipboard"] = bool(checked)
        self._schedule_save()

    def _refresh_color_buttons(self) -> None:
        if self._group is None:
            for button in self._color_buttons.values():
                button.setEnabled(False)
            return
        selected = normalize_color(self._group.color)
        for key, button in self._color_buttons.items():
            button.setEnabled(True)
            button.setChecked(key == selected)

    def _set_group_color(self, color: str) -> None:
        if self._loading or self._group is None:
            return
        self._group.color = normalize_color(color)
        self._update_group_item(self._group)
        self._refresh_color_buttons()
        self._schedule_save()

    def _update_group_item(self, group: Group) -> None:
        for index in range(self.group_list.count()):
            item = self.group_list.item(index)
            if item.data(Qt.UserRole) == group.id:
                item.setIcon(_color_icon(group.color))
                item.setTextAlignment(text_alignment(group.name))
                break

    def _add_group(self) -> None:
        name, accepted = QInputDialog.getText(
            self,
            "New group",
            "Group name:",
            text=self.store.unique_group_name(),
        )
        if not accepted:
            return
        name = name.strip() or self.store.unique_group_name()
        group = self.store.add_group(name)
        self._reload_groups(group.id)
        self._schedule_save()
        self.hotkeys_changed.emit()
        self.statusBar().showMessage(
            f"Added group '{group.name}' - set a hotkey for it", 5000
        )

    def _rename_group(self) -> None:
        if self._group is None:
            return
        name, accepted = QInputDialog.getText(
            self,
            "Rename group",
            "Group name:",
            text=self._group.name,
        )
        if not accepted:
            return
        name = name.strip()
        if not name:
            return
        self._group.name = name
        self._reload_groups(self._group.id)
        self._schedule_save()

    def _delete_group(self) -> None:
        if self._group is None:
            return
        answer = QMessageBox.question(
            self,
            "Delete group",
            f"Delete group '{self._group.name}' and all of its templates?",
            QMessageBox.Yes | QMessageBox.No,
            QMessageBox.No,
        )
        if answer != QMessageBox.Yes:
            return
        self.store.remove_group(self._group.id)
        self._group = None
        self._reload_groups(None)
        self._schedule_save()
        self.hotkeys_changed.emit()

    def _move_group(self, delta: int) -> None:
        if self._group is None:
            return
        if self.store.move_group(self._group.id, delta):
            self._reload_groups(self._group.id)
            self._schedule_save()

    def _add_template(self) -> None:
        if self._group is None:
            return
        template = self.store.add_template(self._group.id)
        if template is None:
            return
        self._reload_templates(template.id)
        self._schedule_save()

    def _duplicate_template(self) -> None:
        if self._group is None or self._template is None:
            return
        copy = self.store.duplicate_template(self._group.id, self._template.id)
        if copy is None:
            return
        self._reload_templates(copy.id)
        self._schedule_save()

    def _delete_template(self) -> None:
        if self._group is None or self._template is None:
            return
        answer = QMessageBox.question(
            self,
            "Delete template",
            f"Delete template '{self._template.title}'?",
            QMessageBox.Yes | QMessageBox.No,
            QMessageBox.No,
        )
        if answer != QMessageBox.Yes:
            return
        self.store.remove_template(self._group.id, self._template.id)
        self._template = None
        self._reload_templates(None)
        self._schedule_save()

    def _move_template(self, delta: int) -> None:
        if self._group is None or self._template is None:
            return
        if self.store.move_template(self._group.id, self._template.id, delta):
            self._reload_templates(self._template.id)
            self._schedule_save()

    def closeEvent(self, event) -> None:
        self.flush_save()
        event.ignore()
        self.hide()
        self.statusBar().showMessage(
            "PasteNotes is still running in the tray", 4000
        )
