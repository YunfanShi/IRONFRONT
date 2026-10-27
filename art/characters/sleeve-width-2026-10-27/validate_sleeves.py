"""Compare sleeve revision with the accepted hand candidate; reimport all FBXs."""
import bpy, json, hashlib, math
from pathlib import Path

ROOT = Path(__file__).resolve().parent
OLD = ROOT.parent / 'sleeve-volume-2026-10-27'

def digest(value):
    return hashlib.sha256(json.dumps(value, sort_keys=True).encode()).hexdigest()

def mesh_record(obj, subset=None):
    ids = set(range(len(obj.data.vertices))) if subset is None else subset
    return {
        'vertices': [(v.index, tuple(v.co), sorted((obj.vertex_groups[g.group].name, g.weight) for g in v.groups))
                     for v in obj.data.vertices if v.index in ids],
        'faces': [tuple(p.vertices) for p in obj.data.polygons if all(i in ids for i in p.vertices)],
    }

def hand_vertices(obj):
    adjacency = [[] for _ in obj.data.vertices]
    for edge in obj.data.edges:
        a, b = edge.vertices
        adjacency[a].append(b); adjacency[b].append(a)
    seeds = {v.index for v in obj.data.vertices if any(
        g.weight > .001 and obj.vertex_groups[g.group].name.startswith(('Index', 'Middle', 'Ring', 'Pinky', 'Thumb'))
        for g in v.groups)}
    stack = list(seeds)
    while stack:
        for n in adjacency[stack.pop()]:
            if n not in seeds:
                seeds.add(n); stack.append(n)
    assert seeds
    return seeds

def snapshot(path):
    bpy.ops.wm.open_mainfile(filepath=str(path))
    rig = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    bones = [(b.name, b.parent.name if b.parent else None, tuple(b.head_local), tuple(b.tail_local),
              tuple(tuple(row) for row in b.matrix_local)) for b in rig.data.bones]
    bodies, hands, arms = {}, {}, {}
    for obj in bpy.data.objects:
        if obj.type != 'MESH': continue
        if obj.name.endswith('_Body'):
            bodies[obj.name] = digest(mesh_record(obj))
        elif obj.name.endswith(('_ArmL', '_ArmR')):
            ids = hand_vertices(obj)
            hands[obj.name] = {'hash': digest(mesh_record(obj, ids)), 'vertices': len(ids)}
            arms[obj.name] = mesh_record(obj)
    return {'bones': digest(bones), 'bodies': bodies, 'hands': hands, 'arms': arms}

checks = []
for team in ['blue', 'red']:
    filename = team + '-soldier-hand-candidate.blend'
    before = snapshot(OLD / 'candidate' / team / filename)
    after = snapshot(ROOT / 'candidate' / team / filename)
    assert before['bones'] == after['bones'], 'Rest skeleton changed'
    assert before['bodies'] == after['bodies'], 'Body changed'
    assert before['hands'] == after['hands'], 'Glove topology/skin changed'
    changed = {}
    for name, new in after['arms'].items():
        old = before['arms'][name]
        assert new['faces'] == old['faces'], 'Arm topology changed'
        assert len(new['vertices']) == len(old['vertices'])
        count = 0
        for a, b in zip(old['vertices'], new['vertices']):
            assert a[0] == b[0] and a[2] == b[2], 'Skin weights changed'
            count += a[1] != b[1]
        assert count > 0
        changed[name] = count
    checks.append({'team': team, 'bodyGeometryAndWeightsPreserved': True,
                   'restSkeletonPreserved': True, 'gloveGeometryAndWeightsPreserved': True,
                   'allArmTopologyAndWeightsPreserved': True, 'hands': after['hands'],
                   'changedSleeveAndPadVertices': changed})
    for lod in range(3):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=str(ROOT / 'candidate' / team / (team + '-soldier-skinned-lod' + str(lod) + '.fbx')))
        rig = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
        assert len(rig.data.bones) == 46
        assert not any(n.startswith('Fingers_') for n in rig.data.bones.keys())
        expected = [f + str(j) + '_' + side for side in ['L','R']
                    for f in ['Index','Middle','Ring','Pinky','Thumb'] for j in range(1,4)]
        assert all(n in rig.data.bones for n in expected)
        vertices, max_influences, max_error = 0, 0, 0
        for obj in bpy.data.objects:
            if obj.type != 'MESH': continue
            for v in obj.data.vertices:
                weights = [g.weight for g in v.groups if g.weight > 1e-6]
                max_influences = max(max_influences, len(weights))
                max_error = max(max_error, abs(sum(weights) - 1))
                assert weights and len(weights) <= 4 and abs(sum(weights) - 1) < 2e-5
                assert all(math.isfinite(x) for x in v.co)
            vertices += len(obj.data.vertices)
        checks.append({'team': team, 'lod': lod, 'freshImport': True, 'bones': 46, 'digitJoints': 30,
                       'vertices': vertices, 'maxInfluences': max_influences, 'maxWeightError': max_error})

baseline = json.loads((ROOT / 'baseline.json').read_text())
# Runtime source hashes are also checked outside Blender in the delivery report.
(ROOT / 'validation.json').write_text(json.dumps({'pass': True, 'checks': checks}, indent=2))
print('SLEEVE_ASSET_VALIDATION_PASS')
