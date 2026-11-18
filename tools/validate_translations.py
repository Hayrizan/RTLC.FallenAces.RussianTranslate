#!/usr/bin/env python3
"""Validate translation catalogs against private extraction catalogs and an owned install.

No extracted game data is bundled. --catalog-dir accepts the recon output directory
(data-catalog.json, ui-candidates.json, optionally dynamic-ui.json/live-ui.json).
--game-dir additionally checks actual subtitle segments with the game's lexer rules,
after the same narrow repairs as Plugin.SubtitleBodyPrefix.
"""
from __future__ import annotations

import argparse
from collections import Counter
import json
from pathlib import Path
import re
import sys

TAGS = re.compile(r"<[^>]+>")
PLACEHOLDERS = re.compile(r"\{(?:\d+|[A-Za-z_]\w*)(?:[^{}]*)\}")
TOKEN = re.compile(r'//[^\n]*|/\*.*?\*/|"(?:\\.|[^"\\])*"|[A-Za-z_][\w]*|[-+]?(?:\d+(?:\.\d*)?|\.\d+)|[^\s]', re.S)
# These serialized fields are developer labels, asset identifiers, or nonlinguistic
# counters, not text shown to players. Keep exclusions explicit and auditable.
UI_INTERNAL = {
    '(Global) "crickets"', '(Under Text)', '[Ambience Tracker]', '[DEBUG]',
    '[Enemy State Tracker]', 'x2',
    'Name: Unarmed Goon\n\nPhysical State: \n\tIdle\n\tSubstate: Unknown\nBehaviour State: \n\tEngagingPlayer\n\tSubstate: Unknown',
}

# These fields become visible names/actions via Prop, HumanoidNPC and Usable.
# Editor layer/event names and script/texture identifiers must not be translated.
LEVEL_DISPLAY_FIELDS = {'name', 'description', 'custom_name', 'print_name',
                        'title', 'display_name', 'use_verb', 'character_name'}
STRING_PROPERTY = re.compile(r'([A-Za-z_]\w*)\s*=\s*"((?:\\.|[^"\\])*)"')
EXTRA_PROPERTIES = re.compile(r'extra_properties\(((?:"(?:\\.|[^"\\])*"|[^()])*)\)')
# LootProp.Variant is converted to spaced display names at runtime, independently
# of the generic actor name. Keep this source inventory explicit and auditable.
LOOT_NAMES = ('Wad Of Dough', 'Wallet', 'Gold Pocket Watch', 'Gold Ring',
              'Lucky Rabbits Foot', 'Diamond', 'Silver Bar', 'Gold Bar',
              'Pearl Necklace', 'Stack Of Coins', 'Gold Tooth',
              'Silver Cigarette Case', 'Pile Of Coins', 'Cursed Emerald',
              'Ruby', 'Sapphire', 'Jewel Encrusted Falcon', 'Penny', 'Quarter')


def current_world_sources(game_dir: Path) -> dict[str, set[str]]:
    result = {name: set() for name in ('Level display names and actions',
              'Current note pages', 'Current journal note titles', 'Current prop and decal names',
              'Sprite display names', 'Chapter title casing variants', 'Script display strings',
              'Faction display names')}
    # FactionManager.Awake supplies these before chapter/script overrides. Faction
    # IDs remain internal; only the resolved name reaches EnemyOnHudTracker.
    result['Faction display names'].update(('Player', 'Enemies'))
    root = game_dir / 'AcesData'
    for path in root.rglob('*.txt'):
        if 'Subtitles' in path.relative_to(root).parts:
            continue
        text = path.read_text(encoding='utf-8-sig', errors='replace')
        if path.name.lower() == 'chapterinfo.txt':
            title = re.search(r'(?m)^title\s*=\s*"([^"]+)"', text)
            if title:
                result['Chapter title casing variants'].update((title[1], title[1].upper()))
            result['Faction display names'].update(re.findall(
                r'\bfaction_name\s*=\s*[-+]?\d+\s*"((?:\\.|[^"\\])*)"', text, re.I))
        if 'Scripts' in path.relative_to(root).parts:
            # Keep quoted literals intact while removing comments. These APIs
            # supply journal/map labels and HUD text, not texture or script IDs.
            script = ''.join(m[0] for m in TOKEN.finditer(text) if not m[0].startswith(('//', '/*')))
            calls = re.finditer(r'(?:AddObjective|GiveMap|SetChapterIntroText|ShowNotification|'
                                r'SetObjectiveCount|SetObjectiveMaxCount|CompleteObjective|'
                                r'FailObjective|CancelObjective|ObjectiveExists)\("'
                                r'((?:\\.|[^"\\])*)"', script, re.I)
            values = [m[1] for m in calls]
            values.extend(m[1] for m in re.finditer(
                r'SetTalkPromptText\([^,]+,"((?:\\.|[^"\\])*)"', script, re.I))
            # An ampersand denotes a script variable reference, not visible text.
            result['Script display strings'].update(s for s in values if not s.startswith('&'))
            # SetFactionName takes an ID first and the visible name second. It
            # cannot be collected by the first-string-argument rules above.
            result['Faction display names'].update(m[1] for m in re.finditer(
                r'SetFactionName\([^,]+,"((?:\\.|[^"\\])*)"', script, re.I)
                if not m[1].startswith('&'))
        if re.match(r'\s*Global\s*\{', text):
            for chunk in EXTRA_PROPERTIES.finditer(text):
                result['Level display names and actions'].update(
                    value for key, value in STRING_PROPERTY.findall(chunk[1])
                    if key in LEVEL_DISPLAY_FIELDS)
        elif 'Notes' in path.relative_to(root).parts:
            properties = STRING_PROPERTY.findall(text)
            result['Current journal note titles'].update(v for k, v in properties if k == 'print_name')
            pages = [v for k, v in properties if k == 'text']
            result['Current note pages'].update(pages)
            # JournalNotesPanel strips tags from the first page when PrintName is
            # absent. Tagged page translations alone do not cover this label.
            if pages and TAGS.search(pages[0]) and not any(k == 'print_name' and v for k, v in properties):
                result['Current journal note titles'].add(TAGS.sub('', pages[0]).strip())
        elif path.parent == root:
            for actor in re.finditer(r'actor\s+(\w+)\s+\d+\s*\{([^}]*)\}', text, re.S):
                name, body = actor.groups()
                kind = re.search(r'\btype\s*=\s*"([^"]+)"', body)
                if kind and kind[1] in ('Prop', 'Decal'):
                    explicit = re.search(r'\b(?:inGameName|title|name)\s*=\s*"([^"]*)"', body)
                    display = explicit[1] if explicit else re.sub(r'(?<!^)(?=[A-Z])', ' ', re.sub(r'\d', '', name))
                    if display.strip():
                        result['Current prop and decal names'].add(display)
                if re.search(r'inGameNameIsSpriteName\s*=\s*true', body, re.I):
                    sprites = re.search(r'sprites\s*=\s*([^;]+)', body, re.S)
                    if sprites:
                        result['Sprite display names'].update(re.findall(r'"([^"\n]+)"', sprites[1]))
    result['Generated loot names'] = set(LOOT_NAMES)
    return result


