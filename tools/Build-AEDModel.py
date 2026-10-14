"""Builds the VITAL VR AED (automated external defibrillator) model with Blender's Python module.

Usage (pip install bpy):  python3 tools/Build-AEDModel.py [--render out.png]
Output: Assets/_Project/Resources/Visual/Equipment/AED.glb

Generic, unbranded device. Dimensions match the interactive AED in MedicalProcedureRig
(shell 0.32 x 0.12 x 0.25 m, display at (-0.035, 0.071, -0.06), shock button at (0.09, -, -0.06)),
so the existing colliders, the code-driven display text and the lid animation keep working.

Unity axes are used throughout (x right, y up, z forward). glTF + glTFast map Blender (x, y, z)
to Unity (-x, z, -y), so U() converts a Unity position to Blender coordinates.

Nodes exported (names are read by MedicalProcedureRig):
  Body          shell, rubber bumper, handle, screen bezel and glass, speaker, buttons, icons
  Lid           pad-compartment lid; origin on its hinge so Euler(-95, 0, 0) opens it upwards
  PadElectrode  electrode pad (white foam + gel), centred at the origin
  PadBacking    peel-off liner with placement diagram, sits on top of the electrode
"""
import argparse
import math
import sys
from pathlib import Path

import bpy
import bmesh
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Assets/_Project/Resources/Visual/Equipment/AED.glb'


def U(x, y, z):
    """Unity position -> Blender position."""
    return Vector((-x, -z, y))


def S(x, y, z):
    """Unity size -> Blender size."""
    return Vector((x, z, y))


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


MATS = {}


def mat(name, color, rough=.5, metal=0., emit=None, emit_strength=0.):
    if name in MATS:
        return MATS[name]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes['Principled BSDF']
    bsdf.inputs['Base Color'].default_value = (*color, 1)
    bsdf.inputs['Roughness'].default_value = rough
    bsdf.inputs['Metallic'].default_value = metal
    if emit is not None:
        bsdf.inputs['Emission Color'].default_value = (*emit, 1)
        bsdf.inputs['Emission Strength'].default_value = emit_strength
    MATS[name] = m
    return m


def srgb(hex_value):
    """Hex sRGB -> linear tuple (glTF stores linear base colour factors)."""
    h = hex_value.lstrip('#')
    out = []
    for i in (0, 2, 4):
        c = int(h[i:i + 2], 16) / 255
        out.append(c / 12.92 if c <= .04045 else ((c + .055) / 1.055) ** 2.4)
    return tuple(out)


def rounded_box(name, centre, size, radius, material, segments=4, parent=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=centre)
    o = bpy.context.active_object
    o.name = name
    o.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if radius > 0:
        mod = o.modifiers.new('bevel', 'BEVEL')
        mod.width = radius
        mod.segments = segments
        mod.limit_method = 'ANGLE'
        bpy.ops.object.modifier_apply(modifier=mod.name)
    o.data.materials.append(material)
    for p in o.data.polygons:
        p.use_smooth = True
    if parent is not None:
        o.parent = parent
    return o


def cylinder(name, centre, radius, depth, material, verts=28, bevel=0.):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=depth, location=centre)
    o = bpy.context.active_object
    o.name = name
    if bevel > 0:
        mod = o.modifiers.new('bevel', 'BEVEL')
        mod.width = bevel
        mod.segments = 3
        mod.limit_method = 'ANGLE'
        bpy.ops.object.modifier_apply(modifier=mod.name)
    o.data.materials.append(material)
    for p in o.data.polygons:
        p.use_smooth = True
    return o


def flat_shape(name, points2d, z, thickness, material):
    """Extruded polygon on the top face. points2d are Unity (x, z) pairs; z is the Unity height of its base."""
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    verts = [bm.verts.new(U(px, z, pz)) for px, pz in points2d]
    face = bm.faces.new(verts)
    bmesh.ops.recalc_face_normals(bm, faces=[face])
    if face.normal.z < 0:
        face.normal_flip()
    ext = bmesh.ops.extrude_face_region(bm, geom=[face])
    moved = [v for v in ext['geom'] if isinstance(v, bmesh.types.BMVert)]
    bmesh.ops.translate(bm, vec=Vector((0, 0, thickness)), verts=moved)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    o = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(o)
    o.data.materials.append(material)
    return o


def label(name, text, centre_xz, size, z, material):
    """Flat extruded text lying on the top face, readable from the front (Unity -z) of the device."""
    bpy.ops.object.text_add(location=U(centre_xz[0], z, centre_xz[1]))
    o = bpy.context.active_object
    o.data.body = text
    o.data.size = size
    o.data.extrude = .0006
    o.data.align_x = 'CENTER'
    o.data.align_y = 'CENTER'
    # Text faces Blender +z (up); turn it so its reading direction runs along Unity +x as seen from the front.
    o.rotation_euler = (0, 0, math.pi)
    bpy.ops.object.convert(target='MESH')
    o = bpy.context.active_object
    o.name = name
    o.location.z += .0006
    o.data.materials.clear()
    o.data.materials.append(material)
    return o


