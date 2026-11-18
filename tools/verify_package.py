"""Check the install ZIP against the pinned BepInEx archive and built plugin."""

import sys
from pathlib import Path
from zipfile import ZipFile


project = Path(__file__).resolve().parents[1]
package_path, bepinex_path = map(Path, sys.argv[1:3])
plugin = "BepInEx/plugins/RTLC.FallenAces.RussianTranslate/"
generated = {
    plugin + "RTLC.FallenAces.RussianTranslate.dll":
        project / "bin/Release/netstandard2.1/RTLC.FallenAces.RussianTranslate.dll",
    plugin + "rtlc.bundle": project / "tools/BundleProject/BundleOutput/rtlc.bundle",
}
notices = {
    "ThirdPartyLicenses/THIRD_PARTY_NOTICES.txt": project / "THIRD_PARTY_NOTICES.txt",
    **{"ThirdPartyLicenses/" + path.name: path for path in (project / "LICENSES").glob("*.txt")},
}

with ZipFile(bepinex_path) as original, ZipFile(package_path) as package:
    if original.testzip() is not None or package.testzip() is not None:
        raise SystemExit("A ZIP entry failed its CRC check")
    original_files = {entry.filename for entry in original.infolist() if not entry.is_dir()}
    package_names = [entry.filename for entry in package.infolist() if not entry.is_dir()]
    if len(package_names) != len(set(package_names)):
        raise SystemExit("Package contains duplicate file names")
    expected_files = original_files | generated.keys() | notices.keys()
    if set(package_names) != expected_files:
        raise SystemExit("Package has missing or extra files: "
                         + repr(sorted(set(package_names) ^ expected_files)))
    if any(name.lower().endswith(".md") or ".." in Path(name).parts for name in package_names):
        raise SystemExit("Package contains Markdown or an unsafe path")
    for name in original_files:
        if package.read(name) != original.read(name):
            raise SystemExit("BepInEx file differs from official archive: " + name)
    for name, source in {**generated, **notices}.items():
        if package.read(name) != source.read_bytes():
            raise SystemExit("Package file differs from source: " + name)
print(f"PASS: {len(original_files)} official BepInEx files, 2 plugin files and {len(notices)} license notices; CRC OK; no Markdown")
