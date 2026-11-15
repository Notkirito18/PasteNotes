from __future__ import annotations

import logging
import sys
from logging.handlers import RotatingFileHandler

from PySide6.QtCore import QObject, Qt, QTimer
from PySide6.QtGui import QColor, QIcon, QPainter, QPen, QPixmap
from PySide6.QtNetwork import QLocalServer, QLocalSocket
from PySide6.QtWidgets import QApplication, QMenu, QSystemTrayIcon

from .hotkeys import HotkeyManager
from .manager import ManagerWindow
from .paste import force_foreground, get_foreground_window, is_window, paste_text
from .picker import Picker
from .storage import DataStore, app_dir

SERVER_NAME = "PasteNotes.SingleInstance.v1"

log = logging.getLogger(__name__)


def _build_icon() -> QIcon:
    pixmap = QPixmap(64, 64)
    pixmap.fill(Qt.transparent)
    painter = QPainter(pixmap)
    painter.setRenderHint(QPainter.Antialiasing)
    painter.setPen(Qt.NoPen)
    painter.setBrush(QColor("#f3c14b"))
    painter.drawRoundedRect(10, 10, 44, 48, 8, 8)
    painter.setBrush(QColor("#2a2f3a"))
    painter.drawRoundedRect(22, 6, 20, 10, 4, 4)
    painter.setPen(QPen(QColor("#4d3a12"), 3))
    painter.drawLine(20, 28, 44, 28)
    painter.drawLine(20, 37, 44, 37)
    painter.drawLine(20, 46, 36, 46)
    painter.end()
    return QIcon(pixmap)


