using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CandyFlickEditor
{
    public static class CandySkinAssetsBuilder
    {
        const string Folder="Assets/Resources/CandySkins";
        static void Bake(GameObject obj,string source,Scene scene,StringBuilder report)
        {
            Quaternion orientation=source=="bagel"?Quaternion.Euler(-90,0,0):Quaternion.identity;
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();
            foreach(var filter in obj.GetComponentsInChildren<MeshFilter>())
            {
                Mesh mesh=filter.sharedMesh;int offset=vertices.Count;
                for(int i=0;i<mesh.vertexCount;i++)
                {
                    vertices.Add(orientation*filter.transform.TransformPoint(mesh.vertices[i]));
                    normals.Add((orientation*filter.transform.TransformDirection(mesh.normals[i])).normalized);uv.Add(mesh.uv[i]);
                }
                foreach(int index in mesh.triangles)indices.Add(offset+index);
            }
            Bounds bounds=new Bounds(vertices[0],Vector3.zero);foreach(var vertex in vertices)bounds.Encapsulate(vertex);
            float size=Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);
            for(int i=0;i<vertices.Count;i++)vertices[i]=(vertices[i]-bounds.center)/size;
            string name=source=="bagel"?"Donut":"Cupcake",meshPath=Folder+"/"+name+".asset",materialPath=Folder+"/"+name+".mat";
            var baked=new Mesh{name=name};baked.SetVertices(vertices);baked.SetNormals(normals);baked.SetUVs(0,uv);baked.SetTriangles(indices,0);baked.RecalculateBounds();
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);if(existing){EditorUtility.CopySerialized(baked,existing);UnityEngine.Object.DestroyImmediate(baked);baked=existing;}else AssetDatabase.CreateAsset(baked,meshPath);
            var native=Resources.Load<GameObject>("Original26/Geometry").transform.Find("Candies/Candy1/zCandy").GetComponent<Renderer>().sharedMaterial;
            var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);if(!material){material=new Material(native);AssetDatabase.CreateAsset(material,materialPath);}else material.CopyPropertiesFromMaterial(native);
            material.shader=source=="cupcake"?Shader.Find("Toon/Basic"):native.shader;
            material.SetShaderPassEnabled("OUTLINE",source!="cupcake");
            material.name=name;material.mainTexture=Resources.Load<Texture2D>("CandySkins/Models/"+source);EditorUtility.SetDirty(material);
            var root=new GameObject(name);SceneManager.MoveGameObjectToScene(root,scene);
            root.AddComponent<MeshFilter>().sharedMesh=baked;root.AddComponent<MeshRenderer>().sharedMaterial=material;
            PrefabUtility.SaveAsPrefabAsset(root,Folder+"/"+name+".prefab");UnityEngine.Object.DestroyImmediate(root);
            report.AppendLine(name+" normalized bounds="+baked.bounds+" shader="+material.shader.name);
        }
        public static void Build()
        {
            AssetDatabase.Refresh();
            foreach(string file in new[]{"Models/bagel.fbx","Models/cupcake.obj"})
            {
                var importer=(ModelImporter)AssetImporter.GetAtPath(Folder+"/"+file);
                importer.importAnimation=false;importer.isReadable=true;importer.addCollider=false;
                importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.SaveAndReimport();
            }
            foreach(string file in new[]{"Models/bagel.png","Models/cupcake.png"})
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(Folder+"/"+file);
                importer.mipmapEnabled=false;importer.npotScale=TextureImporterNPOTScale.None;importer.alphaIsTransparency=true;
                importer.wrapMode=file=="Models/cupcake.png"?TextureWrapMode.Repeat:TextureWrapMode.Clamp;importer.isReadable=true;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            }
            var scene=EditorSceneManager.NewPreviewScene();var report=new StringBuilder();
            try
            {
                foreach(string model in new[]{"bagel","cupcake"})
                {
                    var obj=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("CandySkins/Models/"+model));SceneManager.MoveGameObjectToScene(obj,scene);
                    var texture=Resources.Load<Texture2D>("CandySkins/Models/"+model);Vector3 pink=Vector3.zero,normal=Vector3.zero;int count=0;
                    Bounds bounds=default;bool first=true;
                    foreach(var mf in obj.GetComponentsInChildren<MeshFilter>())
                    {
                        var mesh=mf.sharedMesh;var vertices=mesh.vertices;var uv=mesh.uv;var normals=mesh.normals;
                        for(int i=0;i<vertices.Length;i++)
                        {
                            Vector3 point=mf.transform.TransformPoint(vertices[i]);if(first){bounds=new Bounds(point,Vector3.zero);first=false;}else bounds.Encapsulate(point);
                            Color color=texture.GetPixelBilinear(uv[i].x,uv[i].y);
                            if(color.r>.5f&&color.r>color.g*1.35f&&color.b>color.g*1.2f)
                            {pink+=point;normal+=mf.transform.TransformDirection(normals[i]);count++;}
                        }
                    }
                    report.AppendLine(model+" bounds="+bounds+" pink center="+(count>0?pink/count:Vector3.zero)+" normal="+normal.normalized);
                    Bake(obj,model,scene,report);UnityEngine.Object.DestroyImmediate(obj);
                }
                var geometry=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Original26/Geometry"));SceneManager.MoveGameObjectToScene(geometry,scene);
                foreach(string path in new[]{"Player/CandyGhost","Candies/Candy1"})
                {
                    var t=geometry.transform.Find(path);report.AppendLine(path+" position="+t.position+" rotation="+t.rotation.eulerAngles+" scale="+t.lossyScale);
                    foreach(var r in t.GetComponentsInChildren<Renderer>(true))report.AppendLine("  "+r.name+" "+r.GetType().Name+" bounds="+r.bounds+" scale="+r.transform.lossyScale+" shader="+(r.sharedMaterial?r.sharedMaterial.shader.name:""));
                    foreach(var c in t.GetComponentsInChildren<Collider>(true))report.AppendLine("  "+c.name+" "+c.GetType().Name+" enabled="+c.enabled+" bounds="+c.bounds);
                }
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);}
            foreach(string file in new[]{"Models/bagel.png","Models/cupcake.png"})
            {var importer=(TextureImporter)AssetImporter.GetAtPath(Folder+"/"+file);importer.isReadable=false;importer.SaveAndReimport();}
            AssetDatabase.SaveAssets();File.WriteAllText("Documentation/candy-skin-assets.txt",report.ToString());
            CandyPortraitsBuilder.Build();
        }
    }
}
