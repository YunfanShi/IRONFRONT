import bpy,json
p='art/vehicles/r3-firefly/r3-optimized.blend';bpy.ops.wm.open_mainfile(filepath=p)
o=next(o for o in bpy.data.objects if o.type=='MESH' and 'LOD0' in o.name)
for a,b in [(.56,.60),(.60,.63),(.63,.66),(.66,.72)]:
 pts=[v.co for v in o.data.vertices if a<v.co.y<b]
 print(a,b,[(min(p[i] for p in pts),max(p[i] for p in pts)) for i in range(3)],flush=True)