class PasteNotesApp(QObject):
    def __init__(self, qapp: QApplication) -> None:
        super().__init__()
        self.qapp = qapp
        self._paused = False
        self._picker: Picker | None = None
        self._target_hwnd = 0

        self.store = DataStore()
        self.store.load()

        self.icon = _build_icon()
        self.qapp.setWindowIcon(self.icon)

        self.manager = ManagerWindow(self.store)
        self.manager.setWindowIcon(self.icon)
        self.manager.hotkeys_changed.connect(self.apply_hotkeys)

        self.hotkeys = HotkeyManager(self)
        self.hotkeys.hotkey_pressed.connect(self._on_hotkey)

        self.tray = QSystemTrayIcon(self.icon, self)
        self._build_tray()

        self._server = QLocalServer(self)
        self._server.newConnection.connect(self._on_new_connection)
        QLocalServer.removeServer(SERVER_NAME)
        if not self._server.listen(SERVER_NAME):
            log.warning(
                "Could not start single-instance server: %s",
                self._server.errorString(),
            )

        self.apply_hotkeys(notify=True)
        self.qapp.aboutToQuit.connect(self.shutdown)

    def _build_tray(self) -> None:
        menu = QMenu()
        open_action = menu.addAction("Open PasteNotes")
        open_action.triggered.connect(self.open_manager)
        self.pause_action = menu.addAction("Pause hotkeys")
        self.pause_action.setCheckable(True)
        self.pause_action.toggled.connect(self.set_paused)
        menu.addSeparator()
        quit_action = menu.addAction("Exit")
        quit_action.triggered.connect(self.quit)

        self.tray.setContextMenu(menu)
        self.tray.setToolTip("PasteNotes")
        self.tray.activated.connect(self._on_tray_activated)
        self.tray.show()

    def _on_tray_activated(self, reason) -> None:
        if reason in (QSystemTrayIcon.Trigger, QSystemTrayIcon.DoubleClick):
            self.open_manager()

    def open_manager(self) -> None:
        if self.manager.isMinimized():
            self.manager.showNormal()
        self.manager.show()
        self.manager.raise_()
        self.manager.activateWindow()

    def apply_hotkeys(self, notify: bool = False) -> None:
        if self._paused:
            self.hotkeys.unregister_all()
            self.manager.set_hotkey_statuses(
                {group.id: "Paused" for group in self.store.groups}
            )
            return

        entries = [
            (group.id, group.name, group.hotkey) for group in self.store.groups
        ]
        statuses = self.hotkeys.apply(entries)
        self.manager.set_hotkey_statuses(statuses)

        problems = [
            (group.name, statuses.get(group.id))
            for group in self.store.groups
            if statuses.get(group.id) not in (None, "No hotkey set")
        ]
        if notify and problems:
            detail = "\n".join(
                f"{name}: {message}" for name, message in problems[:4]
            )
            self.tray.showMessage(
                "PasteNotes hotkey problem",
                detail,
                QSystemTrayIcon.Warning,
                8000,
            )

    def set_paused(self, paused: bool) -> None:
        self._paused = paused
        if self.pause_action.isChecked() != paused:
            self.pause_action.blockSignals(True)
            self.pause_action.setChecked(paused)
            self.pause_action.blockSignals(False)
        if paused:
            self.hotkeys.unregister_all()
            self.manager.set_hotkey_statuses(
                {group.id: "Paused" for group in self.store.groups}
            )
            self.tray.showMessage(
                "PasteNotes",
                "Hotkeys paused",
                QSystemTrayIcon.Information,
                3000,
            )
        else:
            self.apply_hotkeys(notify=True)
            self.tray.showMessage(
                "PasteNotes",
                "Hotkeys resumed",
                QSystemTrayIcon.Information,
                3000,
            )

    def _on_hotkey(self, group_id: str) -> None:
        if self._paused:
            return
        group = self.store.find_group(group_id)
        if group is None:
            return

        self._target_hwnd = get_foreground_window()

        previous = self._picker
        if previous is not None:
            self._picker = None
            previous.close()
            previous.deleteLater()

        picker = Picker(group)
        picker.selected.connect(self._on_template_selected)
        picker.cancelled.connect(self._on_picker_cancelled)
        self._picker = picker
        picker.open_at_cursor()

    def _on_template_selected(self, text: str) -> None:
        picker = self._picker
        self._picker = None
        if picker is not None:
            picker.deleteLater()

        restore = bool(self.store.settings.get("restore_clipboard", True))
        target = self._target_hwnd
        QTimer.singleShot(0, lambda: paste_text(text, target, restore))

    def _on_picker_cancelled(self, refocus: bool) -> None:
        picker = self._picker
        self._picker = None
        if picker is not None:
            picker.deleteLater()
        if refocus and self._target_hwnd and is_window(self._target_hwnd):
            force_foreground(self._target_hwnd)

    def _on_new_connection(self) -> None:
        connection = self._server.nextPendingConnection()
        if connection is None:
            return
        connection.readyRead.connect(lambda: self._read_request(connection))
        connection.disconnected.connect(connection.deleteLater)
        if connection.bytesAvailable():
            self._read_request(connection)

    def _read_request(self, connection: QLocalSocket) -> None:
        data = bytes(connection.readAll())
        if not data:
            return
        self.open_manager()
        connection.disconnectFromServer()

    def quit(self) -> None:
        self.qapp.quit()

    def shutdown(self) -> None:
        self.hotkeys.unregister_all()
        self.manager.flush_save()
        self.tray.hide()
        if self._server.isListening():
            self._server.close()


def setup_logging() -> None:
    directory = app_dir()
    try:
        directory.mkdir(parents=True, exist_ok=True)
        handler = RotatingFileHandler(
            directory / "pasteNotes.log",
            maxBytes=1_000_000,
            backupCount=3,
            encoding="utf-8",
        )
    except OSError:
        return
    handler.setFormatter(
        logging.Formatter("%(asctime)s %(levelname)s %(name)s: %(message)s")
    )
    root = logging.getLogger()
    root.setLevel(logging.INFO)
    root.addHandler(handler)

    def handle_exception(exc_type, exc_value, exc_traceback):
        logging.getLogger("PasteNotes").critical(
            "Unhandled exception",
            exc_info=(exc_type, exc_value, exc_traceback),
        )
        sys.__excepthook__(exc_type, exc_value, exc_traceback)

    sys.excepthook = handle_exception


def main(argv: list[str] | None = None) -> int:
    arguments = list(sys.argv if argv is None else argv)
    setup_logging()

    app = QApplication(arguments)
    app.setApplicationName("PasteNotes")
    app.setOrganizationName("PasteNotes")
    app.setQuitOnLastWindowClosed(False)

    probe = QLocalSocket()
    probe.connectToServer(SERVER_NAME)
    if probe.waitForConnected(150):
        probe.write(b"show")
        probe.flush()
        probe.waitForBytesWritten(200)
        probe.disconnectFromServer()
        return 0

    controller = PasteNotesApp(app)
    if "--show" in arguments:
        controller.open_manager()

    return app.exec()