def load(path: Path, errors: list[str]):
    def pairs(rows):
        result = {}
        for key, value in rows:
            if key in result:
                errors.append(f"{path}: duplicate JSON key {key!r}")
            result[key] = value
        return result
    return json.loads(path.read_text(encoding='utf-8-sig'), object_pairs_hook=pairs)


def repair_subtitle(text: str) -> str:
    text = text.replace('lip? "Cause', 'lip? Cause')
    lines = text.split('\n')
    for index, line in enumerate(lines):
        # Deliberately match only T command lines, never expression names or IDs.
        if re.match(r'^\s*T\s', line) and line.count('"') % 2:
            lines[index] = line.rstrip('\r') + '"'
    return '\n'.join(lines)


def subtitle_segments(text: str) -> list[str]:
    """Simulate LexText's string/expression handling on valid/repaired source.

    An E command consumes its timing and expression argument; the latter can be a
    quoted animation name and must never enter the translation catalog. I commands
    select a voice-line index at runtime; here all indices are visited for coverage.
    """
    tokens = [m.group() for m in TOKEN.finditer(text) if not m.group().startswith(('//', '/*'))]
    result = []
    index = 0
    while index < len(tokens):
        token = tokens[index]
        if token == 'E':
            index += 3
            continue
        if token.startswith('"'):
            # TextmapLexer.RemoveQuotesFromString removes quotes but does not
            # unescape backslashes. Preserve this behavior for exact-key checks.
            result.append(token.replace('"', ''))
        index += 1
    return result


