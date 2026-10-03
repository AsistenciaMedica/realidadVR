"""Fetch the approved MIT Rocketbox patient models from one pinned upstream commit."""
import argparse
import concurrent.futures
import hashlib
import json
from pathlib import Path
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
COMMIT = '0943055db6ec570bcef9f2c8b41c9e5467c808f9'
BASE = f'https://raw.githubusercontent.com/microsoft/Microsoft-Rocketbox/{COMMIT}/'
PATIENTS = [
    ('Daniel', 'Professions/Sports_Male_04'), ('Lucia', 'Adults/Female_Adult_12'),
    ('Mateo', 'Adults/Male_Adult_20'), ('Ricardo', 'Adults/Male_Adult_05'),
    ('Sara', 'Professions/Sports_Female_02'), ('Rosa', 'Adults/Female_Adult_05'),
    ('Javier', 'Adults/Male_Adult_17'), ('Camila', 'Adults/Female_Adult_01'),
    ('Beatriz', 'Adults/Female_Adult_09'), ('Luis', 'Adults/Male_Adult_02'),
    ('Andres', 'Professions/Sports_Male_02'), ('Pablo', 'Professions/Sports_Male_03'),
    ('Elena', 'Adults/Female_Adult_03'), ('Sergio', 'Adults/Male_Adult_08'),
    ('Miguel', 'Adults/Male_Adult_12'),
]

def fetch(url):
    request = urllib.request.Request(url, headers={'User-Agent': 'VitalVR-PatientRoster'})
    with urllib.request.urlopen(request, timeout=90) as response:
        return response.read()

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--previews-only', action='store_true')
    args = parser.parse_args()
    tree = json.loads(fetch(f'https://api.github.com/repos/microsoft/Microsoft-Rocketbox/git/trees/{COMMIT}?recursive=1'))
    files = {entry['path']: entry for entry in tree['tree'] if entry['type'] == 'blob'}
    jobs = []
    models = []
    for patient_id, folder in PATIENTS:
        upstream = 'Assets/Avatars/' + folder
        model = folder.rsplit('/', 1)[1]
        preview = upstream + '/' + model + '.png'
        jobs.append((preview, ROOT / 'TestResults' / 'patient-roster-source-previews' / (patient_id + '.png')))
        if args.previews_only:
            continue
        destination = ROOT / 'Assets/ThirdParty/Rocketbox/Roster' / patient_id
        if patient_id == 'Daniel':
            local_model = 'Assets/ThirdParty/Rocketbox/Daniel/Daniel.fbx'
            local_textures = 'Assets/ThirdParty/Rocketbox/Daniel/Textures'
        else:
            source_model = upstream + '/Export/' + model + '_facial.fbx'
            jobs.append((source_model, destination / (patient_id + '.fbx')))
            local_model = (destination / (patient_id + '.fbx')).relative_to(ROOT).as_posix()
            local_textures = (destination / 'Textures').relative_to(ROOT).as_posix()
            for path in files:
                if path.startswith(upstream + '/Textures/') and any(path.endswith(suffix + '.tga') for suffix in
                    ('_body_color', '_body_normal', '_head_color', '_head_normal', '_opacity_color')):
                    jobs.append((path, destination / 'Textures' / path.rsplit('/', 1)[1]))
        models.append({'id': patient_id, 'upstreamModel': model, 'modelPath': local_model, 'textureDirectory': local_textures,
                       'source': BASE + upstream + '/Export/' + model + '_facial.fbx'})

    def download(job):
        upstream, target = job
        expected = files[upstream]
        write_target = not target.exists()
        data = fetch(BASE + upstream) if write_target else target.read_bytes()
        git_hash = hashlib.sha1(b'blob ' + str(len(data)).encode() + b'\0' + data).hexdigest()
        if git_hash != expected['sha'] and target.suffix == '.png' and 'TestResults' in target.parts:
            data = fetch(BASE + upstream)
            write_target = True
            git_hash = hashlib.sha1(b'blob ' + str(len(data)).encode() + b'\0' + data).hexdigest()
        if git_hash != expected['sha']:
            raise RuntimeError('Upstream integrity mismatch: ' + upstream)
        if write_target:
            # Keep incomplete files outside Assets so an open editor cannot import partial FBX/textures.
            staging = ROOT / 'TestResults' / 'patient-roster-downloads' / (expected['sha'] + '.part')
            staging.parent.mkdir(parents=True, exist_ok=True)
            staging.write_bytes(data)
            target.parent.mkdir(parents=True, exist_ok=True)
            staging.replace(target)
        return {'file': target.relative_to(ROOT).as_posix(), 'source': BASE + upstream,
                'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest(), 'gitBlob': git_hash}

    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
        records = list(pool.map(download, jobs))
    if not args.previews_only:
        output = ROOT / 'Assets/ThirdParty/Rocketbox/Roster'
        output.mkdir(parents=True, exist_ok=True)
        manifest = {'schemaVersion': 1, 'license': 'MIT', 'copyright': 'Copyright (c) 2020 Microsoft',
                    'commit': COMMIT, 'models': models, 'assets': records}
        (output / 'sources.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(f'Verified {len(records)} files from Rocketbox {COMMIT}; models={len(models)}')

if __name__ == '__main__':
    main()
