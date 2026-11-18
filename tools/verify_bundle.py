"""Verify that Unity imported every shipped translation and Cyrillic font intact."""

import sys
from pathlib import Path

import UnityPy


root = Path(__file__).resolve().parents[1]
bundle = UnityPy.load(sys.argv[1])
expected = {
    **{f"assets/rtlc/translations/{name}": ("TextAsset", root / "translations" / name)
       for name in ("data.ru.json", "subtitles.ru.json", "ui.ru.json")},
    "assets/rtlc/fonts/fonts.json": ("TextAsset", root / "Fonts" / "fonts.json"),
    **{f"assets/rtlc/fonts/{name.lower()}": ("Font", root / "Fonts" / name)
       for name in ("Broadway Cyrillic Extended.ttf", "brushscriptstd.ttf",
                    "JosefinSansExt-Regular.ttf", "Troubleside.ttf")},
}
for name, (kind, source) in expected.items():
    matches = [obj for path, obj in bundle.container.items()
               if path == name and obj.type.name == kind]
    if len(matches) != 1:
        raise SystemExit(f"Missing or duplicate bundled {kind}: {name}")
    asset = matches[0].read()
    contents = (asset.m_Script.encode("utf-8") if kind == "TextAsset"
                else bytes(asset.m_FontData))
    if contents != source.read_bytes():
        raise SystemExit(f"Bundled bytes differ from source: {name}")
print(f"PASS: {len(expected)} bundled catalogs and fonts byte-match their sources")
