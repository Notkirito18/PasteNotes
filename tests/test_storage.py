import json

from pastenotes.colors import GROUP_COLORS
from pastenotes.hotkeys import parse_hotkey
from pastenotes.storage import DataStore

DEFAULT_GROUP_NAMES = [
    "WhatsApp - Customer Care (AR)",
    "WhatsApp - Transfers (AR)",
    "WhatsApp - Rates & Gold (AR)",
    "WhatsApp - Cards & Wallets (AR)",
    "Email - Operations (EN)",
    "Refunds & Payments (EN)",
    "Verification (EN)",
    "Internal - Notes & Prompts (EN)",
]


def make_store(tmp_path):
    store = DataStore(tmp_path / "data.json")
    store.load()
    return store


def test_first_run_seeds_defaults(tmp_path):
    store = make_store(tmp_path)
    assert [group.name for group in store.groups] == DEFAULT_GROUP_NAMES
    assert store.groups[0].templates[0].title == "Greeting - Thank You"
    assert store.groups[0].color == "green"
    assert store.groups[0].hotkey == "Ctrl+Alt+1"
    assert store.groups[-1].hotkey == "Ctrl+Alt+8"
    assert (tmp_path / "data.json").exists()


def test_default_groups_are_well_formed(tmp_path):
    store = make_store(tmp_path)
    hotkeys = [group.hotkey for group in store.groups]
    titles = [template.title for group in store.groups for template in group.templates]

    assert len(store.groups) == 8
    assert len(hotkeys) == len(set(hotkeys))
    assert all(parse_hotkey(hotkey) for hotkey in hotkeys)
    assert all(group.color in dict(GROUP_COLORS) for group in store.groups)
    assert all(group.templates for group in store.groups)
    assert all(template.content.strip() for group in store.groups for template in group.templates)
    assert len(titles) == len(set(titles))
    assert sum(len(group.templates) for group in store.groups) == 78
    assert (
        store.groups[0].templates[0].content
        == "شكرًا لتواصلكم مع الفردان للصرافة، نتمنى لكم يومًا سعيدًا!"
    )


def test_group_color_roundtrip(tmp_path):
    store = make_store(tmp_path)
    group = store.add_group("Colors", "Ctrl+Alt+4")
    group.color = "purple"
    store.save()

    reloaded = DataStore(tmp_path / "data.json")
    reloaded.load()
    assert reloaded.find_group(group.id).color == "purple"


def test_old_data_without_color_gets_default(tmp_path):
    path = tmp_path / "data.json"
    path.write_text(
        json.dumps(
            {
                "version": 1,
                "groups": [
                    {"id": "g1", "name": "Old", "hotkey": "", "templates": []},
                    {
                        "id": "g2",
                        "name": "Bad",
                        "hotkey": "",
                        "color": "neon",
                        "templates": [],
                    },
                ],
            }
        ),
        encoding="utf-8",
    )
    store = DataStore(path)
    store.load()
    assert store.groups[0].color == "yellow"
    assert store.groups[1].color == "yellow"


def test_arabic_content_roundtrip(tmp_path):
    store = make_store(tmp_path)
    group = store.add_group("مجموعة", "Ctrl+Alt+5")
    template = store.add_template(group.id, "تحية", "مرحبا بالعالم")
    store.save()

    reloaded = DataStore(tmp_path / "data.json")
    reloaded.load()
    loaded_group = reloaded.find_group(group.id)
    assert loaded_group.name == "مجموعة"
    assert reloaded.find_template(group.id, template.id).title == "تحية"
    assert reloaded.find_template(group.id, template.id).content == "مرحبا بالعالم"


def test_roundtrip_persists_changes(tmp_path):
    store = make_store(tmp_path)
    group = store.add_group("Work", "Ctrl+Alt+3")
    template = store.add_template(
        group.id,
        "Standup",
        "Yesterday / today / blockers",
    )
    store.settings["restore_clipboard"] = False
    store.save()

    reloaded = DataStore(tmp_path / "data.json")
    reloaded.load()
    loaded_group = reloaded.find_group(group.id)
    assert loaded_group is not None
    assert loaded_group.name == "Work"
    assert loaded_group.hotkey == "Ctrl+Alt+3"
    loaded_template = reloaded.find_template(group.id, template.id)
    assert loaded_template is not None
    assert loaded_template.content == "Yesterday / today / blockers"
    assert reloaded.settings["restore_clipboard"] is False


def test_move_group_and_template(tmp_path):
    store = make_store(tmp_path)
    first = store.groups[0]
    assert store.move_group(first.id, 1)
    assert store.groups[1].id == first.id
    assert not store.move_group(first.id, 100)

    extra = store.add_template(first.id, "Second", "text")
    assert first.templates[-1].id == extra.id
    assert store.move_template(first.id, extra.id, -(len(first.templates) - 1))
    assert first.templates[0].id == extra.id


def test_duplicate_and_delete(tmp_path):
    store = make_store(tmp_path)
    group = store.groups[0]
    source = group.templates[0]
    copy = store.duplicate_template(group.id, source.id)
    assert copy is not None
    assert copy.id != source.id
    assert copy.title.startswith(source.title)
    assert store.remove_template(group.id, copy.id)
    assert store.remove_group(group.id)
    assert store.find_group(group.id) is None


def test_corrupt_file_is_quarantined_and_reseeded(tmp_path):
    path = tmp_path / "data.json"
    path.write_text("{not valid json", encoding="utf-8")
    store = DataStore(path)
    store.load()
    assert [group.name for group in store.groups] == DEFAULT_GROUP_NAMES
    assert list(tmp_path.glob("data.corrupt-*.json"))


def test_missing_settings_get_defaults(tmp_path):
    path = tmp_path / "data.json"
    path.write_text(json.dumps({"version": 1, "groups": []}), encoding="utf-8")
    store = DataStore(path)
    store.load()
    assert store.settings["restore_clipboard"] is True
    assert store.groups == []
