# Barril de DriftPaint, low poly y sin n-gons (solo triángulos y cuadriláteros).
# Uso: blender --background --python barrel.py
# Genera Tools/Blender/Barrel.blend y exporta Assets/3D_Models/Props/Barrel.fbx.
#
# Piezas, una por material para que Unity pueda teñir cada una por separado:
#   Barrel_Body    cuerpo abombado con tapa y fondo
#   Barrel_Gold    segmentos dorados de los aros de arriba y abajo
#   Barrel_Stripe  segmentos oscuros de los aros (franjas de peligro)
#   Barrel_Band    banda central: toma el color del equipo (blanca en los pickups)
#   Barrel_Emblem  cuatro estrellas de explosión apoyadas sobre la banda

import math
import os

import bmesh
import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.normpath(os.path.join(HERE, "..", ".."))
FBX_PATH = os.path.join(PROJECT, "Assets", "3D_Models", "Props", "Barrel.fbx")
BLEND_PATH = os.path.join(HERE, "Barrel.blend")

SIDES = 12
HEIGHT = 1.10

# Perfil del cuerpo (radio, altura) de abajo hacia arriba: abombado al centro como un tambor.
BODY_PROFILE = [
    (0.380, 0.02),
    (0.420, 0.12),
    (0.455, 0.33),
    (0.465, 0.56),
    (0.455, 0.79),
    (0.420, 1.00),
    (0.380, 1.10),
]
LID_RIM = 0.335
LID_DEPTH = 0.035

HOOPS = [(0.10, 0.22), (0.90, 1.02)]
HOOP_RELIEF = 0.025
BAND = (0.37, 0.75)
BAND_RELIEF = 0.028

EMBLEM_Z = 0.56
EMBLEM_POINTS = 8
EMBLEM_OUTER = (0.165, 0.118)  # puntas largas y cortas alternadas
EMBLEM_INNER = 0.068
EMBLEM_LIFT = 0.004


def body_radius(z):
    """Radio del cuerpo a una altura, interpolando el perfil."""
    for (r0, z0), (r1, z1) in zip(BODY_PROFILE, BODY_PROFILE[1:]):
        if z0 <= z <= z1:
            t = (z - z0) / (z1 - z0)
            return r0 + (r1 - r0) * t
    return BODY_PROFILE[-1][0]


def ring_point(radius, angle, z):
    return (radius * math.cos(angle), radius * math.sin(angle), z)


def angle_of(i):
    return 2.0 * math.pi * i / SIDES


def new_object(name, material_name, color):
    mesh = bpy.data.meshes.new(name)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    material = bpy.data.materials.get(material_name) or bpy.data.materials.new(material_name)
    material.diffuse_color = color
    mesh.materials.append(material)
    return obj


def lathe(bm, profile, segments=None):
    """Revoluciona un perfil abierto en bandas de cuadriláteros. Devuelve los anillos de vértices."""
    segments = range(SIDES) if segments is None else segments
    rings = [[bm.verts.new(ring_point(r, angle_of(i), z)) for i in range(SIDES)] for r, z in profile]
    for a, b in zip(rings, rings[1:]):
        for i in segments:
            j = (i + 1) % SIDES
            bm.faces.new((a[i], a[j], b[j], b[i]))
    return rings


def fan(bm, ring, center, upward):
    """Tapa un anillo con triángulos alrededor de un vértice central."""
    middle = bm.verts.new(center)
    for i in range(SIDES):
        j = (i + 1) % SIDES
        bm.faces.new((ring[i], ring[j], middle) if upward else (ring[j], ring[i], middle))


def finish(obj, bm, smooth_angle=35.0):
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bm.verts.ensure_lookup_table()
    bm.to_mesh(obj.data)
    bm.free()
    for poly in obj.data.polygons:
        poly.use_smooth = True
    obj.data.set_sharp_from_angle(angle=math.radians(smooth_angle))
    obj.data.validate()


