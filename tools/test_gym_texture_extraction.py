"""Focused binary regression: geometry/material preservation and repeated extraction."""
import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location('gym_extract', Path(__file__).with_name('Externalize-GymTextures.py'))
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class ExtractionTests(unittest.TestCase):
    def test_deduplicates_images_preserves_geometry_channels_and_is_idempotent(self):
        geometry, image = b'GEOMETRY-DATA', b'encoded-png-image'
        document = {
            'asset': {'version': '2.0'}, 'buffers': [{'byteLength': len(geometry + image)}],
            'bufferViews': [{'buffer': 0, 'byteOffset': 0, 'byteLength': len(geometry)},
                            {'buffer': 0, 'byteOffset': len(geometry), 'byteLength': len(image)}],
            'accessors': [{'bufferView': 0, 'componentType': 5126, 'count': 1, 'type': 'VEC3'}],
            'images': [{'bufferView': 1, 'mimeType': 'image/png'}] * 2,
            'textures': [{'source': 0}, {'source': 1}],
            'materials': [{'normalTexture': {'index': 0, 'scale': .25}}, {'normalTexture': {'index': 1}}],
            'nodes': [{'mesh': 0, 'translation': [1, 2, 3]}],
            'meshes': [{'primitives': [{'attributes': {'POSITION': 0}, 'material': 0}]}],
        }
        original = module.write_glb(document, geometry + image)
        derived, images = module.externalize(original)
        actual, binary = module.read_glb(derived)
        self.assertEqual(len(images), 1)
        self.assertEqual(next(iter(images.values())), image)
        self.assertIn('-normal.png', next(iter(images)))
        self.assertEqual(binary[:len(geometry)], geometry)
        self.assertEqual(len(actual['bufferViews']), 1)
        for field in ('materials', 'nodes', 'meshes', 'accessors', 'textures'):
            self.assertEqual(actual[field], document[field])
        self.assertEqual(module.externalize(derived), (derived, {}))


if __name__ == '__main__':
    unittest.main()
