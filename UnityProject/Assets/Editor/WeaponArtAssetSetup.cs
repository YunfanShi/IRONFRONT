using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    public static class WeaponArtAssetSetup
    {
        const string Root="Assets/Art/Weapons/IF27";
        public static void BuildPlayer()
        {
            Build();
            PrototypeOptimizationBuild.Build();
        }
        [MenuItem("IRONFRONT/Art/Build IF27 Weapon")]
        public static void Build()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Directory.CreateDirectory("Assets/Resources/Weapons");AssetDatabase.Refresh();
            foreach(string path in Directory.GetFiles(Root,"*.fbx"))
            {
                var importer=(ModelImporter)AssetImporter.GetAtPath(path);
                importer.globalScale=1;importer.useFileUnits=true;importer.bakeAxisConversion=false;
                importer.importAnimation=false;importer.importCameras=false;importer.importLights=false;
                importer.isReadable=true;importer.materialImportMode=ModelImporterMaterialImportMode.None;
                importer.SaveAndReimport();
            }
            var baseMap=Texture("basecolor",true,false);var normal=Texture("normal",false,true);
            var orm=Texture("metalrough",false,false,true);
            var pixels=orm.GetPixels32();
            for(int i=0;i<pixels.Length;i++)pixels[i]=new Color32(pixels[i].b,0,0,(byte)(255-pixels[i].g));
            var packed=new Texture2D(orm.width,orm.height,TextureFormat.RGBA32,false,true);
            packed.SetPixels32(pixels);packed.Apply();File.WriteAllBytes(Root+"/if27_mask.png",packed.EncodeToPNG());UnityEngine.Object.DestroyImmediate(packed);
            AssetDatabase.Refresh();var mask=Texture("mask",false,false);
            Material pbr=Material("IF27_Surface"),inside=Material("IF27_Interior");
            pbr.SetTexture("_BaseMap",baseMap);pbr.SetColor("_BaseColor",new Color(.66f,.70f,.74f));
            pbr.SetTexture("_BumpMap",normal);pbr.SetFloat("_BumpScale",.6f);pbr.EnableKeyword("_NORMALMAP");
            pbr.SetTexture("_MetallicGlossMap",mask);pbr.SetFloat("_Smoothness",.55f);pbr.EnableKeyword("_METALLICSPECGLOSSMAP");
            inside.SetColor("_BaseColor",new Color(.045f,.05f,.055f));inside.SetFloat("_Metallic",.5f);inside.SetFloat("_Smoothness",.32f);
            EditorUtility.SetDirty(pbr);EditorUtility.SetDirty(inside);
            BuildPrefab(true,pbr,inside);BuildPrefab(false,pbr,inside);
            AssetDatabase.SaveAssets();Debug.Log("IRONFRONT IF27 asset PASS: authored view/world prefabs, anchors, four LODs, URP maps");
        }
        static Texture2D Texture(string role,bool srgb,bool normal,bool readable=false)
        {
            string path=Root+"/if27_"+role+".png";
            var i=(TextureImporter)AssetImporter.GetAtPath(path);i.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
            i.sRGBTexture=srgb;i.isReadable=readable;i.maxTextureSize=2048;i.mipmapEnabled=true;
            i.textureCompression=TextureImporterCompression.CompressedHQ;i.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        static Material Material(string name)
        {
            string path=Root+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}return m;
        }
        static Transform Find(Transform root,string name)=>root.GetComponentsInChildren<Transform>().Single(t=>t.name==name);
        static void BuildPrefab(bool view,Material pbr,Material inside)
        {
            var root=new GameObject("IF27"+(view?"View":"World"));
            try
            {
                var rig=root.AddComponent<PrototypeWeaponArtRig>();
                var parts=new Dictionary<string,Transform>();
                foreach(string name in new[]{"Body","Magazine","ChargingHandle","Bolt","Stock"})
                {var p=new GameObject(name).transform;p.SetParent(root.transform,false);parts[name]=p;}
                parts["Magazine"].localPosition=new Vector3(0,-.1392f,.0336f);
                parts["ChargingHandle"].localPosition=new Vector3(0,.06f,-.19f);
                var lods=new List<LOD>();int first=view?0:1,last=view?0:3;
                for(int level=first;level<=last;level++)
                {
                    var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/if27_lod"+level+".fbx");
                    var instance=UnityEngine.Object.Instantiate(asset,root.transform,false);
                    instance.transform.localRotation=Quaternion.Euler(-90,0,0);
                    if(level==first)
                    {
                        foreach(string name in new[]{"RightGrip","LeftGrip","Muzzle","Sight","MagazineGrip","ChargingGrip"})
                        {
                            var a=Find(instance.transform,name);a.SetParent(root.transform,true);a.rotation=Quaternion.identity;a.localScale=Vector3.one;
                        }
                        var muzzle=Find(root.transform,"Muzzle").localPosition;
                        Debug.Log("IF27 imported muzzle="+muzzle+" right="+Find(root.transform,"RightGrip").localPosition);
                        if(muzzle.z<.7f||Mathf.Abs(muzzle.y)>.03f)throw new InvalidOperationException("IF27 FBX axes incorrect: "+muzzle);
                    }
                    var renderers=new List<Renderer>();
                    foreach(var pair in parts)
                    {
                        var mesh=Find(instance.transform,pair.Key);mesh.SetParent(pair.Value,true);mesh.name="LOD"+level;
                        var mr=mesh.GetComponent<MeshRenderer>();
                        mr.sharedMaterials=mr.sharedMaterials.Select(m=>m!=null&&m.name.Contains("Interior")?inside:pbr).ToArray();
                        // Imported material slots are deterministic even with material import disabled.
                        mr.sharedMaterials=pair.Key=="Bolt"?new[]{inside}:new[]{pbr,inside};
                        renderers.Add(mr);
                    }
                    UnityEngine.Object.DestroyImmediate(instance);
                    lods.Add(new LOD(view?.005f:level==1?.20f:level==2?.07f:.008f,renderers.ToArray()));
                }
                rig.Magazine=parts["Magazine"];rig.ChargingHandle=parts["ChargingHandle"];rig.Bolt=parts["Bolt"];
                rig.RightGrip=Find(root.transform,"RightGrip");rig.LeftGrip=Find(root.transform,"LeftGrip");
                rig.Muzzle=Find(root.transform,"Muzzle");rig.Sight=Find(root.transform,"Sight");
                rig.MagazineGrip=Find(root.transform,"MagazineGrip");rig.MagazineGrip.SetParent(rig.Magazine,true);
                rig.ChargingGrip=Find(root.transform,"ChargingGrip");rig.ChargingGrip.SetParent(rig.ChargingHandle,true);
                var group=root.AddComponent<LODGroup>();group.SetLODs(lods.ToArray());group.RecalculateBounds();
                var dot=GameObject.CreatePrimitive(PrimitiveType.Sphere);dot.name="Sight aiming dot";UnityEngine.Object.DestroyImmediate(dot.GetComponent<Collider>());
                dot.transform.SetParent(rig.Sight,false);dot.transform.localPosition=Vector3.forward*.018f;dot.transform.localScale=Vector3.one*.003f;
                string dotPath=Root+"/IF27_Reticle.mat";var dotMaterial=AssetDatabase.LoadAssetAtPath<Material>(dotPath);
                if(dotMaterial==null){dotMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));dotMaterial.SetColor("_BaseColor",new Color(1,.08f,.015f));AssetDatabase.CreateAsset(dotMaterial,dotPath);}
                dot.GetComponent<Renderer>().sharedMaterial=dotMaterial;
                if(view)group.ForceLOD(0);
                PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/Weapons/"+root.name+".prefab");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
    }
}