def build_body():
    obj = new_object("Barrel_Body", "Barrel_Body", (0.16, 0.15, 0.2, 1))
    bm = bmesh.new()
    profile = BODY_PROFILE + [(LID_RIM, HEIGHT), (LID_RIM, HEIGHT - LID_DEPTH)]
    rings = lathe(bm, profile)
    fan(bm, rings[-1], (0, 0, HEIGHT - LID_DEPTH), upward=True)
    fan(bm, rings[0], (0, 0, BODY_PROFILE[0][1]), upward=False)
    finish(obj, bm)
    return obj


def raised_profile(z0, z1, relief):
    """Perfil de un aro en relieve con los bordes biselados."""
    bevel = min(0.02, (z1 - z0) * 0.25)
    r0, r1 = body_radius(z0), body_radius(z1)
    return [
        (r0 - 0.002, z0),
        (r0 + relief, z0 + bevel),
        (r1 + relief, z1 - bevel),
        (r1 - 0.002, z1),
    ]


def build_hoops(name, material_name, color, parity):
    obj = new_object(name, material_name, color)
    bm = bmesh.new()
    segments = [i for i in range(SIDES) if i % 2 == parity]
    for z0, z1 in HOOPS:
        lathe(bm, raised_profile(z0, z1, HOOP_RELIEF), segments)
    # Se borran los vértices sueltos de los segmentos que le tocan a la otra pieza.
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    finish(obj, bm, smooth_angle=20.0)
    return obj


def build_band():
    obj = new_object("Barrel_Band", "Barrel_Band", (1, 1, 1, 1))
    bm = bmesh.new()
    lathe(bm, raised_profile(BAND[0], BAND[1], BAND_RELIEF))
    finish(obj, bm)
    return obj


def build_emblems():
    obj = new_object("Barrel_Emblem", "Barrel_Emblem", (0.1, 0.1, 0.13, 1))
    bm = bmesh.new()
    surface = body_radius(EMBLEM_Z) + BAND_RELIEF + EMBLEM_LIFT
    for k in range(4):
        facing = math.pi * 0.5 * k + math.pi / SIDES
        # Cada punto de la estrella se enrolla sobre la banda: el ancho pasa a ángulo sobre el cilindro.
        def wrap(u, v):
            a = facing + u / surface
            return bm.verts.new((surface * math.cos(a), surface * math.sin(a), EMBLEM_Z + v))

        center = wrap(0.0, 0.0)
        rim = []
        for p in range(EMBLEM_POINTS * 2):
            spin = math.pi * p / EMBLEM_POINTS + math.pi * 0.5
            if p % 2 == 0:
                radius = EMBLEM_OUTER[(p // 2) % 2]
            else:
                radius = EMBLEM_INNER
            rim.append(wrap(radius * math.cos(spin), radius * math.sin(spin)))
        count = len(rim)
        for p in range(count):
            q = (p + 1) % count
            # Orden de los vértices para que la cara mire hacia afuera del barril.
            bm.faces.new((center, rim[q], rim[p]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    for face in bm.faces:
        outward = face.calc_center_median()
        outward.z = 0.0
        if face.normal.dot(outward) < 0:
            face.normal_flip()
    finish(obj, bm, smooth_angle=60.0)
    return obj


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    parts = [
        build_body(),
        build_hoops("Barrel_Gold", "Barrel_Gold", (0.95, 0.68, 0.2, 1), parity=0),
        build_hoops("Barrel_Stripe", "Barrel_Stripe", (0.1, 0.1, 0.13, 1), parity=1),
        build_band(),
        build_emblems(),
    ]

    os.makedirs(os.path.dirname(FBX_PATH), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    bpy.ops.object.select_all(action="DESELECT")
    for part in parts:
        part.select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=FBX_PATH,
        use_selection=True,
        object_types={"MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=True,
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        bake_anim=False,
    )

    report = []
    for part in parts:
        mesh = part.data
        sizes = [len(p.vertices) for p in mesh.polygons]
        triangles = sum(n - 2 for n in sizes)
        report.append(f"{part.name}: caras {len(sizes)}, tris {triangles}, ngons {sum(1 for n in sizes if n > 4)}")
    total = sum(sum(len(p.vertices) - 2 for p in part.data.polygons) for part in parts)
    report.append(f"Total de triángulos: {total}")
    print("BARREL_REPORT\n" + "\n".join(report))


main()