def join(name, objects):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    o = bpy.context.active_object
    o.name = name
    o.data.name = name
    return o


def set_origin(obj, unity_point):
    target = U(*unity_point)
    offset = target - obj.location
    obj.data.transform(__import__('mathutils').Matrix.Translation(-offset))
    obj.location = target


def bolt(cx, cz, s):
    pts = [(-.10, .50), (.30, .50), (.06, .08), (.32, .08), (-.24, -.55), (-.02, -.06), (-.30, -.06)]
    return [(cx + x * s, cz + y * s) for x, y in pts]


def heart(cx, cz, s, n=40):
    out = []
    for i in range(n):
        t = 2 * math.pi * i / n
        x = 16 * math.sin(t) ** 3
        y = 13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t)
        out.append((cx + x / 34 * s, cz + y / 34 * s))
    return out


def build():
    reset()
    shell = mat('AED_Shell', srgb('#D3D8DC'), .4)
    bumper = mat('AED_Bumper', srgb('#1E8F4E'), .8)
    dark = mat('AED_Dark', srgb('#26292D'), .78)
    green = mat('AED_Green', srgb('#1F9D55'), .42)
    glass = mat('AED_ScreenGlass', srgb('#0B1418'), .08)
    bezel = mat('AED_Bezel', srgb('#3A3F45'), .55)
    orange = mat('AED_ShockOrange', srgb('#F26B1D'), .35, emit=srgb('#F26B1D'), emit_strength=.15)
    on_green = mat('AED_OnButton', srgb('#2FBF5C'), .35)
    white = mat('AED_White', srgb('#F7F7F5'), .45)
    grille = mat('AED_Grille', srgb('#15181B'), .7)
    foam = mat('Pad_Foam', srgb('#F2F1EC'), .85)
    gel = mat('Pad_Gel', srgb('#C9D7DC'), .25)
    liner = mat('Pad_Liner', srgb('#3FA9D6'), .45)
    print_ink = mat('Pad_Print', srgb('#1D2A33'), .6)
    red = mat('AED_Red', srgb('#D7262E'), .4)

    parts = []
    # Rubber bumper: full footprint, slightly larger, lower two thirds of the height.
    parts.append(rounded_box('bumper', U(0, -.022, 0), S(.336, .076, .262), .03, bumper, 6))
    # Main shell: light grey, softly rounded.
    parts.append(rounded_box('shell', U(0, .004, 0), S(.318, .112, .246), .028, shell, 6))
    # Recessed carry handle at the back edge (dark grip with a cut-through slot look).
    parts.append(rounded_box('handle_grip', U(0, .046, .108), S(.15, .026, .03), .011, dark, 4))
    parts.append(rounded_box('handle_slot', U(0, .05, .1), S(.11, .02, .012), .005, grille, 3))
    # Screen: dark bezel and inset glass exactly under the code-driven display text.
    parts.append(rounded_box('screen_bezel', U(-.035, .0605, -.06), S(.226, .006, .1), .008, bezel, 3))
    parts.append(rounded_box('screen_glass', U(-.035, .0640, -.06), S(.206, .003, .082), .004, glass, 2))
    # Shock button: orange, raised, with a white bolt.
    parts.append(cylinder('shock_ring', U(.09, .0615, -.06), .028, .006, dark, 32, .002))
    parts.append(cylinder('shock_button', U(.09, .0665, -.06), .022, .010, orange, 32, .003))
    parts.append(flat_shape('shock_bolt', bolt(.09, -.06, .03), .0715, .0012, white))
    # On/off button (green) beside the shock button.
    parts.append(cylinder('on_button', U(.09, .0625, -.005), .013, .007, on_green, 24, .002))
    parts.append(flat_shape('on_ring', [(.09 + .006 * math.cos(a), -.005 + .006 * math.sin(a))
                                        for a in [i * 2 * math.pi / 20 for i in range(20)]], .0662, .0008, white))
    # Speaker grille dots beside the screen.
    for i in range(4):
        for j in range(3):
            parts.append(cylinder(f'speaker_{i}_{j}', U(-.135 + j * .008, .0605, -.088 + i * .009), .0024, .002, grille, 10))
    # Status window (green check light) next to handle.
    parts.append(rounded_box('status_light', U(.115, .0605, .1), S(.024, .003, .014), .003, on_green, 2))
    body = join('Body', parts)

    # Lid over the pad compartment (back half). Hinge on its front edge, so Euler(-95,0,0) opens it upwards.
    lid_parts = [
        rounded_box('lid', U(-.02, .0625, .045), S(.24, .012, .086), .006, green, 4),
        flat_shape('lid_heart', heart(-.055, .045, .045), .0686, .0012, white),
        flat_shape('lid_bolt', bolt(-.055, .046, .028), .0699, .0010, red),
        rounded_box('lid_tab', U(-.02, .065, .089), S(.05, .008, .008), .003, dark, 2),
        label('lid_label', 'DEA', (.045, .045), .022, .0686, white),
    ]
    lid = join('Lid', lid_parts)
    set_origin(lid, (0, .0625, .002))

    # Electrode pad (Unity size .09 x .008 x .12) and its peel-off liner on top.
    electrode = join('PadElectrode', [
        rounded_box('pad_foam', U(0, 0, 0), S(.09, .006, .12), .012, foam, 4),
        rounded_box('pad_gel', U(0, -.0025, 0), S(.074, .002, .102), .01, gel, 3),
    ])
    torso = [(-.022, .036), (.022, .036), (.026, .01), (.022, -.04), (-.022, -.04), (-.026, .01)]
    backing = join('PadBacking', [
        rounded_box('liner', U(0, .0045, 0), S(.087, .003, .117), .011, liner, 4),
        flat_shape('liner_torso', torso, .006, .0006, white),
        flat_shape('liner_head', [(.0 + .009 * math.cos(a), .046 + .009 * math.sin(a))
                                  for a in [i * 2 * math.pi / 18 for i in range(18)]], .006, .0006, white),
        rounded_box('liner_pad_mark', U(-.009, .0068, .02), S(.012, .0006, .016), .002, print_ink, 1),
    ])
    # Every node keeps its pivot where Unity expects it: device parts on the device origin,
    # pad layers on the pad origin (the liner sits 4.5 mm above it), lid on its hinge.
    for o in (body, electrode, backing):
        set_origin(o, (0, 0, 0))
    for o in (body, lid, electrode, backing):
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.shade_auto_smooth(angle=math.radians(40))
    return body, lid, electrode, backing


