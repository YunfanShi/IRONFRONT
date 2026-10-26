using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Ironfront.UnityPrototype.Editor
{
    public static class Batch2WeaponAssetSetup
    {
        const string Root="Assets/Art/Weapons/Batch2/";
        static readonly PrototypeWeaponId[] Ids={PrototypeWeaponId.BattleRifle,PrototypeWeaponId.Lmg,
            PrototypeWeaponId.Marksman,PrototypeWeaponId.Pistol,PrototypeWeaponId.Sniper,PrototypeWeaponId.Smg};
        public static void BuildPlayer(){Build();PrototypeOptimizationBuild.Build();}
        [MenuItem("IRONFRONT/Art/Build six replacement weapons")]
        public static void Build()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Directory.CreateDirectory("Assets/Resources/Weapons/Batch2");AssetDatabase.Refresh();
            foreach(var id in Ids)
            {
                string folder=Root+id+"/";
                foreach(string path in Directory.GetFiles(folder,"*.fbx"))
                {
                    var i=(ModelImporter)AssetImporter.GetAtPath(path);
                    i.globalScale=1;i.useFileUnits=true;i.bakeAxisConversion=false;i.importAnimation=false;
                    i.importCameras=false;i.importLights=false;i.isReadable=true;
                    i.materialImportMode=ModelImporterMaterialImportMode.None;i.SaveAndReimport();
                }
                var surface=Material(folder+"Surface.mat","Universal Render Pipeline/Lit");
                surface.SetColor("_BaseColor",Color.white);surface.SetTexture("_BaseMap",Texture(folder+"BaseColor.png",true,false));
                surface.SetTexture("_BumpMap",Texture(folder+"Normal.png",false,true));
                surface.SetFloat("_BumpScale",.65f);surface.EnableKeyword("_NORMALMAP");
                surface.SetTexture("_MetallicGlossMap",Texture(folder+"MetallicSmoothness.png",false,false));
                surface.SetFloat("_Smoothness",.6f);surface.EnableKeyword("_METALLICSPECGLOSSMAP");
                var interior=Material(folder+"Interior.mat","Universal Render Pipeline/Lit");
                interior.SetColor("_BaseColor",new Color(.035f,.04f,.045f));interior.SetFloat("_Metallic",.4f);interior.SetFloat("_Smoothness",.25f);
                EditorUtility.SetDirty(surface);EditorUtility.SetDirty(interior);
                BuildPrefab(id,true,surface,interior);BuildPrefab(id,false,surface,interior);
            }
            PlayerSettings.bundleVersion="0.65.0";AssetDatabase.SaveAssets();
            Debug.Log("BATCH2_WEAPONS_READY: six authored weapon pairs, three LODs, mapped URP textures and articulated magazines");
        }
        static Texture2D Texture(string path,bool srgb,bool normal)
        {
            var i=(TextureImporter)AssetImporter.GetAtPath(path);i.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
            i.sRGBTexture=srgb;i.isReadable=false;i.maxTextureSize=2048;i.mipmapEnabled=true;
            i.textureCompression=TextureImporterCompression.CompressedHQ;i.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        static Material Material(string path,string shader)
        {var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(Shader.Find(shader));AssetDatabase.CreateAsset(m,path);}return m;}
        static Transform Find(Transform root,string name)=>root.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);
        static void BuildPrefab(PrototypeWeaponId id,bool view,Material surface,Material interior)
        {
            var root=new GameObject(id+(view?"View":"World"));
            try
            {
                var rig=root.AddComponent<PrototypeWeaponArtRig>();var parts=new Dictionary<string,Transform>();
                foreach(string name in new[]{"Body","Magazine","ChargingHandle","Bolt"})
                {var t=new GameObject(name).transform;t.SetParent(root.transform,false);parts[name]=t;}
                var lods=new List<LOD>();
                for(int level=0;level<(view?1:3);level++)
                {
                    var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Root+id+"/LOD"+level+".fbx");
                    var instance=UnityEngine.Object.Instantiate(asset,root.transform,false);
                    // These FBXs export meshes and anchors directly under the
                    // file root; Unity already converts their Z-up coordinates.
                    instance.transform.localRotation=Quaternion.identity;
                    if(level==0)
                    {
                        foreach(string name in new[]{"RightGrip","LeftGrip","Muzzle","Sight","MagazineGrip","ChargingGrip"})
                        {var t=Find(instance.transform,name);t.SetParent(root.transform,true);t.localRotation=Quaternion.identity;t.localScale=Vector3.one;}
                        parts["Magazine"].localPosition=Find(root.transform,"MagazineGrip").localPosition;
                        parts["ChargingHandle"].localPosition=Find(root.transform,"ChargingGrip").localPosition;
                        parts["Bolt"].localPosition=parts["ChargingHandle"].localPosition;
                    }
                    var renderers=new List<Renderer>();
                    foreach(var mesh in instance.GetComponentsInChildren<MeshFilter>())
                    {
                        string part=mesh.name;if(!parts.ContainsKey(part))throw new InvalidOperationException("Unexpected weapon part "+part);
                        mesh.transform.SetParent(parts[part],true);mesh.name="LOD"+level;
                        var mr=mesh.GetComponent<MeshRenderer>();mr.sharedMaterials=Enumerable.Range(0,mesh.sharedMesh.subMeshCount).Select(i=>i==0?surface:interior).ToArray();renderers.Add(mr);
                    }
                    UnityEngine.Object.DestroyImmediate(instance);
                    lods.Add(new LOD(view?.005f:level==0?.28f:level==1?.09f:.008f,renderers.ToArray()));
                }
                rig.Magazine=parts["Magazine"];rig.ChargingHandle=parts["ChargingHandle"];rig.Bolt=parts["Bolt"];
                rig.RightGrip=Find(root.transform,"RightGrip");rig.LeftGrip=Find(root.transform,"LeftGrip");rig.Muzzle=Find(root.transform,"Muzzle");rig.Sight=Find(root.transform,"Sight");
                rig.MagazineGrip=Find(root.transform,"MagazineGrip");rig.MagazineGrip.SetParent(rig.Magazine,true);
                rig.ChargingGrip=Find(root.transform,"ChargingGrip");rig.ChargingGrip.SetParent(rig.ChargingHandle,true);
                bool pistol=id==PrototypeWeaponId.Pistol,scope=id==PrototypeWeaponId.Marksman||id==PrototypeWeaponId.Sniper;
                rig.BoltTravel=pistol?.050f:0f;rig.ChargingTravel=pistol?.050f:.085f;
                rig.CycleHandleOnShot=id==PrototypeWeaponId.Sniper;rig.UsesScope=scope;rig.EyeRelief=scope?.075f:pistol?.45f:.38f;
                rig.MagazineTravel=pistol?new Vector3(-.02f,-.20f,-.055f):new Vector3(-.035f,-.30f,-.055f);
                if(pistol){rig.HipPosition=new Vector3(.18f,-.15f,.49f);rig.LeftHandRotation=Quaternion.Euler(0,0,40);}
                if(id==PrototypeWeaponId.Smg)rig.HipPosition=new Vector3(.23f,-.20f,.58f);
                var group=root.AddComponent<LODGroup>();group.SetLODs(lods.ToArray());group.RecalculateBounds();
                if(!pistol)
                {
                    var dot=GameObject.CreatePrimitive(PrimitiveType.Sphere);dot.name="Sight aiming dot";UnityEngine.Object.DestroyImmediate(dot.GetComponent<Collider>());
                    dot.transform.SetParent(rig.Sight,false);dot.transform.localPosition=Vector3.forward*.005f;dot.transform.localScale=Vector3.one*(scope?.0008f:.002f);
                    var material=Material(Root+id+"/Reticle.mat","Universal Render Pipeline/Unlit");material.SetColor("_BaseColor",new Color(1f,.04f,.01f));EditorUtility.SetDirty(material);dot.GetComponent<Renderer>().sharedMaterial=material;
                }
                if(view)group.ForceLOD(0);
                if(rig.Muzzle.localPosition.z<=rig.RightGrip.localPosition.z||Mathf.Abs(rig.Muzzle.localPosition.y)>.005f)
                    throw new InvalidOperationException("Incorrect gun axes "+id+": "+rig.Muzzle.localPosition);
                PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/Weapons/Batch2/"+root.name+".prefab");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
    }
}
