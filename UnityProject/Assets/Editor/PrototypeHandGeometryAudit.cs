using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace Ironfront.UnityPrototype.Editor
{
    // Read evaluated vertex groups, independently of the thumb direction marker.
    public static class PrototypeHandGeometryAudit
    {
        [Serializable] class Point { public Vector3 p; public float thumb, fingers; }
        [Serializable] class Pose { public float phase; public Vector3 wrist,knuckle,thumbRoot,thumbAxis,radial; public Quaternion thumbRotation; public Point[] points; }
        [Serializable] class Report { public Pose[] poses; }
        public static void Run()
        {
            string[] args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-handGeometryOutput");
            string output=args[i+1];var camera=new GameObject("Hand audit camera").AddComponent<Camera>();
            var fp=PrototypeWeaponArtRig.Create(camera.transform,PrototypeWeaponId.Pistol,true,PrototypeTeam.Blue);
            var art=fp.GetComponentInChildren<PrototypeSoldierArtRig>();var poses=new List<Pose>();
            foreach(float phase in new[]{-2f,-1f,.24f,.66f,.74f,.87f})
            {
                fp.SetPresentation(phase>=0,Mathf.Max(0,phase),100);
                if(phase==-2f){art.Thumbs[0].localRotation=art.ThumbRest[0];art.Fingers[0].localRotation=art.FingerRest[0];}
                var hand=art.Bindings[7].Bone;var points=new List<Point>();
                foreach(var r in art.Arms.Take(2))
                {
                    int ti=Array.IndexOf(r.bones,art.Thumbs[0]),fi=Array.IndexOf(r.bones,art.Fingers[0]);
                    if(ti<0)continue;
                    var mesh=r.sharedMesh;var vs=mesh.vertices;var weights=mesh.boneWeights;var skin=r.bones.Select((bone,b)=>(bone.localToWorldMatrix*mesh.bindposes[b])).ToArray();
                    for(int n=0;n<vs.Length;n++)
                    {
                        var w=weights[n];int[] bs={w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};float[] ws={w.weight0,w.weight1,w.weight2,w.weight3};float t=0,f=0;
                        for(int j=0;j<4;j++){if(bs[j]==ti)t+=ws[j];if(bs[j]==fi)f+=ws[j];}
                        if(t>.5f || f>.5f)
                        {
                            Vector3 world=Vector3.zero;for(int j=0;j<4;j++)if(ws[j]>0)world+=skin[bs[j]].MultiplyPoint3x4(vs[n])*ws[j];
                            points.Add(new Point {p=fp.transform.InverseTransformPoint(world),thumb=t,fingers=f});
                        }
                    }

                }
                poses.Add(new Pose {phase=phase,thumbRotation=Quaternion.Inverse(fp.transform.rotation)*art.Thumbs[0].rotation,wrist=fp.transform.InverseTransformPoint(hand.position),knuckle=fp.transform.InverseTransformPoint(art.Fingers[0].position),thumbRoot=fp.transform.InverseTransformPoint(art.Thumbs[0].position),thumbAxis=fp.transform.InverseTransformDirection(art.LeftThumbSurfaceDirection),radial=fp.transform.InverseTransformDirection(art.Fingers[0].rotation*new Vector3(.096511f,.027319f,-.994957f).normalized),points=points.ToArray()});
            }
            File.WriteAllText(output,JsonUtility.ToJson(new Report {poses=poses.ToArray()},true));
            Debug.Log("HAND_GEOMETRY_AUDIT_PASS poses="+poses.Count+" vertices="+poses[0].points.Length);
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
        }
    }
}
