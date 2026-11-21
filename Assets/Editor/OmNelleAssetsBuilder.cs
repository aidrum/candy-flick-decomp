using System;
using System.IO;
using System.Linq;
using CandyFlick;
using UnityEditor;
using UnityEngine;

namespace CandyFlickEditor
{
    public static class OmNelleAssetsBuilder
    {
        const string Folder="Assets/OmNelle/", Source="Assets/Editor/OmNelleSource/";
        static T Save<T>(T value,string path) where T:UnityEngine.Object
        {
            var existing=AssetDatabase.LoadAssetAtPath<T>(path);
            if(existing)
            {
                if(value is Mesh source&&existing is Mesh target)
                {
                    // CopySerialized can leave Unity's native vertex buffer at the old size
                    // when rebuilding a mesh with more vertices. Rebuild that buffer explicitly.
                    target.Clear(false);target.name=source.name;target.indexFormat=source.indexFormat;
                    target.vertices=source.vertices;target.normals=source.normals;target.uv=source.uv;
                    target.tangents=source.tangents;target.colors32=source.colors32;
                    target.bindposes=source.bindposes;target.boneWeights=source.boneWeights;
                    target.subMeshCount=source.subMeshCount;
                    for(int s=0;s<source.subMeshCount;s++)target.SetIndices(source.GetIndices(s),source.GetTopology(s),s,false);
                    target.bounds=source.bounds;
                }
                else EditorUtility.CopySerialized(value,existing);
                EditorUtility.SetDirty(existing);UnityEngine.Object.DestroyImmediate(value);return existing;
            }
            AssetDatabase.CreateAsset(value,path);return value;
        }
        [MenuItem("Candy Flick/Import Om Nelle")]
        public static void Build()
        {
            foreach(string sub in new[]{"Meshes","Materials","Textures","Audio"})Directory.CreateDirectory(Folder+sub);
            AssetDatabase.Refresh();
            var modelImporter=(ModelImporter)AssetImporter.GetAtPath("Assets/Editor/OmNelleSource/OmNelle.fbx");
            modelImporter.isReadable=true;modelImporter.importAnimation=false;modelImporter.SaveAndReimport();
            string png=Folder+"Textures/omnelle.png";
            File.Copy(Source+"omnelle.png",png,true);AssetDatabase.ImportAsset(png);
            var textureImporter=(TextureImporter)AssetImporter.GetAtPath(png);textureImporter.mipmapEnabled=false;
            textureImporter.textureCompression=TextureImporterCompression.Uncompressed;textureImporter.wrapMode=TextureWrapMode.Clamp;textureImporter.SaveAndReimport();
            var geometry=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Original26/Geometry"));
            var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Editor/OmNelleSource/OmNelle.fbx"));
            try
            {
                var actor=geometry.transform.Find("OmNom@All-1");var original=actor.GetComponentsInChildren<SkinnedMeshRenderer>().Single(r=>r.name=="OmNom_Mesh");
                root.name="TimeTravelSkin";root.transform.SetParent(actor,false);
                var transforms=root.GetComponentsInChildren<Transform>(true);
                var sourceRoot=transforms.Single(t=>t.name=="OmNomoot");var rig=sourceRoot.parent;
                var drivers=actor.GetComponentsInChildren<Transform>(true).Where(t=>!t.IsChildOf(root.transform)).ToArray();
                // Match the authored rig's units to Candy Flick; the replacement FBX has
                // a different object scale from the old model. Never scale the actor/collider.
                Vector3 sourceSpan=root.transform.InverseTransformPoint(transforms.Single(t=>t.name=="Jt_PonyTail").position)-root.transform.InverseTransformPoint(sourceRoot.position);
                Vector3 originalSpan=actor.InverseTransformPoint(drivers.Single(t=>t.name=="Jt_PonyTail").position)-actor.InverseTransformPoint(drivers.Single(t=>t.name=="OmNom_Root").position);
                root.transform.localScale=Vector3.one*(originalSpan.magnitude/sourceSpan.magnitude);
                var visual=root.AddComponent<TimeTravelVisual>();
                var bindings=new System.Collections.Generic.List<TimeTravelVisual.Attachment>();
                foreach(var bone in rig.GetComponentsInChildren<Transform>(true))
                {
                    // The armature already follows its actor through parenting. Do not bind it
                    // by the actor's scene name: Photo mode renames each independently placed actor.
                    if(bone==rig)continue;
                    if(bone.name.EndsWith("_end"))continue;
                    var candidates=drivers.Where(t=>Vector3.Distance(actor.InverseTransformPoint(t.position),actor.InverseTransformPoint(bone.position))<.00002f).ToArray();
                    string name=OriginalName(bone.name);
                    var driver=candidates.FirstOrDefault(t=>t.name==name)??candidates.FirstOrDefault(t=>t.name.StartsWith(bone.name.Contains("_Jt_")?"Jt_":"Bn_"))??candidates.FirstOrDefault();
                    if(driver)
                    {
                        Matrix4x4 pose=bone.localToWorldMatrix;
                        bindings.Add(new TimeTravelVisual.Attachment{bone=bone,originalBone=driver.name,correction=driver.worldToLocalMatrix*pose});
                    }
                }
                if(bindings.Count<65)throw new Exception("Incomplete Om Nelle retarget: "+bindings.Count);
                visual.attachments=bindings.ToArray();
                visual.directSkinning=true;
                var material=new Material(original.sharedMaterial){name="OmNelle",mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"Textures/omnelle.png")};
                material=Save(material,Folder+"Materials/OmNelle.mat");
                var face=root.GetComponentsInChildren<SkinnedMeshRenderer>().Single(r=>r.name=="eyelid_mesh_0.001");
                var gaze=root.GetComponentsInChildren<SkinnedMeshRenderer>().Single(r=>r.name=="omnom_mesh_0");
                int[] lidVertices=null;
                foreach(var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var adapted=UnityEngine.Object.Instantiate(renderer.sharedMesh);
                    if(renderer.name=="omnom_mesh_0")LowerPupils(adapted,renderer,face,actor);
                    if(renderer==face)lidVertices=EyelidClearance(adapted,renderer,actor);
                    // Bind the GPU skin directly to the native animation joints. Decomposing
                    // their affine motion into imported TRS controls loses eye/face shear.
                    var palette=renderer.bones;var sourceBind=adapted.bindposes;
                    var used=adapted.boneWeights.SelectMany(w=>new[]{(w.boneIndex0,w.weight0),(w.boneIndex1,w.weight1),(w.boneIndex2,w.weight2),(w.boneIndex3,w.weight3)}).Where(p=>p.Item2>.00001f).Select(p=>p.Item1).ToHashSet();
                    adapted.bindposes=palette.Select((bone,i)=>
                    {
                        int binding=bindings.FindIndex(a=>a.bone==bone);
                        if(binding>=0)return bindings[binding].correction*sourceBind[i];
                        if(used.Contains(i))throw new Exception("Unmapped replacement skin joint: "+bone.name);
                        return actor.worldToLocalMatrix*bone.localToWorldMatrix*sourceBind[i];
                    }).ToArray();
                    renderer.sharedMesh=Save(adapted,Folder+"Meshes/"+renderer.name+".asset");
                    renderer.sharedMaterial=material;renderer.quality=SkinQuality.Bone4;renderer.updateWhenOffscreen=true;
                    renderer.localBounds=new Bounds(Vector3.zero,Vector3.one*2);
                }
                ConfigureEyeSurface(root,face,gaze,lidVertices,material);
                // Keep the authored face and lashes; surface attachments keep the two caps
                // and lowered pupils synchronized with the blended eye whites.
                foreach(var bone in rig.GetComponentsInChildren<Transform>(true))bone.name="Nelle_"+bone.name;
                PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/TimeTravel/OmNelle.prefab");
            }
            finally {UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(geometry);}
            BuildVoice();AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        }
        static int[] EyelidClearance(Mesh mesh,SkinnedMeshRenderer renderer,Transform actor)
        {
            // The caps are separate surface islands in the combined mesh. A tiny shell
            // clearance prevents coincident white/green surfaces without changing their weights.
            var points=mesh.vertices;var normals=mesh.normals;var parents=Enumerable.Range(0,points.Length).ToArray();
            int Root(int i){while(parents[i]!=i){parents[i]=parents[parents[i]];i=parents[i];}return i;}
            var indices=mesh.triangles;
            for(int i=0;i<indices.Length;i+=3){parents[Root(indices[i+1])]=Root(indices[i]);parents[Root(indices[i+2])]=Root(indices[i]);}
            Matrix4x4 toActor=actor.worldToLocalMatrix*renderer.transform.localToWorldMatrix,normalPose=toActor.inverse.transpose;
            int caps=0;var selected=new System.Collections.Generic.List<int>();
            foreach(var component in Enumerable.Range(0,points.Length).GroupBy(Root))
            {
                var vertices=component.ToArray();if(vertices.Length<80||vertices.Length>180)continue;
                var bounds=new Bounds(toActor.MultiplyPoint3x4(points[vertices[0]]),Vector3.zero);
                foreach(int i in vertices)bounds.Encapsulate(toActor.MultiplyPoint3x4(points[i]));
                if(bounds.min.y<.29f||bounds.max.y>.44f||bounds.min.z<=0)continue;
                foreach(int i in vertices)points[i]=toActor.inverse.MultiplyPoint3x4(toActor.MultiplyPoint3x4(points[i])+normalPose.MultiplyVector(normals[i]).normalized*.0015f);
                selected.AddRange(vertices);
                caps++;
            }
            if(caps!=2)throw new Exception("Expected two authored eyelid caps, found "+caps);
            mesh.vertices=points;mesh.RecalculateBounds();return selected.ToArray();
        }
        static void ConfigureEyeSurface(GameObject root,SkinnedMeshRenderer face,SkinnedMeshRenderer gaze,int[] lids,Material material)
        {
            var surface=root.AddComponent<OmNelleEyeSurface>();surface.whites=face;surface.gaze=gaze;
            var gazeWeights=gaze.sharedMesh.boneWeights;
            int[] pupilVertices=Enumerable.Range(0,gazeWeights.Length).Where(i=>gaze.bones[gazeWeights[i].boneIndex0].name.EndsWith("_Bn_Eye")).ToArray();
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(Source+"OmNelle.fbx");
            var authored=source.GetComponentsInChildren<SkinnedMeshRenderer>().Single(r=>r.name==gaze.name).sharedMesh.vertices;
            surface.authoredPupils=pupilVertices.Select(i=>authored[i]).ToArray();
            surface.lids=EyePiece("OmNelle eyelids",root.transform,face,lids,material,true);
            surface.pupils=EyePiece("OmNelle pupils",root.transform,gaze,pupilVertices,material,false);
            surface.pupilSourceIndices=pupilVertices;
            var mesh=face.sharedMesh;var points=mesh.vertices;var uv=mesh.uv;var weights=mesh.boneWeights;var indices=mesh.triangles;
            bool HasEye(int vertex)
            {
                var w=weights[vertex];
                return w.weight0>.001f&&face.bones[w.boneIndex0].name.Contains("FullEye")||w.weight1>.001f&&face.bones[w.boneIndex1].name.Contains("FullEye")||w.weight2>.001f&&face.bones[w.boneIndex2].name.Contains("FullEye")||w.weight3>.001f&&face.bones[w.boneIndex3].name.Contains("FullEye");
            }
            var atlas=new Texture2D(2,2);atlas.LoadImage(File.ReadAllBytes(Folder+"Textures/omnelle.png"));
            var white=new System.Collections.Generic.List<int>();
            try
            {
                for(int t=0;t<indices.Length;t+=3)
                {
                    int a=indices[t],b=indices[t+1],c=indices[t+2];if(!HasEye(a)&&!HasEye(b)&&!HasEye(c))continue;
                    Vector2 sample=(uv[a]+uv[b]+uv[c])/3;Color color=atlas.GetPixelBilinear(sample.x,sample.y);
                    float max=Mathf.Max(color.r,Mathf.Max(color.g,color.b)),min=Mathf.Min(color.r,Mathf.Min(color.g,color.b));
                    if(max<.15f||min<max*.75f||Vector3.Cross(points[b]-points[a],points[c]-points[a]).sqrMagnitude<1e-14f)continue;
                    white.AddRange(new[]{a,b,c});
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(atlas);}
            if(white.Count<300)throw new Exception("Missing animated white-eye surface");
            surface.whiteTriangles=white.ToArray();
        }
        static MeshFilter EyePiece(string name,Transform parent,SkinnedMeshRenderer source,int[] selected,Material material,bool refine)
        {
            var original=source.sharedMesh;var sourcePoints=original.vertices;var sourceUV=original.uv;var sourceNormals=original.normals;
            var map=selected.Select((vertex,index)=>(vertex,index)).ToDictionary(p=>p.vertex,p=>p.index);
            Matrix4x4 pose=parent.worldToLocalMatrix*source.transform.localToWorldMatrix,normalPose=pose.inverse.transpose;
            var points=selected.Select(i=>pose.MultiplyPoint3x4(sourcePoints[i])).ToList();var uv=selected.Select(i=>sourceUV[i]).ToList();
            var normals=selected.Select(i=>normalPose.MultiplyVector(sourceNormals[i]).normalized).ToList();
            var triangles=new System.Collections.Generic.List<int>();var retained=new System.Collections.Generic.List<int>();var indices=original.triangles;
            for(int t=0;t<indices.Length;t+=3)
            {
                int a=indices[t],b=indices[t+1],c=indices[t+2];
                if(map.ContainsKey(a)&&map.ContainsKey(b)&&map.ContainsKey(c))triangles.AddRange(new[]{map[a],map[b],map[c]});else retained.AddRange(new[]{a,b,c});
            }
            if(refine)
            {
                var edges=new System.Collections.Generic.Dictionary<(int,int),int>();
                int Mid(int a,int b)
                {
                    var key=a<b?(a,b):(b,a);if(edges.TryGetValue(key,out int index))return index;
                    index=points.Count;edges.Add(key,index);points.Add((points[a]+points[b])*.5f);uv.Add((uv[a]+uv[b])*.5f);normals.Add((normals[a]+normals[b]).normalized);return index;
                }
                var refined=new System.Collections.Generic.List<int>();
                for(int t=0;t<triangles.Count;t+=3){int a=triangles[t],b=triangles[t+1],c=triangles[t+2],ab=Mid(a,b),bc=Mid(b,c),ca=Mid(c,a);refined.AddRange(new[]{a,ab,ca,ab,b,bc,ca,bc,c,ab,bc,ca});}
                triangles=refined;
            }
            var mesh=new Mesh{name=name,vertices=points.ToArray(),normals=normals.ToArray(),uv=uv.ToArray(),triangles=triangles.ToArray()};mesh.RecalculateBounds();
            mesh=Save(mesh,Folder+"Meshes/"+(refine?"EyeCaps":"PupilSurface")+".asset");
            var trimmed=UnityEngine.Object.Instantiate(original);trimmed.triangles=retained.ToArray();
            source.sharedMesh=Save(trimmed,AssetDatabase.GetAssetPath(original));
            var filter=new GameObject(name).AddComponent<MeshFilter>();filter.transform.SetParent(parent,false);filter.sharedMesh=mesh;
            var renderer=filter.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=source.shadowCastingMode;renderer.receiveShadows=source.receiveShadows;return filter;
        }
        static void LowerPupils(Mesh mesh,SkinnedMeshRenderer pupils,SkinnedMeshRenderer whites,Transform actor)
        {
            // Move the pupil patches down the actual eye surface, retaining native animated gaze.
            // A simple joint translation buries them inside the whites; spherical rotation also
            // fails because these eyes are not spherical. Project onto the authored front surface.
            Vector3[] surface=whites.sharedMesh.vertices.Select(p=>actor.InverseTransformPoint(whites.transform.TransformPoint(p))).ToArray();
            int[] triangles=whites.sharedMesh.triangles;var points=mesh.vertices;var weights=mesh.boneWeights;
            var changed=new System.Collections.Generic.List<int>();
            for(int i=0;i<points.Length;i++)
            {
                if(!pupils.bones[weights[i].boneIndex0].name.EndsWith("_Bn_Eye"))continue;
                Vector3 point=actor.InverseTransformPoint(pupils.transform.TransformPoint(points[i]));point.y-=OmNelleEyeSurface.GazeLowering;
                float front=float.NegativeInfinity;
                for(int t=0;t<triangles.Length;t+=3)
                {
                    Vector3 a=surface[triangles[t]],b=surface[triangles[t+1]],c=surface[triangles[t+2]];
                    float d=(b.y-c.y)*(a.x-c.x)+(c.x-b.x)*(a.y-c.y);if(Mathf.Abs(d)<1e-10f)continue;
                    float u=((b.y-c.y)*(point.x-c.x)+(c.x-b.x)*(point.y-c.y))/d;
                    float v=((c.y-a.y)*(point.x-c.x)+(a.x-c.x)*(point.y-c.y))/d;
                    if(u>=-.0001f&&v>=-.0001f&&u+v<=1.0001f)front=Mathf.Max(front,u*a.z+v*b.z+(1-u-v)*c.z);
                }
                if(!float.IsFinite(front))throw new Exception("Om Nelle pupil leaves the eye surface");
                point.z=front+.001f;
                points[i]=pupils.transform.InverseTransformPoint(actor.TransformPoint(point));changed.Add(i);
            }
            if(changed.Count!=242)throw new Exception("Unexpected Om Nelle pupil geometry: "+changed.Count);
            var normals=mesh.normals;mesh.vertices=points;mesh.RecalculateNormals();var adjusted=mesh.normals;
            foreach(int i in changed)normals[i]=adjusted[i];mesh.normals=normals;mesh.RecalculateBounds();
        }
        static string OriginalName(string name)
        {
            if(name=="OmNomoot")return "OmNom_Root";
            if(name=="BnowerBody")return "Bn_LowerBody";
            foreach(string side in new[]{"L","R"})
            {
                name=name.Replace("Bip"+side+"_Jtip_End","Jt_Lip_"+side+"_End").Replace("Bip"+side+"_Bnip_End","Bn_Lip_"+side+"_End");
                name=name.Replace("Bip"+side+"_Jt_","Jt_"+side+"_").Replace("Bip"+side+"_Bn_","Bn_"+side+"_");
                name=name.Replace(side+"_Freg",side+"Fr_Leg").Replace(side+"_Bckeg",side+"Bck_Leg");
                foreach(string lip in new[]{"UpperLip","BottomLip"})name=name.Replace(side+"_"+lip+"_",lip+"_"+side+"_");
                name=name.Replace(side+"_LipEnd","Lip_"+side+"_End");
            }
            return name;
        }
        static byte[] DelaySad(byte[] wav)
        {
            const int silenceFrames=28985; // round(26630 / 44100 * 48000), the native sadness delay.
            using(var reader=new BinaryReader(new MemoryStream(wav)))
            {
                reader.BaseStream.Position=12;
                while(reader.BaseStream.Position+8<=wav.Length)
                {
                    string chunk=new string(reader.ReadChars(4));int length=reader.ReadInt32();int start=(int)reader.BaseStream.Position;
                    if(chunk=="data")
                    {
                        int padding=silenceFrames*2;var output=new byte[wav.Length+padding];
                        Array.Copy(wav,0,output,0,start);Array.Copy(wav,start,output,start+padding,wav.Length-start);
                        Array.Copy(BitConverter.GetBytes(output.Length-8),0,output,4,4);
                        Array.Copy(BitConverter.GetBytes(length+padding),0,output,start-4,4);return output;
                    }
                    reader.BaseStream.Position=start+length+(length&1);
                }
            }
            throw new InvalidDataException("Om Nelle sadness WAV has no PCM data");
        }
        public static void BuildVoice()
        {
            AudioClip Load(string source,string cue)
            {
                string path=Folder+"Audio/OmNelle_"+cue+".wav";
                byte[] wav=File.ReadAllBytes(Source+source+".wav");
                // close_sad starts with the mouth closing. Match the original sadness cue's delay.
                if(cue=="Sad")wav=DelaySad(wav);
                File.WriteAllBytes(path,wav);AssetDatabase.ImportAsset(path);
                var importer=(AudioImporter)AssetImporter.GetAtPath(path);var settings=importer.defaultSampleSettings;
                settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.PCM;
                settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;settings.preloadAudioData=true;
                importer.defaultSampleSettings=settings;importer.forceToMono=false;importer.loadInBackground=false;importer.SaveAndReimport();
                return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }
            var voice=ScriptableObject.CreateInstance<TimeTravelVoice>();voice.name="OmNelle";
            voice.openMouth=Load("open_mouth","Open");voice.closeMouth=Load("close_mouth","Close");voice.chew=Resources.Load<GameAssets>("GameAssets").chew;
            voice.sad=Load("sad","Sad");voice.tap=Load("petting_tap","Tap");voice.loveTouch=Load("petting_hold","Pet");
            Save(voice,"Assets/Resources/TimeTravelAudio/OmNelle.asset");
        }
    }
}
