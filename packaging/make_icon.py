import struct
from pathlib import Path

from PySide6.QtCore import QBuffer, Qt
from PySide6.QtGui import QColor, QImage, QPainter, QPen


def render_icon(size: int = 256) -> QImage:
    image = QImage(size, size, QImage.Format_ARGB32)
    image.fill(Qt.transparent)
    painter = QPainter(image)
    painter.setRenderHint(QPainter.Antialiasing)
    painter.scale(size / 64.0, size / 64.0)
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
    return image


def main() -> None:
    image = render_icon(256)
    buffer = QBuffer()
    buffer.open(QBuffer.WriteOnly)
    if not image.save(buffer, "PNG"):
        raise SystemExit("Could not render the icon")
    png = bytes(buffer.data())
    header = struct.pack("<HHH", 0, 1, 1)
    entry = struct.pack("<BBBBHHII", 0, 0, 0, 0, 1, 32, len(png), 22)
    target = Path(__file__).with_name("PasteNotes.ico")
    target.write_bytes(header + entry + png)
    print(f"Wrote {target}")


if __name__ == "__main__":
    main()
