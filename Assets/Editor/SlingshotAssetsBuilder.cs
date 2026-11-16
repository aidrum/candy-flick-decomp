using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace CandyFlickEditor
{
    public static class SlingshotAssetsBuilder
    {
        const string Folder="Assets/Resources/Slingshot";
        [Serializable]class Layout{public Vector3 left,right,mountLeft,mountRight,releaseLeft,releaseRight,handle;}
        public static void Build()
        {
            AssetDatabase.Refresh();
            var importer=(ModelImporter)AssetImporter.GetAtPath(Folder+"/Models/CandyFlickSlingshot.fbx");importer.importAnimation=false;importer.isReadable=true;importer.addCollider=false;importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.SaveAndReimport();
            var textureImporter=(TextureImporter)AssetImporter.GetAtPath(Folder+"/Models/SlingshotTex.png");textureImporter.isReadable=true;textureImporter.mipmapEnabled=true;textureImporter.wrapMode=TextureWrapMode.Clamp;textureImporter.textureCompression=TextureImporterCompression.Uncompressed;textureImporter.SaveAndReimport();
            var scene=EditorSceneManager.NewPreviewScene();
            try
            {
                var obj=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Slingshot/Models/CandyFlickSlingshot"));SceneManager.MoveGameObjectToScene(obj,scene);
                var texture=Resources.Load<Texture2D>("Slingshot/Models/SlingshotTex");
                var positions=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
                foreach(var mf in obj.GetComponentsInChildren<MeshFilter>())
                {
                    var mesh=mf.sharedMesh;int start=positions.Count;
                    for(int i=0;i<mesh.vertexCount;i++){positions.Add(mf.transform.TransformPoint(mesh.vertices[i]));normals.Add(mf.transform.TransformDirection(mesh.normals[i]).normalized);uv.Add(mesh.uv[i]);}
                    triangles.AddRange(mesh.triangles.Select(i=>i+start));
                }
                Bounds bounds=new Bounds(positions[0],Vector3.zero);foreach(var p in positions)bounds.Encapsulate(p);
                Quaternion orientation=bounds.size.z>bounds.size.y?Quaternion.Euler(-90,0,0):Quaternion.identity;
                for(int i=0;i<positions.Count;i++){positions[i]=orientation*positions[i];normals[i]=orientation*normals[i];}
                var capPoints=new List<Vector3>();
                for(int i=0;i<triangles.Count;i+=3)
                {
                    int a=triangles[i],b=triangles[i+1],c=triangles[i+2];Vector2 center=(uv[a]+uv[b]+uv[c])/3;
                    Color color=texture.GetPixelBilinear(center.x,center.y);Color.RGBToHSV(color,out float h,out float saturation,out float value);
                    if(saturation<.68f&&value>.3f&&color.r>color.b*1.2f)capPoints.Add((positions[a]+positions[b]+positions[c])/3);
                }
                if(capPoints.Count<4)throw new Exception("Could not identify the two tan elastic wraps from the supplied UV texture");
                float middle=(positions.Min(p=>p.x)+positions.Max(p=>p.x))/2;
                Vector3 left=capPoints.Where(p=>p.x<middle).Aggregate(Vector3.zero,(a,b)=>a+b)/capPoints.Count(p=>p.x<middle);
                Vector3 right=capPoints.Where(p=>p.x>=middle).Aggregate(Vector3.zero,(a,b)=>a+b)/capPoints.Count(p=>p.x>=middle);
                Vector3 origin=(left+right)/2;float span=Vector3.Distance(left,right);Vector3 xaxis=(right-left).normalized;
                Vector3 up=Vector3.ProjectOnPlane(Vector3.up,xaxis).normalized;
                if(origin.y<(positions.Min(p=>p.y)+positions.Max(p=>p.y))/2)up=-up;
                Vector3 forward=Vector3.Cross(xaxis,up).normalized;
                for(int i=0;i<positions.Count;i++)
                {
                    Vector3 p=positions[i]-origin,n=normals[i];positions[i]=new Vector3(Vector3.Dot(p,xaxis),Vector3.Dot(p,up),Vector3.Dot(p,forward))/span;
                    normals[i]=new Vector3(Vector3.Dot(n,xaxis),Vector3.Dot(n,up),Vector3.Dot(n,forward));
                }
                var baked=new Mesh{name="Visual slingshot"};baked.SetVertices(positions);baked.SetNormals(normals);baked.SetUVs(0,uv);baked.SetTriangles(triangles,0);baked.RecalculateBounds();
                string meshPath=Folder+"/Slingshot.asset";var prior=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);if(prior){EditorUtility.CopySerialized(baked,prior);UnityEngine.Object.DestroyImmediate(baked);baked=prior;}else AssetDatabase.CreateAsset(baked,meshPath);
                var native=Resources.Load<GameObject>("Original26/Geometry").transform.Find("Candies/Candy1/zCandy").GetComponent<Renderer>().sharedMaterial;
                var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Slingshot.mat");if(!material){material=new Material(native);AssetDatabase.CreateAsset(material,Folder+"/Slingshot.mat");}else material.CopyPropertiesFromMaterial(native);
                material.shader=Shader.Find("Toon/Basic");material.mainTexture=texture;EditorUtility.SetDirty(material);
                var root=new GameObject("Visual slingshot");SceneManager.MoveGameObjectToScene(root,scene);root.AddComponent<MeshFilter>().sharedMesh=baked;root.AddComponent<MeshRenderer>().sharedMaterial=material;PrefabUtility.SaveAsPrefabAsset(root,Folder+"/Slingshot.prefab");
                var geometry=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Original26/Geometry"));SceneManager.MoveGameObjectToScene(geometry,scene);
                var player=geometry.transform.Find("Player");var elastic=player.Find("elastic").GetComponentInChildren<SkinnedMeshRenderer>();var source=elastic.sharedMesh;var weights=source.boneWeights;var vertices=source.vertices;
                var points=new List<Vector3>();
                for(int i=0;i<vertices.Length;i++)
                {
                    BoneWeight w=weights[i];Vector3 p=Vector3.zero;
                    foreach(var pair in new[]{(w.boneIndex0,w.weight0),(w.boneIndex1,w.weight1),(w.boneIndex2,w.weight2),(w.boneIndex3,w.weight3)})if(pair.Item2>0)p+=(elastic.bones[pair.Item1].localToWorldMatrix*source.bindposes[pair.Item1]).MultiplyPoint3x4(vertices[i])*pair.Item2;
                    points.Add(player.InverseTransformPoint(p));
                }
                float min=points.Min(p=>p.x),max=points.Max(p=>p.x),epsilon=(max-min)*.002f;
                var leftEdge=points.Where(p=>p.x<min+epsilon).ToArray();var rightEdge=points.Where(p=>p.x>max-epsilon).ToArray();
                var layout=new Layout{left=leftEdge.Aggregate(Vector3.zero,(a,b)=>a+b)/leftEdge.Length,right=rightEdge.Aggregate(Vector3.zero,(a,b)=>a+b)/rightEdge.Length};
                float handleBottom=positions.Min(p=>p.y);var handlePoints=positions.Where(p=>p.y<handleBottom+.08f).ToArray();layout.handle=handlePoints.Aggregate(Vector3.zero,(a,b)=>a+b)/handlePoints.Length;
                bool Deforming(int vertex)
                {
                    var w=weights[vertex];float influence=0;
                    foreach(var pair in new[]{(w.boneIndex0,w.weight0),(w.boneIndex1,w.weight1),(w.boneIndex2,w.weight2),(w.boneIndex3,w.weight3)})if(elastic.bones[pair.Item1].IsChildOf(player.Find("CandyGhost")))influence+=pair.Item2;
                    return influence>.1f;
                }
                var movingIndices=new List<int>();var originalIndices=source.triangles;
                for(int i=0;i<originalIndices.Length;i+=3)if(Deforming(originalIndices[i])||Deforming(originalIndices[i+1])||Deforming(originalIndices[i+2])){movingIndices.Add(originalIndices[i]);movingIndices.Add(originalIndices[i+1]);movingIndices.Add(originalIndices[i+2]);}
                var movingPoints=movingIndices.Distinct().Select(i=>points[i]).ToArray();float movingMin=movingPoints.Min(p=>p.x),movingMax=movingPoints.Max(p=>p.x);
                var movingLeft=movingPoints.Where(p=>p.x<movingMin+epsilon).ToArray();var movingRight=movingPoints.Where(p=>p.x>movingMax-epsilon).ToArray();
                layout.mountLeft=movingLeft.Aggregate(Vector3.zero,(a,b)=>a+b)/movingLeft.Length;layout.mountRight=movingRight.Aggregate(Vector3.zero,(a,b)=>a+b)/movingRight.Length;
                var moving=UnityEngine.Object.Instantiate(source);moving.name="Original elastic deforming section";moving.SetTriangles(movingIndices,0);
                string movingPath=Folder+"/ElasticMoving.asset";var previousMoving=AssetDatabase.LoadAssetAtPath<Mesh>(movingPath);if(previousMoving){EditorUtility.CopySerialized(moving,previousMoving);UnityEngine.Object.DestroyImmediate(moving);}else AssetDatabase.CreateAsset(moving,movingPath);
                var release=player.Find("test");var clip=release.GetComponent<Animation>().clip;clip.SampleAnimation(release.gameObject,clip.length);
                var releaseRenderer=release.GetComponentInChildren<SkinnedMeshRenderer>();var releaseMesh=releaseRenderer.sharedMesh;var releaseVertices=releaseMesh.vertices;var releaseWeights=releaseMesh.boneWeights;var releasePoints=new List<Vector3>();
                for(int i=0;i<releaseVertices.Length;i++)
                {
                    var w=releaseWeights[i];Vector3 p=Vector3.zero;
                    foreach(var pair in new[]{(w.boneIndex0,w.weight0),(w.boneIndex1,w.weight1),(w.boneIndex2,w.weight2),(w.boneIndex3,w.weight3)})if(pair.Item2>0)p+=(releaseRenderer.bones[pair.Item1].localToWorldMatrix*releaseMesh.bindposes[pair.Item1]).MultiplyPoint3x4(releaseVertices[i])*pair.Item2;
                    releasePoints.Add(player.InverseTransformPoint(p));
                }
                float releaseMin=releasePoints.Min(p=>p.x),releaseMax=releasePoints.Max(p=>p.x);
                var releaseLeft=releasePoints.Where(p=>p.x<releaseMin+.1f).ToArray();var releaseRight=releasePoints.Where(p=>p.x>releaseMax-.1f).ToArray();
                layout.releaseLeft=releaseLeft.Aggregate(Vector3.zero,(a,b)=>a+b)/releaseLeft.Length;layout.releaseRight=releaseRight.Aggregate(Vector3.zero,(a,b)=>a+b)/releaseRight.Length;
                File.WriteAllText(Folder+"/layout.json",JsonUtility.ToJson(layout,true));
                Directory.CreateDirectory("Documentation/Slingshot");File.WriteAllText("Documentation/Slingshot/assets.txt","Source bounds: "+bounds+"\nTan wraps: "+left+" / "+right+"\nNormalized mesh: "+baked.bounds+"\nNative endpoints: "+layout.left+" / "+layout.right+"\nNative band width: "+(max-min)+"\nDeforming section mounts: "+layout.mountLeft+" / "+layout.mountRight+"\nVisible triangles: "+movingIndices.Count/3+" of "+originalIndices.Length/3+"\n");
                UnityEngine.Object.DestroyImmediate(obj);UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(geometry);AssetDatabase.Refresh();AssetDatabase.SaveAssets();
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);}
        }
    }
}
