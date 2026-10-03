"""Extract GLB images verbatim, sharing identical content/channels and retaining geometry.

Run after acquiring/replacing gym assets, then commit the GLBs, Textures, and sources.json.
Unity imports the external images through TextureImporter instead of uncompressed GLB
subassets. No image resampling or material/normal/UV conversion happens in this step.
"""
import argparse
import copy
import hashlib
import json
from pathlib import Path
import struct
import uuid

ROOT = Path(__file__).resolve().parents[1]
MODELS = ROOT / 'Assets/ThirdParty/GymModels'


def read_glb(data):
    magic, version, size = struct.unpack_from('<III', data)
    if (magic, version, size) != (0x46546c67, 2, len(data)):
        raise ValueError('Expected GLB 2.0')
    chunks, offset = [], 12
    while offset < size:
        length, kind = struct.unpack_from('<II', data, offset)
        chunks.append((kind, data[offset + 8:offset + 8 + length]))
        offset += 8 + length
    if len(chunks) != 2 or chunks[0][0] != 0x4e4f534a or chunks[1][0] != 0x004e4942:
        raise ValueError('Expected JSON and BIN chunks')
    return json.loads(chunks[0][1]), chunks[1][1]


def write_glb(document, binary):
    encoded = json.dumps(document, ensure_ascii=False, separators=(',', ':')).encode('utf8')
    encoded += b' ' * (-len(encoded) % 4)
    binary += b'\0' * (-len(binary) % 4)
    return (struct.pack('<III', 0x46546c67, 2, 28 + len(encoded) + len(binary)) +
            struct.pack('<II', len(encoded), 0x4e4f534a) + encoded +
            struct.pack('<II', len(binary), 0x004e4942) + binary)


def walk(value):
    if isinstance(value, dict):
        yield value
        for child in value.values():
            yield from walk(child)
    elif isinstance(value, list):
        for child in value:
            yield from walk(child)


def image_roles(document):
    roles = {}
    for node in walk(document.get('materials', [])):
        for key, info in node.items():
            if key.endswith('Texture') and isinstance(info, dict) and 'index' in info:
                texture = document['textures'][info['index']]
                role = ('normal' if 'normal' in key.lower() else 'color' if key in
                        ('baseColorTexture', 'diffuseTexture', 'emissiveTexture', 'specularGlossinessTexture') else 'data')
                roles.setdefault(texture['source'], set()).add(role)
    if any(len(value) != 1 for value in roles.values()):
        raise ValueError('One source image has conflicting color/normal/data roles')
    return {index: next(iter(value)) for index, value in roles.items()}


def externalize(data):
    document, binary = read_glb(data)
    if not any('bufferView' in image for image in document.get('images', [])):
        return data, {}
    original_materials = copy.deepcopy(document.get('materials'))
    roles, extracted = image_roles(document), {}
    views = document['bufferViews']
    for index, image in enumerate(document.get('images', [])):
        if 'bufferView' not in image:
            continue
        view = views[image.pop('bufferView')]
        if view.get('buffer', 0) != 0:
            raise ValueError('Expected the embedded GLB buffer')
        start = view.get('byteOffset', 0)
        pixels = binary[start:start + view['byteLength']]
        suffix = {'image/png': '.png', 'image/jpeg': '.jpg'}[image.pop('mimeType')]
        name = hashlib.sha256(pixels).hexdigest() + '-' + roles.get(index, 'data') + suffix
        extracted[name] = pixels
        image['uri'] = '../../Textures/' + name
    # Repack only referenced views, removing embedded image bytes without changing any
    # accessor data. Indices are remapped across the whole document, including extensions.
    references = [node for node in walk(document) if 'bufferView' in node]
    kept = sorted({node['bufferView'] for node in references})
    remap, packed, new_views = {}, bytearray(), []
    for old_index in kept:
        view = copy.deepcopy(views[old_index])
        if view.get('buffer', 0) != 0:
            raise ValueError('External buffers are not supported by this extraction')
        start = view.get('byteOffset', 0)
        content = binary[start:start + view['byteLength']]
        packed.extend(b'\0' * (-len(packed) % 4))
        view['byteOffset'] = len(packed)
        packed.extend(content)
        remap[old_index] = len(new_views)
        new_views.append(view)
    for node in references:
        node['bufferView'] = remap[node['bufferView']]
    document['bufferViews'] = new_views
    document['buffers'][0]['byteLength'] = len(packed)
    assert document.get('materials') == original_materials
    for old, new in remap.items():
        a, b = views[old], new_views[new]
        assert binary[a.get('byteOffset', 0):a.get('byteOffset', 0) + a['byteLength']] == packed[b['byteOffset']:b['byteOffset'] + b['byteLength']]
    return write_glb(document, bytes(packed)), extracted


def check(path):
    document, _ = read_glb(path.read_bytes())
    for image in document.get('images', []):
        if 'bufferView' in image or not image.get('uri', '').startswith('../../Textures/'):
            raise ValueError(f'{path.name}: image is not externalized')
        texture = (path.parent / image['uri']).resolve()
        if not texture.is_relative_to(MODELS / 'Textures'):
            raise ValueError('Texture outside the shared texture directory')
        if hashlib.sha256(texture.read_bytes()).hexdigest() != texture.name.split('-')[0]:
            raise ValueError(f'External image content hash mismatch: {texture.name}')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    paths = sorted((MODELS / 'Resources/Gym').glob('*.glb'))
    if not args.check:
        manifest_path = MODELS / 'sources.json'
        manifest = json.loads(manifest_path.read_text(encoding='utf-8-sig'))
        entries = {Path(entry['file']).name: entry for entry in manifest['assets']}
        target = MODELS / 'Textures'
        target.mkdir(exist_ok=True)
        changes = [(path, path.read_bytes(), *externalize(path.read_bytes())) for path in paths]
        for path, before, after, images in changes:
            for name, content in images.items():
                texture = target / name
                if not texture.exists():
                    texture.write_bytes(content)
                    texture.with_suffix(texture.suffix + '.meta').write_text(
                        'fileFormatVersion: 2\nguid: ' + uuid.uuid5(uuid.NAMESPACE_URL, 'vitalvr:gym:' + name).hex + '\n', encoding='utf8')
                elif texture.read_bytes() != content:
                    raise ValueError('Content-addressed image collision')
            if before != after:
                path.write_bytes(after)
                entry = entries[path.name]
                entry.setdefault('originalSha256', entry['sha256'])
                entry['sha256'] = hashlib.sha256(after).hexdigest()
                entry['derivation'] = 'Externalized original encoded images by SHA256/channel; removed duplicate image buffer views. Geometry, materials, UVs, normals, author and license unchanged. tools/Externalize-GymTextures.py'
        encoded = json.dumps(manifest, ensure_ascii=False, indent=2) + '\n'
        if manifest_path.read_text(encoding='utf-8-sig') != encoded:
            manifest_path.write_text(encoded, encoding='utf8')
    for path in paths:
        check(path)
    print(f'{len(paths)} GLBs verified; shared external textures retain their original encoded bytes.')


if __name__ == '__main__':
    main()
