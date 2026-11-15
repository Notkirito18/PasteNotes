from __future__ import annotations

GROUP_COLORS: tuple[tuple[str, str], ...] = (
    ("yellow", "#F2C94C"),
    ("green", "#6FCF97"),
    ("blue", "#56A3E8"),
    ("pink", "#EE8FBE"),
    ("purple", "#A98BE0"),
    ("orange", "#F2994A"),
    ("red", "#EB5757"),
    ("gray", "#9AA0AA"),
)

DEFAULT_GROUP_COLOR = "yellow"
TEXT_ON_GROUP_COLOR = "#1b202b"

_COLOR_HEX = dict(GROUP_COLORS)


def normalize_color(key: str | None) -> str:
    if isinstance(key, str) and key in _COLOR_HEX:
        return key
    return DEFAULT_GROUP_COLOR


def color_hex(key: str | None) -> str:
    return _COLOR_HEX[normalize_color(key)]


def color_label(key: str | None) -> str:
    return normalize_color(key).title()
