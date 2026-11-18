"""Verify the CIL inventory against the reviewed owned build, without game code in repo.

AssemblyTextAudit decodes every method body, not a whitelist of UI call names.
Any changed assembly requires a fresh contextual review; hash equality alone is
not proof of localization completeness. Rendered compositions are tested in C#.
"""
import argparse
import json
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument('inventory', type=Path)
args = parser.parse_args()
actual = json.loads(args.inventory.read_text(encoding='utf-8-sig'))
baseline = json.loads(Path(__file__).with_name('assembly_text_baseline.json').read_text())
if {a['name'] for a in actual} != {b['name'] for b in baseline}:
    raise SystemExit('Assembly inventory does not match the reviewed assembly set.')
for expected in baseline:
    a = next(a for a in actual if a['name'] == expected['name'])
    observed = {key: a[key] for key in ('name', 'sha256', 'mvid', 'methods')}
    observed.update(literalOccurrences=len(a['rows']), uniqueLiterals=len({r['text'] for r in a['rows']}))
    if observed != expected:
        raise SystemExit(f"{a['name']}: game assembly changed; re-audit its literals and composed UI before updating the baseline.")
    print(f"CIL inventory {a['name']}: {a['methods']} method bodies, {len(a['rows'])} literal occurrences, {observed['uniqueLiterals']} unique; reviewed build matches")
