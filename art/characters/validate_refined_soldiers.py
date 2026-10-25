"""Fresh-file FBX round-trip audit. Never saves over authored assets."""
import bpy, json
from pathlib import Path
ROOT=Path(__file__).resolve().parent
reports=[]
for team in ('blue','red'):
    for lod in range(3):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        path=ROOT/(team+'-soldier')/'rigged-v02'/(team+'-soldier-skinned-lod'+str(lod)+'.fbx')
        bpy.ops.import_scene.fbx(filepath=str(path))
        rigs=[o for o in bpy.data.objects if o.type=='ARMATURE']
        meshes=[o for o in bpy.data.objects if o.type=='MESH']
        errors=[];max_influences=0;verts=0;tris=0
        if len(rigs)!=1 or len(rigs[0].data.bones)!=18:errors.append('skeleton count')
        if len(meshes)!=3:errors.append('body/arm separation')
        for o in meshes:
            tris+=sum(len(p.vertices)-2 for p in o.data.polygons);verts+=len(o.data.vertices)
            if not any(m.type=='ARMATURE' and m.object==rigs[0] for m in o.modifiers):errors.append('missing skin modifier')
            if len(o.data.uv_layers)!=1:errors.append('UV layers')
            for v in o.data.vertices:
                n=len(v.groups);max_influences=max(max_influences,n)
                if n==0 or n>4 or abs(sum(g.weight for g in v.groups)-1)>.0001:errors.append('bad weight')
        bpy.context.view_layer.update()
        pts=[o.matrix_world@v.co for o in meshes for v in o.data.vertices]
        height=max(p.z for p in pts)-min(p.z for p in pts)
        if abs(height-1.8)>.005:errors.append('height')
        # Exercise elbows, knees and spine in the freshly imported bind poses.
        for n in ('Forearm_L','Forearm_R','Shin_L','Shin_R'):
            p=rigs[0].pose.bones[n];p.rotation_mode='XYZ';p.rotation_euler.x=1.4
        rigs[0].pose.bones['Torso'].rotation_mode='XYZ';rigs[0].pose.bones['Torso'].rotation_euler.y=.4
        bpy.context.view_layer.update();dg=bpy.context.evaluated_depsgraph_get()
        max_extent=0
        for o in meshes:
            evaluated=o.evaluated_get(dg);mesh=evaluated.to_mesh()
            max_extent=max(max_extent,max(v.co.length for v in mesh.vertices));evaluated.to_mesh_clear()
        if max_extent>3:errors.append('pose exploded')
        reports.append(dict(file=str(path.relative_to(ROOT)),bones=len(rigs[0].data.bones),meshes=len(meshes),triangles=tris,vertices=verts,height=height,max_influences=max_influences,posed_extent=max_extent,errors=sorted(set(errors))))
out=ROOT/'motion-refinement-2026-10-24'/'fresh-fbx-audit.json'
out.write_text(json.dumps(reports,indent=2))
if any(r['errors'] for r in reports):raise RuntimeError('FBX audit failed: '+str(out))
print('SOLDIER_FBX_AUDIT_PASS',len(reports),flush=True)
