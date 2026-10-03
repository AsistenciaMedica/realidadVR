#!/usr/bin/env python3
"""Extract the 15 actual QuestXRWalkthroughTests from one complete NUnit XML run.

Test-case elements (results, messages, outputs, times and properties) are preserved.
Only enclosing aggregate counts/results are recalculated after structural filtering.
Writes provenance JSON; exit 0 requires all 15 cases AND their fixture to have passed.
Exit 1 preserves observed failures; exit 2 rejects incomplete or ambiguous input.

python tools/Extract-AstraXRResults.py TestResults/astra/final-playmode.xml \
    --output TestResults/astra/xr-walkthrough.xml
"""
from __future__ import annotations

import argparse
import copy
import hashlib
import json
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

CLASS = 'EmergencyVR.Tests.QuestXRWalkthroughTests'
EXPECTED = {
    'Daniel_Hypotension', 'Gym_Faint', 'Gym_Glucose', 'Gym_ChestPain', 'Gym_Asthma',
    'Mall_UnconsciousBreathing', 'Mall_AbnormalBreathing', 'Mall_Choking', 'Mall_Confusion', 'Mall_Dehydration',
    'Andres_Arrest', 'Football_Faint', 'Football_Glucose', 'Football_HeatExhaustion', 'Football_Hypoxia',
}
COUNT_KEYS = ('passed', 'failed', 'inconclusive', 'skipped')


def fingerprint(element: ET.Element):
    """Ignore only formatting whitespace between elements, preserving actual node content."""
    text = element.text or ''
    if not text.strip():
        text = ''
    return (element.tag, tuple(sorted(element.attrib.items())), text,
            tuple(fingerprint(child) for child in element))


def result_kind(element: ET.Element) -> str:
    result = element.get('result', '').split('(')[0]
    if result not in ('Passed', 'Failed', 'Inconclusive', 'Skipped'):
        raise ValueError(f"Unknown or unfinished test result {result!r}: {element.get('fullname', element.tag)}")
    return result


def selected(case: ET.Element) -> bool:
    return case.tag == 'test-case' and case.get('classname') == CLASS


def prune(element: ET.Element) -> bool:
    if element.tag == 'test-case':
        return selected(element)
    for child in list(element):
        if child.tag in ('test-suite', 'test-case') and not prune(child):
            element.remove(child)
    cases = list(element.iter('test-case'))
    if not cases:
        return False
    counts = {key: sum(result_kind(case).lower() == key for case in cases) for key in COUNT_KEYS}
    element.set('testcasecount', str(len(cases)))
    element.set('total', str(len(cases)))
    for key, value in counts.items():
        element.set(key, str(value))
    element.set('asserts', str(sum(int(case.get('asserts', '0')) for case in cases)))
    is_fixture = element.tag == 'test-suite' and element.get('fullname') == CLASS
    if not is_fixture:
        children = [child for child in element if child.tag in ('test-suite', 'test-case')]
        states = [result_kind(child) for child in children]
        # A suite's own setup/teardown failure is not a failure from an excluded sibling.
        own_failure = element.find('failure') is not None and element.get('site') in ('SetUp', 'TearDown')
        result = 'Failed' if own_failure or 'Failed' in states else 'Inconclusive' if 'Inconclusive' in states else 'Passed' if 'Passed' in states else 'Skipped'
        element.set('result', result)
        if result != 'Failed':
            element.attrib.pop('site', None)
            for failure in list(element.findall('failure')):
                element.remove(failure)
    # Ancestor timestamps stay as supplied by the source run; they are not XR timing metrics.
    return True


