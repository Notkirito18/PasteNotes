from __future__ import annotations

import json
import os
import shutil
import sys
import uuid
from dataclasses import dataclass, field
from datetime import datetime
from pathlib import Path

from .colors import DEFAULT_GROUP_COLOR, normalize_color
from .defaults import DEFAULT_GROUPS

APP_NAME = "PasteNotes"
DATA_VERSION = 1
DEFAULT_SETTINGS = {"restore_clipboard": True}


def _portable_data_dir() -> Path | None:
    if not getattr(sys, "frozen", False):
        return None
    exe_dir = Path(sys.executable).resolve().parent
    if not (exe_dir / "PasteNotes.portable").exists():
        return None
    data_dir = exe_dir / "PasteNotesData"
    try:
        data_dir.mkdir(parents=True, exist_ok=True)
        probe = data_dir / ".write-test"
        probe.write_text("ok", encoding="utf-8")
        probe.unlink()
    except OSError:
        return None
    return data_dir


def app_dir() -> Path:
    override = os.environ.get("PASTENOTES_DATA_DIR")
    if override:
        return Path(override)
    portable = _portable_data_dir()
    if portable is not None:
        return portable
    base = os.environ.get("APPDATA")
    root = Path(base) if base else Path.home() / "AppData" / "Roaming"
    return root / APP_NAME


def default_data_path() -> Path:
    return app_dir() / "data.json"


@dataclass
class Template:
    id: str
    title: str
    content: str = ""

    @classmethod
    def create(cls, title: str = "New template", content: str = "") -> "Template":
        return cls(id=uuid.uuid4().hex, title=title, content=content)

    def to_dict(self) -> dict:
        return {"id": self.id, "title": self.title, "content": self.content}

    @classmethod
    def from_dict(cls, data: dict) -> "Template":
        return cls(
            id=str(data.get("id") or uuid.uuid4().hex),
            title=str(data.get("title") or "Untitled template"),
            content=str(data.get("content") or ""),
        )


@dataclass
class Group:
    id: str
    name: str
    hotkey: str = ""
    color: str = DEFAULT_GROUP_COLOR
    templates: list[Template] = field(default_factory=list)

    @classmethod
    def create(
        cls,
        name: str = "New group",
        hotkey: str = "",
        templates: list[Template] | None = None,
        color: str = DEFAULT_GROUP_COLOR,
    ) -> "Group":
        return cls(
            id=uuid.uuid4().hex,
            name=name,
            hotkey=hotkey,
            color=normalize_color(color),
            templates=list(templates or []),
        )

    def to_dict(self) -> dict:
        return {
            "id": self.id,
            "name": self.name,
            "hotkey": self.hotkey,
            "color": self.color,
            "templates": [template.to_dict() for template in self.templates],
        }

    @classmethod
    def from_dict(cls, data: dict) -> "Group":
        templates = [
            Template.from_dict(item)
            for item in data.get("templates", [])
            if isinstance(item, dict)
        ]
        return cls(
            id=str(data.get("id") or uuid.uuid4().hex),
            name=str(data.get("name") or "Untitled group"),
            hotkey=str(data.get("hotkey") or ""),
            color=normalize_color(data.get("color")),
            templates=templates,
        )


