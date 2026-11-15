"""Generate the embedded default data blob for the native PasteNotes build.

Run from the repository root:
    python native/tools/gen_defaults.py
"""

from __future__ import annotations

import base64
import gzip
import importlib.util
import json
import re
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
DEFAULTS_PATH = ROOT / "pastenotes" / "defaults.py"
DATA_SOURCE = ROOT / "native" / "src" / "03_Data.cs"
BLOB_BEGIN = "// BEGIN DEFAULTS BLOB"
BLOB_END = "// END DEFAULTS BLOB"
GREETING_BEGIN = "// BEGIN EXPECTED GREETING"
GREETING_END = "// END EXPECTED GREETING"


def load_defaults():
    spec = importlib.util.spec_from_file_location("pastenotes_defaults", DEFAULTS_PATH)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Could not load {DEFAULTS_PATH}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module.DEFAULT_GROUPS


def build_payload(groups) -> dict:
    payload = {
        "version": 1,
        "settings": {"restore_clipboard": True},
        "groups": [],
    }

    for group in groups:
        payload["groups"].append(
            {
                "id": "",
                "name": group["name"],
                "hotkey": group["hotkey"],
                "color": group["color"],
                "templates": [
                    {"id": "", "title": title, "content": content}
                    for title, content in group["templates"]
                ],
            }
        )
    return payload


def compress(payload: dict) -> str:
    raw = json.dumps(
        payload,
        ensure_ascii=False,
        separators=(",", ":"),
    ).encode("utf-8")
    compressed = gzip.compress(raw, compresslevel=9, mtime=0)
    return base64.b64encode(compressed).decode("ascii")


def csharp_escape(value: str) -> str:
    parts: list[str] = []
    for char in value:
        code = ord(char)
        if char == "\\":
            parts.append("\\\\")
        elif char == '"':
            parts.append('\\"')
        elif char == "\r":
            parts.append("\\r")
        elif char == "\n":
            parts.append("\\n")
        elif char == "\t":
            parts.append("\\t")
        elif 0x20 <= code < 0x7F:
            parts.append(char)
        elif code <= 0xFFFF:
            parts.append(f"\\u{code:04x}")
        else:
            encoded = char.encode("utf-16-le", "surrogatepass")
            for index in range(0, len(encoded), 2):
                unit = int.from_bytes(encoded[index : index + 2], "little")
                parts.append(f"\\u{unit:04x}")
    return "".join(parts)


def replace_block(source: str, begin: str, end: str, assignment: str) -> str:
    pattern = re.compile(
        re.escape(begin) + r"\s*(?:private const string \w+ = \".*?\";\s*)?" + re.escape(end),
        re.DOTALL,
    )
    replacement = f"{begin}\n        {assignment}\n        {end}"
    updated, count = pattern.subn(lambda match: replacement, source)
    if count != 1:
        raise RuntimeError(f"Expected exactly one block for {begin}, found {count}")
    return updated


def main() -> int:
    groups = load_defaults()
    template_count = sum(len(group["templates"]) for group in groups)
    payload = build_payload(groups)
    blob = compress(payload)
    greeting = groups[0]["templates"][0][1]
    escaped_greeting = csharp_escape(greeting)

    source = DATA_SOURCE.read_text(encoding="utf-8")
    source = replace_block(
        source,
        BLOB_BEGIN,
        BLOB_END,
        f'private const string DefaultsCompressedBase64 = "{blob}";',
    )
    source = replace_block(
        source,
        GREETING_BEGIN,
        GREETING_END,
        f'private const string ExpectedGreeting = "{escaped_greeting}";',
    )
    DATA_SOURCE.write_text(source, encoding="utf-8", newline="\n")

    try:
        source.encode("ascii")
    except UnicodeEncodeError as exc:
        print(f"ERROR: 03_Data.cs is not ASCII: {exc}", file=sys.stderr)
        return 1

    print(f"Groups: {len(groups)}")
    print(f"Templates: {template_count}")
    print(f"Compressed blob (base64 chars): {len(blob)}")
    print(f"Expected greeting (C# chars): {len(escaped_greeting)}")
    print(f"Updated: {DATA_SOURCE}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