def extract(source: Path, output: Path) -> int:
    source, output = source.resolve(), output.resolve()
    provenance_path = output.with_suffix('.provenance.json')
    if source == output or source == provenance_path:
        raise ValueError('The source XML cannot be overwritten by the extraction.')
    raw = source.read_bytes()
    document = ET.fromstring(raw)
    if document.tag != 'test-run':
        raise ValueError('Expected the NUnit test-run document emitted by the Unity test runner.')
    fixtures = [item for item in document.iter('test-suite') if item.get('type') == 'TestFixture' and item.get('fullname') == CLASS]
    if len(fixtures) != 1:
        raise ValueError(f'Expected exactly one {CLASS} fixture; found {len(fixtures)}.')
    cases = [case for case in document.iter('test-case') if selected(case)]
    names = [case.get('methodname') for case in cases]
    if len(cases) != 15 or len(set(names)) != 15 or set(names) != EXPECTED:
        raise ValueError(f'Expected exactly the 15 distinct release walkthroughs; found {len(cases)}. Missing={sorted(EXPECTED-set(names))}; unexpected={sorted(str(name) for name in set(names)-EXPECTED)}')
    for case in cases:
        if case.get('fullname') != CLASS + '.' + case.get('methodname', ''):
            raise ValueError('Unexpected parameterized or renamed case: ' + str(case.get('fullname')))
        result_kind(case)
    result_kind(fixtures[0])
    if len(list(fixtures[0].iter('test-case'))) != 15:
        raise ValueError('The selected fixture includes unexpected test cases.')

    filtered = copy.deepcopy(document)
    prune(filtered)
    preserved = list(filtered.iter('test-case'))
    if len(preserved) != 15 or [fingerprint(case) for case in preserved] != [fingerprint(case) for case in cases]:
        raise RuntimeError('Structural filtering changed an original test-case element.')
    sha = hashlib.sha256(raw).hexdigest()
    properties = filtered.find('properties')
    if properties is None:
        properties = ET.Element('properties')
        filtered.insert(0, properties)
    for name, value in {
        'AstraEvidenceKind': 'Structural subset of original Unity NUnit run; not a new execution',
        'AstraSourceXml': str(source), 'AstraSourceSha256': sha,
        'AstraSourceRunResult': document.get('result', ''), 'AstraSelectedFixture': CLASS,
        'AstraTimingNote': 'Ancestor durations/timestamps are retained from the source run; test-case times are unchanged.',
    }.items():
        ET.SubElement(properties, 'property', name=name, value=value)
    encoded = ET.tostring(filtered, encoding='utf-8', xml_declaration=True)
    reloaded = ET.fromstring(encoded)
    if [fingerprint(case) for case in reloaded.iter('test-case')] != [fingerprint(case) for case in cases]:
        raise RuntimeError('XML serialization changed a case.')
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_bytes(encoded)
    evidence = {
        'schemaVersion': 1, 'kind': 'EXTRACTED_ORIGINAL_RESULTS', 'newTestExecution': False,
        'source': {'path': str(source), 'sha256': sha, 'result': document.get('result'), 'total': document.get('total')},
        'selectedFixture': CLASS, 'sourceFixtureResult': fixtures[0].get('result'),
        'output': {'path': str(output), 'sha256': hashlib.sha256(encoded).hexdigest(), 'result': filtered.get('result')},
        'counts': {key: int(filtered.get(key, '0')) for key in ('total',) + COUNT_KEYS},
        'testCaseElementsUnchanged': True,
        'cases': [dict(case.attrib) for case in cases],
        'timingNote': 'Ancestor durations/timestamps belong to the source run; individual case timings are unchanged.',
    }
    provenance_path.write_text(json.dumps(evidence, indent=2, ensure_ascii=False), encoding='utf-8')
    print(json.dumps({'xml': str(output), 'provenance': str(provenance_path), 'counts': evidence['counts'], 'result': filtered.get('result')}, ensure_ascii=False))
    return 0 if filtered.get('result') == 'Passed' and all(result_kind(case) == 'Passed' for case in cases) else 1


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('source', type=Path)
    parser.add_argument('--output', required=True, type=Path)
    arguments = parser.parse_args()
    try:
        sys.exit(extract(arguments.source, arguments.output))
    except (OSError, ValueError, RuntimeError, ET.ParseError) as error:
        print('ERROR: ' + str(error), file=sys.stderr)
        sys.exit(2)