class DataStore:
    def __init__(self, path: Path | str | None = None) -> None:
        self.path = Path(path) if path is not None else default_data_path()
        self.version = DATA_VERSION
        self.settings: dict = dict(DEFAULT_SETTINGS)
        self.groups: list[Group] = []

    def load(self) -> None:
        if not self.path.exists():
            self._seed()
            self.save()
            return

        try:
            raw = json.loads(self.path.read_text(encoding="utf-8"))
        except (OSError, ValueError):
            self._quarantine_corrupt_file()
            self._seed()
            self.save()
            return

        if not isinstance(raw, dict):
            self._quarantine_corrupt_file()
            self._seed()
            self.save()
            return

        self.version = DATA_VERSION
        settings = raw.get("settings")
        self.settings = (
            {**DEFAULT_SETTINGS, **settings}
            if isinstance(settings, dict)
            else dict(DEFAULT_SETTINGS)
        )
        groups = raw.get("groups")
        self.groups = (
            [Group.from_dict(item) for item in groups if isinstance(item, dict)]
            if isinstance(groups, list)
            else []
        )

    def save(self) -> None:
        self.path.parent.mkdir(parents=True, exist_ok=True)
        payload = {
            "version": DATA_VERSION,
            "settings": self.settings,
            "groups": [group.to_dict() for group in self.groups],
        }
        temporary = self.path.with_name(self.path.name + ".tmp")
        temporary.write_text(
            json.dumps(payload, ensure_ascii=False, indent=2),
            encoding="utf-8",
        )
        if self.path.exists():
            try:
                shutil.copyfile(self.path, self.path.with_name(self.path.name + ".bak"))
            except OSError:
                pass
        os.replace(temporary, self.path)

    def find_group(self, group_id: str) -> Group | None:
        return next((group for group in self.groups if group.id == group_id), None)

    def find_template(self, group_id: str, template_id: str) -> Template | None:
        group = self.find_group(group_id)
        if group is None:
            return None
        return next(
            (template for template in group.templates if template.id == template_id),
            None,
        )

    def unique_group_name(self, base: str = "New group") -> str:
        names = {group.name for group in self.groups}
        if base not in names:
            return base
        index = 2
        while f"{base} {index}" in names:
            index += 1
        return f"{base} {index}"

    def unique_template_title(self, group: Group, base: str = "New template") -> str:
        titles = {template.title for template in group.templates}
        if base not in titles:
            return base
        index = 2
        while f"{base} {index}" in titles:
            index += 1
        return f"{base} {index}"

    def add_group(self, name: str | None = None, hotkey: str = "") -> Group:
        group = Group.create(name or self.unique_group_name(), hotkey)
        self.groups.append(group)
        return group

    def remove_group(self, group_id: str) -> bool:
        before = len(self.groups)
        self.groups = [group for group in self.groups if group.id != group_id]
        return len(self.groups) != before

    def move_group(self, group_id: str, delta: int) -> bool:
        return _move(self.groups, lambda group: group.id == group_id, delta)

    def add_template(
        self,
        group_id: str,
        title: str | None = None,
        content: str = "",
    ) -> Template | None:
        group = self.find_group(group_id)
        if group is None:
            return None
        template = Template.create(
            title or self.unique_template_title(group),
            content,
        )
        group.templates.append(template)
        return template

    def remove_template(self, group_id: str, template_id: str) -> bool:
        group = self.find_group(group_id)
        if group is None:
            return False
        before = len(group.templates)
        group.templates = [
            template for template in group.templates if template.id != template_id
        ]
        return len(group.templates) != before

    def duplicate_template(self, group_id: str, template_id: str) -> Template | None:
        group = self.find_group(group_id)
        if group is None:
            return None
        source = next(
            (template for template in group.templates if template.id == template_id),
            None,
        )
        if source is None:
            return None
        copy = Template.create(
            self.unique_template_title(group, f"{source.title} copy"),
            source.content,
        )
        group.templates.insert(group.templates.index(source) + 1, copy)
        return copy

    def move_template(self, group_id: str, template_id: str, delta: int) -> bool:
        group = self.find_group(group_id)
        if group is None:
            return False
        return _move(
            group.templates,
            lambda template: template.id == template_id,
            delta,
        )

    def _seed(self) -> None:
        self.version = DATA_VERSION
        self.settings = dict(DEFAULT_SETTINGS)
        self.groups = [
            Group.create(
                spec["name"],
                spec["hotkey"],
                [Template.create(title, content) for title, content in spec["templates"]],
                spec["color"],
            )
            for spec in DEFAULT_GROUPS
        ]

    def _quarantine_corrupt_file(self) -> None:
        timestamp = datetime.now().strftime("%Y%m%d-%H%M%S")
        target = self.path.with_name(
            f"{self.path.stem}.corrupt-{timestamp}{self.path.suffix}"
        )
        try:
            self.path.replace(target)
        except OSError:
            pass


def _move(items: list, predicate, delta: int) -> bool:
    index = next((i for i, item in enumerate(items) if predicate(item)), None)
    if index is None:
        return False
    target = index + delta
    if target < 0 or target >= len(items):
        return False
    items[index], items[target] = items[target], items[index]
    return True