def translate(source: str, entries: dict[str, str], depth: int = 0) -> str:
    """Model exact/trimmed/template/tag fallback used by TranslationCatalog."""
    if source in entries:
        return entries[source]
    stripped = source.strip()
    if stripped in entries and stripped:
        start = source.index(stripped)
        return source[:start] + entries[stripped] + source[start + len(stripped):]
    if depth < 5:
        for key, value in entries.items():
            matches = list(re.finditer(r'\{(\d+)(?:[^{}]*)\}', key))
            if not matches:
                continue
            regex, position, seen = r'\A', 0, set()
            for match in matches:
                regex += re.escape(key[position:match.start()])
                group = 'p' + match[1]
                regex += '(?P=' + group + ')' if group in seen else '(?P<' + group + '>.+?)'
                seen.add(group)
                position = match.end()
            found = re.match(regex + re.escape(key[position:]) + r'\Z', source, re.S)
            if found:
                def replacement(match):
                    captured = found['p' + match[1]]
                    return translate(captured, entries, depth + 1) if len(captured) < len(source) else captured
                return re.sub(r'\{(\d+)(?:[^{}]*)\}', replacement, value)
    if '\n' in source:
        translated = '\n'.join(translate(line, entries, depth + 1) for line in source.split('\n'))
        if translated != source:
            return translated
    if '<' in source:
        return ''.join(part if TAGS.fullmatch(part) else translate(part, entries, depth + 1)
                       for part in re.split(r'(<[^>]+>)', source)) if depth < 5 else source
    return source


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--translations-dir', type=Path, default=Path(__file__).resolve().parents[1] / 'translations')
    parser.add_argument('--catalog-dir', type=Path)
    parser.add_argument('--game-dir', type=Path)
    args = parser.parse_args()
    errors: list[str] = []
    entries: dict[str, str] = {}
    files = sorted(args.translations_dir.glob('*.json'))
    if not files:
        errors.append(f'No translation JSON files in {args.translations_dir}')
    for path in files:
        try:
            data = load(path, errors)
            if not isinstance(data, dict):
                errors.append(f'{path}: expected a JSON object')
                continue
            for key, value in data.items():
                if not isinstance(value, str) or not key or not value:
                    errors.append(f'{path}: empty/non-string translation for {key!r}')
                    continue
                if key in entries and entries[key] != value:
                    errors.append(f'{path}: conflicting translation for {key!r}')
                if Counter(TAGS.findall(key)) != Counter(TAGS.findall(value)):
                    errors.append(f'{path}: markup tags differ for {key!r}')
                if Counter(PLACEHOLDERS.findall(key)) != Counter(PLACEHOLDERS.findall(value)):
                    errors.append(f'{path}: placeholders differ for {key!r}')
                entries[key] = value
        except (ValueError, OSError) as exc:
            errors.append(f'{path}: {exc}')
    print(f'Catalog integrity: {len(files)} files, {len(entries)} unique keys')

    def coverage(label: str, source, strict=True):
        values = {s for s in source if isinstance(s, str) and re.search('[A-Za-z]', TAGS.sub('', s))}
        missing = sorted(s for s in values if translate(s, entries) == s)
        print(f'{label}: {len(values) - len(missing)}/{len(values)} translated')
        if strict:
            errors.extend(f'{label}: missing translation {s!r}' for s in missing)
        elif missing:
            print(f'  {len(missing)} source-code fragments require manual classification (not a coverage claim)')

    if args.catalog_dir:
        for filename, label in [('data-catalog.json', 'External game data'), ('ui-candidates.json', 'Serialized UI')]:
            path = args.catalog_dir / filename
            if not path.is_file():
                errors.append(f'Missing source catalog: {path}')
                continue
            data = load(path, errors)
            if isinstance(data, dict):
                coverage(label, data)
            else:
                values = [row['value'] for row in data]
                excluded = [s for s in values if s in UI_INTERNAL or s.endswith(' Tutorial') or s.endswith(' Tutorial 2')]
                print(f'{label}: excluded {len(set(excluded))} explicit developer/asset labels')
                coverage(label, (s for s in values if s not in excluded))
        dynamic = args.catalog_dir / 'dynamic-ui.json'
        for filename, label in [('data-extra.json', 'Additional descriptions'), ('actor-names.json', 'Actor display names')]:
            extra = args.catalog_dir / filename
            if extra.exists():
                coverage(label, load(extra, errors))
        if dynamic.exists():
            coverage('Dynamic UI fragments (advisory)', (row[2] for row in load(dynamic, errors)), strict=False)

    if args.game_dir:
        for label, source in current_world_sources(args.game_dir).items():
            coverage(label, source)
        subtitle_dir = args.game_dir / 'AcesData' / 'Subtitles'
        source_files = list(subtitle_dir.rglob('*.txt'))
        if not source_files:
            errors.append(f'No subtitle files found: {subtitle_dir}')
        phrases = []
        repaired_count = 0
        for path in source_files:
            text = path.read_text(encoding='utf-8-sig')
            repaired = repair_subtitle(text)
            repaired_count += text != repaired
            phrases.extend(subtitle_segments(repaired))
        print(f'Subtitle parser: {len(source_files)} files, {repaired_count} repaired source files, {len(phrases)} segments')
        coverage('Parsed subtitle segments', phrases)

    # Small behavioral checks cover the repair and expression-skipping contract.
    sample = 'I 0\n{ T 0 "Hello" E 1 "Worried 1" T 2 "Goodbye" }'
    if subtitle_segments(sample) != ['Hello', 'Goodbye']:
        errors.append('Subtitle simulation failed to exclude expression animation names')
    if subtitle_segments(repair_subtitle('T 0 "Broken\nT 2 "Next"')) != ['Broken', 'Next']:
        errors.append('Subtitle repair simulation failed')
    fixture = {'Hello': 'Привет', 'Found {0}': 'Найдено: {0}', 'Key': 'Ключ'}
    for source, expected in [('  Hello\n', '  Привет\n'),
                             ('Found Key', 'Найдено: Ключ'),
                             ('<color=red>Hello</color>', '<color=red>Привет</color>'),
                             ('Hello\nKey', 'Привет\nКлюч')]:
        if translate(source, fixture) != expected:
            errors.append(f'Translation fallback simulation failed for {source!r}')
    if errors:
        for error in errors:
            print('ERROR:', error, file=sys.stderr)
        print(f'FAILED: {len(errors)} issue(s)', file=sys.stderr)
        return 1
    print('PASS (static catalog/coverage checks; no runtime claim)')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
