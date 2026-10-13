"""Prepare outdoor field models and surface photographs from their downloaded originals.

Run with:  uv run --with pillow python tools/Prepare-FieldModels.py <downloads>
<downloads> holds the Objaverse GLBs named <sketchfab uid>.glb and the extracted ambientCG folders.

Each GLB keeps its geometry, UVs, normals and material names; embedded images are removed because
the environment palette owns every field material. The photographs a palette material needs are
written beside the other environment textures and imported through the native texture importer.
"""
import hashlib
import io
import json
from pathlib import Path
import struct
import sys

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
MODELS = ROOT / 'Assets/ThirdParty/FieldModels'
TEXTURES = ROOT / 'Assets/_Project/Resources/Visual/Textures'

# name: (sketchfab uid, title, author, {source material: (texture file, size)})
SOURCES = {
    'goal': ('3001e42d65d3486ba56da3a36d6e5219', 'Gary Fixter Football Goal', 'garyfixter', {}),
    'bleachers': ('10a22c724d1a4083bab2d64cdbac6f01', 'Sports Bleachers', 'justice2free', {}),
    'poplar': ('ec7c1d301bbc43dca5fe5f0650148d13', 'POPLAR TREE - HIGHT TEXTURE LOW-POLY MODEL', 'evseevdaniil0011',
               {'Material.002': ('poplar_bark_color.jpg', 512), 'Material.001': ('poplar_leaves_color.png', 1024)}),
}
# ambientCG folder, file suffix -> environment texture
SURFACES = {('Grass005', 'Color'): 'pitch_turf_color.jpg', ('Grass005', 'NormalGL'): 'pitch_turf_normal.jpg'}


def read_glb(data):
    magic, version, size = struct.unpack_from('<III', data)
    if (magic, version, size) != (0x46546c67, 2, len(data)):
        raise ValueError('Expected GLB 2.0')
    json_length = struct.unpack_from('<I', data, 12)[0]
    binary_start = 20 + json_length + 8
    return json.loads(data[20:20 + json_length]), data[binary_start:]


def write_glb(document, binary):
    encoded = json.dumps(document, ensure_ascii=False, separators=(',', ':')).encode('utf8')
    encoded += b' ' * (-len(encoded) % 4)
    binary += b'\0' * (-len(binary) % 4)
    return (struct.pack('<III', 0x46546c67, 2, 28 + len(encoded) + len(binary)) +
            struct.pack('<II', len(encoded), 0x4e4f534a) + encoded +
            struct.pack('<II', len(binary), 0x004e4942) + binary)


def strip_images(data, wanted):
    """Returns the image-free GLB and {texture file: pixels} for the requested base colours."""
    document, binary = read_glb(data)
    views, extracted = document.get('bufferViews', []), {}
    for material in document.get('materials', []):
        target = wanted.get(material.get('name'))
        legacy = material.get('extensions', {}).pop('KHR_materials_pbrSpecularGlossiness', {})
        base = material.get('pbrMetallicRoughness', {}).get('baseColorTexture') or legacy.get('diffuseTexture')
        if target and base:
            view = views[document['images'][document['textures'][base['index']]['source']]['bufferView']]
            start = view.get('byteOffset', 0)
            extracted[target] = binary[start:start + view['byteLength']]
        for key in ('normalTexture', 'occlusionTexture', 'emissiveTexture'):
            material.pop(key, None)
        for key in ('baseColorTexture', 'metallicRoughnessTexture'):
            material.get('pbrMetallicRoughness', {}).pop(key, None)
    dropped = {image['bufferView'] for image in document.get('images', []) if 'bufferView' in image}
    for key in ('images', 'textures', 'samplers'):
        document.pop(key, None)
    for key in ('extensionsUsed', 'extensionsRequired'):
        if key in document:
            document[key] = [name for name in document[key] if name != 'KHR_materials_pbrSpecularGlossiness']
            if not document[key]:
                del document[key]
    remap, packed, kept = {}, bytearray(), []
    for index, view in enumerate(views):
        if index in dropped:
            continue
        start = view.get('byteOffset', 0)
        packed.extend(b'\0' * (-len(packed) % 4))
        remap[index] = len(kept)
        kept.append({**view, 'byteOffset': len(packed)})
        packed.extend(binary[start:start + view['byteLength']])

    def renumber(value):
        if isinstance(value, dict):
            if 'bufferView' in value:
                value['bufferView'] = remap[value['bufferView']]
            for child in value.values():
                renumber(child)
        elif isinstance(value, list):
            for child in value:
                renumber(child)
    renumber(document)
    document['bufferViews'] = kept
    document['buffers'][0]['byteLength'] = len(packed)
    return write_glb(document, bytes(packed)), extracted


def save_image(pixels, path, size):
    image = Image.open(io.BytesIO(pixels))
    image = image.convert('RGBA' if path.suffix == '.png' else 'RGB')
    if max(image.size) > size:
        image = image.resize((size, size), Image.LANCZOS)
    image.save(path, quality=90)


def main():
    downloads = Path(sys.argv[1])
    target = MODELS / 'Resources/Field'
    target.mkdir(parents=True, exist_ok=True)
    assets = []
    for name, (uid, title, author, wanted) in SOURCES.items():
        original = (downloads / 'glb' / (uid + '.glb')).read_bytes()
        prepared, images = strip_images(original, {material: file for material, (file, _) in wanted.items()})
        (target / (name + '.glb')).write_bytes(prepared)
        for file, size in wanted.values():
            save_image(images[file], TEXTURES / file, size)
        assets.append({
            'file': 'Assets/ThirdParty/FieldModels/Resources/Field/' + name + '.glb', 'title': title, 'author': author,
            'source': 'https://sketchfab.com/3d-models/' + uid, 'license': 'CC-BY-4.0',
            'sha256': hashlib.sha256(prepared).hexdigest(), 'originalSha256': hashlib.sha256(original).hexdigest(),
            'textures': ['Assets/_Project/Resources/Visual/Textures/' + file for file, _ in wanted.values()],
            'derivation': 'Embedded images removed; base colours saved as environment textures at reduced size. '
                          'Geometry, UVs, normals and material names unchanged. tools/Prepare-FieldModels.py'})
    for (folder, suffix), file in SURFACES.items():
        save_image((downloads / 'acg' / folder / f'{folder}_1K-JPG_{suffix}.jpg').read_bytes(), TEXTURES / file, 1024)
        assets.append({'file': 'Assets/_Project/Resources/Visual/Textures/' + file, 'title': folder, 'author': 'ambientCG',
                       'source': 'https://ambientcg.com/view?id=' + folder, 'license': 'CC0-1.0',
                       'sha256': hashlib.sha256((TEXTURES / file).read_bytes()).hexdigest()})
    (MODELS / 'sources.json').write_text(json.dumps(
        {'license': 'https://creativecommons.org/licenses/by/4.0/', 'assets': assets}, indent=2) + '\n', encoding='utf-8')


if __name__ == '__main__':
    main()
