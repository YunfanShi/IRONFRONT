"""Verify digit-only deformation against 0.66.7 and reimport all FBXs."""
import bpy, json, hashlib, math
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parent
OLD = ROOT.parent / 'sleeve-width-2026-10-27'

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
    axes = {}
    for side in ['L','R']:
        for finger in ['Index','Middle','Ring','Pinky','Thumb']:
            a=rig.data.bones[finger+'1_'+side]; b=rig.data.bones[finger+'3_'+side]
            axes[finger+'_'+side] = tuple((b.tail_local-a.head_local).normalized())
    return {'bones': digest(bones), 'bodies': bodies, 'hands': hands, 'arms': arms, 'axes':axes}

checks = []
for team in ['blue', 'red']:
    filename = team + '-soldier-hand-candidate.blend'
    before = snapshot(OLD / 'candidate' / team / filename)
    after = snapshot(ROOT / 'candidate' / team / filename)
    assert before['bones'] == after['bones'], 'Rest skeleton changed'
    assert before['bodies'] == after['bodies'], 'Body changed'
    changed = {}
    max_axial=0.0; max_offset=0.0; max_normal_angle=0.0
    for name, new in after['arms'].items():
        old = before['arms'][name]
        assert new['faces'] == old['faces'], 'Arm topology changed'
        assert len(new['vertices']) == len(old['vertices'])
        side='L' if name.endswith('_ArmL') else 'R'
        count = 0
        for a, b in zip(old['vertices'], new['vertices']):
            assert a[0] == b[0] and a[2] == b[2], 'Skin weights changed'
            digits = {g[:-2] for g,w in a[2] if w>0 and g.startswith(('Index','Middle','Ring','Pinky','Thumb'))}
            delta=Vector(b[1])-Vector(a[1])
            if not digits:
                assert a[1] == b[1], 'Non-digit surface changed: '+name+' '+str(a[0])
            else:
                count += delta.length>0
                max_offset=max(max_offset,delta.length)
                fingers={g[:-1] for g in digits}
                if len(fingers)==1:
                    f=next(iter(fingers))
                    axial=abs(delta.dot(Vector(before['axes'][f+'_'+side])))
                    max_axial=max(max_axial,axial)
                    assert axial < 2e-7, ('Digit length shifted',name,a[0],axial)
        # Ensure local widening has not flipped a face or collapsed its area.
        for face in old['faces']:
            a,b,c=[Vector(old['vertices'][i][1]) for i in face[:3]]
            x,y,z=[Vector(new['vertices'][i][1]) for i in face[:3]]
            n=(b-a).cross(c-a); m=(y-x).cross(z-x)
            if n.length>1e-10:
                assert m.length>n.length*.5, 'Face area collapsed'
                angle=n.angle(m);max_normal_angle=max(max_normal_angle,angle)
                assert angle<math.radians(60), 'Face flipped'
        assert count > 0
        changed[name] = count
    assert 0 < max_offset < .003
    checks.append({'team': team, 'bodyGeometryAndWeightsPreserved': True,
                   'restSkeletonPreserved': True, 'allTopologyAndWeightsPreserved': True,
                   'nonDigitSurfaceExactlyPreserved': True, 'changedDigitVertices': changed,
                   'maxOffsetMeters':max_offset,'maxDigitAxialDriftMeters':max_axial,
                   'maxFaceNormalChangeDegrees':math.degrees(max_normal_angle)})
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
print('FINGER_ASSET_VALIDATION_PASS')