def export():
    OUT.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.export_scene.gltf(filepath=str(OUT), export_format='GLB', export_yup=True,
                              export_apply=True, export_materials='EXPORT', use_selection=False)
    print('EXPORTED', OUT, OUT.stat().st_size, 'bytes')


def render(path, body, lid, electrode, backing, open_lid=True):
    # Preview only: AED with lid open and both pads beside it on a neutral table.
    import mathutils
    if open_lid:
        lid.rotation_mode = 'XYZ'
        # Unity Euler(-95,0,0) about Unity x == Blender -x axis.
        lid.rotation_euler = (math.radians(-95), 0, 0)
    for i, x in enumerate((.22, .33)):
        e = electrode.copy(); e.data = electrode.data; bpy.context.collection.objects.link(e)
        e.location = U(x, -.055, -.02)
        if i == 0:
            b = backing.copy(); b.data = backing.data; bpy.context.collection.objects.link(b)
            b.location = U(x, -.055, -.02)
    table = mat('Table', srgb('#8A8F94'), .6)
    bpy.ops.mesh.primitive_plane_add(size=3, location=U(0, -.06, 0))
    bpy.context.active_object.data.materials.append(table)
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.device = 'CPU'
    scene.cycles.samples = 64
    scene.view_settings.view_transform = 'Standard'
    scene.cycles.use_denoising = True
    scene.render.resolution_x, scene.render.resolution_y = 1400, 1000
    scene.render.film_transparent = False
    world = bpy.data.worlds.new('World'); scene.world = world; world.use_nodes = True
    world.node_tree.nodes['Background'].inputs['Color'].default_value = (.55, .58, .62, 1)
    world.node_tree.nodes['Background'].inputs['Strength'].default_value = .45
    bpy.ops.object.light_add(type='AREA', location=U(-.4, .7, -.5))
    key = bpy.context.active_object; key.data.energy = 35; key.data.size = .6
    key.rotation_euler = (U(0, 0, 0) - key.location).to_track_quat('-Z', 'Y').to_euler()
    bpy.ops.object.light_add(type='AREA', location=U(.6, .4, .4))
    fill = bpy.context.active_object; fill.data.energy = 12; fill.data.size = .8
    fill.rotation_euler = (U(0, 0, 0) - fill.location).to_track_quat('-Z', 'Y').to_euler()
    bpy.ops.object.camera_add(location=U(-.05, .42, -.52))
    cam = bpy.context.active_object; cam.data.lens = 50
    cam.rotation_euler = (U(.06, 0, .0) - cam.location).to_track_quat('-Z', 'Y').to_euler()
    scene.camera = cam
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    print('RENDERED', path)


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--render')
    parser.add_argument('--closed', action='store_true')
    args = parser.parse_args(sys.argv[1:])
    parts = build()
    export()
    if args.render:
        render(args.render, *parts, open_lid=not args.closed)
