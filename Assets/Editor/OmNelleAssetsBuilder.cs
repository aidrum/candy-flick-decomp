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
            foreach(string file in new[]{"omnelle.png","omnelle_lashes.png","eyelashes.png"})
            {
                string path=Folder+"Textures/"+file;
                File.Copy(Source+file,path,true);AssetDatabase.ImportAsset(path);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.mipmapEnabled=false;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
            }
            var geometry=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Original26/Geometry"));
            var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Editor/OmNelleSource/OmNelle.fbx"));
            try
            {
                var actor=geometry.transform.Find("OmNom@All-1");var original=actor.GetComponentsInChildren<SkinnedMeshRenderer>().Single(r=>r.name=="OmNom_Mesh");
                root.name="TimeTravelSkin";root.transform.SetParent(actor,false);root.transform.localScale=Vector3.one*.01f;
                // The FBX includes an unused duplicate control hierarchy. Only the skinned armature is needed.
                UnityEngine.Object.DestroyImmediate(root.transform.Find("Bones").gameObject);
                var rig=root.transform.Find("Bones.001");var visual=root.AddComponent<TimeTravelVisual>();
                var drivers=actor.GetComponentsInChildren<Transform>(true).Where(t=>!t.IsChildOf(root.transform)).ToArray();
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
                var material=new Material(original.sharedMaterial){name="OmNelle",mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"Textures/omnelle.png")};
                material=Save(material,Folder+"Materials/OmNelle.mat");
                var eyelidMaterial=new Material(material){name="OmNelle_Eyelids",mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"Textures/omnelle_lashes.png")};
                eyelidMaterial=Save(eyelidMaterial,Folder+"Materials/Eyelids.mat");
                var whites=root.GetComponentsInChildren<SkinnedMeshRenderer>().Single(r=>r.name=="omnom_mesh_2");
                var eyes=new[]{rig.GetComponentsInChildren<Transform>().Single(t=>t.name=="BipL_Bn_FullEye"),rig.GetComponentsInChildren<Transform>().Single(t=>t.name=="BipR_Bn_FullEye")};
                foreach(var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var adapted=UnityEngine.Object.Instantiate(renderer.sharedMesh);
                    if(renderer.name=="omnom_mesh_0")LowerPupils(adapted,renderer,whites,actor);
                    if(renderer.name=="eyelid_mesh_0")BindEyelids(adapted,renderer,actor,whites);
                    renderer.sharedMesh=Save(adapted,Folder+"Meshes/"+renderer.name+".asset");
                    renderer.sharedMaterial=renderer.name=="eyelid_mesh_0"?eyelidMaterial:material;renderer.quality=SkinQuality.Bone4;renderer.updateWhenOffscreen=true;
                    renderer.localBounds=new Bounds(Vector3.zero,Vector3.one*2);
                }
                var lashes=root.GetComponentInChildren<MeshFilter>();var sourceMesh=lashes.sharedMesh;
                var lashMaterial=new Material(original.sharedMaterial){name="OmNelle_Eyelashes",mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"Textures/eyelashes.png")};
                lashMaterial.shader=Shader.Find("Toon/Basic");lashMaterial=Save(lashMaterial,Folder+"Materials/Eyelashes.mat");
                // Split the unbound left/right triangles and skin each one to its corresponding full eye.
                var mesh=UnityEngine.Object.Instantiate(sourceMesh);var points=mesh.vertices;
                mesh.boneWeights=points.Select(p=>new BoneWeight{boneIndex0=actor.InverseTransformPoint(lashes.transform.TransformPoint(p)).x<0?0:1,weight0=1}).ToArray();
                mesh.bindposes=eyes.Select(t=>t.worldToLocalMatrix*lashes.transform.localToWorldMatrix).ToArray();
                mesh=Save(mesh,Folder+"Meshes/Eyelashes.asset");var lashObject=lashes.gameObject;
                UnityEngine.Object.DestroyImmediate(lashObject.GetComponent<MeshRenderer>());UnityEngine.Object.DestroyImmediate(lashes);
                var lashRenderer=lashObject.AddComponent<SkinnedMeshRenderer>();lashRenderer.sharedMesh=mesh;lashRenderer.bones=eyes;lashRenderer.rootBone=rig;
                lashRenderer.sharedMaterial=lashMaterial;lashRenderer.quality=SkinQuality.Bone4;lashRenderer.updateWhenOffscreen=true;lashRenderer.localBounds=new Bounds(Vector3.zero,Vector3.one*2);
                foreach(var bone in rig.GetComponentsInChildren<Transform>(true))bone.name="Nelle_"+bone.name;
                PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/TimeTravel/OmNelle.prefab");
            }
            finally {UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(geometry);}
            BuildVoice();AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        }
        static void BindEyelids(Mesh mesh,SkinnedMeshRenderer renderer,Transform actor,SkinnedMeshRenderer whites)
        {
            // The whites blend eye and mouth joints. Transfer that complete skinning field;
            // attaching the caps only to the two eye controls misses the facial deformation.
            var points=mesh.vertices.ToList();var uv=mesh.uv.ToList();
            var edges=new System.Collections.Generic.Dictionary<(int,int),int>();
            int Midpoint(int a,int b)
            {
                var key=a<b?(a,b):(b,a);if(edges.TryGetValue(key,out int existing))return existing;
                int index=points.Count;edges.Add(key,index);points.Add((points[a]+points[b])*.5f);uv.Add((uv[a]+uv[b])*.5f);return index;
            }
            var triangles=new System.Collections.Generic.List<int>();var source=mesh.triangles;
            for(int i=0;i<source.Length;i+=3)
            {
                int a=source[i],b=source[i+1],c=source[i+2],ab=Midpoint(a,b),bc=Midpoint(b,c),ca=Midpoint(c,a);
                triangles.AddRange(new[]{a,ab,ca,ab,b,bc,ca,bc,c,ab,bc,ca});
            }
            var surface=EyeSurface(whites,actor);var weights=new BoneWeight[points.Count];
            for(int i=0;i<points.Count;i++)
            {
                Vector3 point=actor.InverseTransformPoint(renderer.transform.TransformPoint(points[i]));
                var nearest=NearestEyeSurface(point,-1,surface,out Vector3 normal);
                float clearance=Vector3.Dot(point-nearest,normal);
                if(clearance<.0025f)point+=normal*(.0025f-clearance);
                NearestEyeSurface(point,-1,surface,out normal,out EyeTriangle triangle,out Vector3 barycentric);
                weights[i]=SurfaceWeights(triangle,barycentric);
                points[i]=renderer.transform.InverseTransformPoint(actor.TransformPoint(point));
            }
            mesh.vertices=points.ToArray();mesh.uv=uv.ToArray();mesh.boneWeights=weights;mesh.triangles=triangles.ToArray();
            renderer.bones=whites.bones;
            mesh.bindposes=renderer.bones.Select(t=>t.worldToLocalMatrix*renderer.transform.localToWorldMatrix).ToArray();
            mesh.RecalculateNormals();mesh.RecalculateBounds();
        }
        internal struct EyeTriangle {public Vector3 a,b,c,normal;public BoneWeight wa,wb,wc;public int side;}
        internal static EyeTriangle[] EyeSurface(SkinnedMeshRenderer whites,Transform actor)
        {
            var result=new System.Collections.Generic.List<EyeTriangle>();var points=whites.sharedMesh.vertices;var normals=whites.sharedMesh.normals;
            var weights=whites.sharedMesh.boneWeights;var indices=whites.sharedMesh.triangles;var uv=whites.sharedMesh.uv;
            var eyeIndices=whites.bones.Select((bone,index)=>(bone,index)).Where(p=>p.bone.name.EndsWith("Bn_FullEye")).Select(p=>p.index).ToArray();
            bool EyeVertex(int i)
            {
                var w=weights[i];return w.weight0>.001f&&eyeIndices.Contains(w.boneIndex0)||w.weight1>.001f&&eyeIndices.Contains(w.boneIndex1)||w.weight2>.001f&&eyeIndices.Contains(w.boneIndex2)||w.weight3>.001f&&eyeIndices.Contains(w.boneIndex3);
            }
            Matrix4x4 pose=actor.worldToLocalMatrix*whites.transform.localToWorldMatrix,normalPose=pose.inverse.transpose;
            var atlas=new Texture2D(2,2);atlas.LoadImage(File.ReadAllBytes(Folder+"Textures/omnelle.png"));
            try
            {
                for(int t=0;t<indices.Length;t+=3)
                {
                    int a=indices[t],b=indices[t+1],c=indices[t+2];Vector2 sample=(uv[a]+uv[b]+uv[c])/3;
                    if(!EyeVertex(a)&&!EyeVertex(b)&&!EyeVertex(c))continue;
                    Color color=atlas.GetPixelBilinear(sample.x,sample.y);
                    // This source mesh also contains the green body. Select its white eye surfaces.
                    float maximum=Mathf.Max(color.r,Mathf.Max(color.g,color.b)),minimum=Mathf.Min(color.r,Mathf.Min(color.g,color.b));
                    if(maximum<.15f||minimum<maximum*.75f)continue;
                    Vector3 pa=pose.MultiplyPoint3x4(points[a]),pb=pose.MultiplyPoint3x4(points[b]),pc=pose.MultiplyPoint3x4(points[c]);
                    Vector3 normal=Vector3.Cross(pb-pa,pc-pa).normalized;
                    if(normal.sqrMagnitude<.5f)continue;
                    if(Vector3.Dot(normal,normalPose.MultiplyVector(normals[a]+normals[b]+normals[c]))<0)normal=-normal;
                    result.Add(new EyeTriangle{a=pa,b=pb,c=pc,normal=normal,wa=weights[a],wb=weights[b],wc=weights[c],side=pa.x+pb.x+pc.x<0?0:1});
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(atlas);}
            if(result.Count<100)throw new Exception("Missing eye-white surface for eyelid fitting: "+result.Count);
            return result.ToArray();
        }
        internal static Vector3 NearestEyeSurface(Vector3 point,int side,EyeTriangle[] surface,out Vector3 normal)
            =>NearestEyeSurface(point,side,surface,out normal,out _,out _);
        internal static Vector3 NearestEyeSurface(Vector3 point,int side,EyeTriangle[] surface,out Vector3 normal,out EyeTriangle face,out Vector3 barycentric)
        {
            float distance=float.PositiveInfinity;Vector3 nearest=point;normal=Vector3.zero;face=default;barycentric=default;
            foreach(var triangle in surface)
            {
                if(side>=0&&triangle.side!=side)continue;
                Vector3 hit=ClosestPoint(point,triangle.a,triangle.b,triangle.c);float squared=(point-hit).sqrMagnitude;
                if(squared>=distance)continue;distance=squared;nearest=hit;normal=triangle.normal;face=triangle;
            }
            Vector3 ab=face.b-face.a,ac=face.c-face.a,ap=nearest-face.a;
            float d00=Vector3.Dot(ab,ab),d01=Vector3.Dot(ab,ac),d11=Vector3.Dot(ac,ac),d20=Vector3.Dot(ap,ab),d21=Vector3.Dot(ap,ac);
            float denominator=d00*d11-d01*d01;
            float v=Mathf.Clamp01((d11*d20-d01*d21)/denominator),w=Mathf.Clamp01((d00*d21-d01*d20)/denominator);
            barycentric=new Vector3(Mathf.Max(0,1-v-w),v,w);barycentric/=barycentric.x+barycentric.y+barycentric.z;
            return nearest;
        }
        static BoneWeight SurfaceWeights(EyeTriangle triangle,Vector3 barycentric)
        {
            var influences=new System.Collections.Generic.Dictionary<int,float>();
            void Add(int bone,float weight){if(weight>0)influences[bone]=(influences.TryGetValue(bone,out float prior)?prior:0)+weight;}
            void Mix(BoneWeight w,float amount){Add(w.boneIndex0,w.weight0*amount);Add(w.boneIndex1,w.weight1*amount);Add(w.boneIndex2,w.weight2*amount);Add(w.boneIndex3,w.weight3*amount);}
            Mix(triangle.wa,barycentric.x);Mix(triangle.wb,barycentric.y);Mix(triangle.wc,barycentric.z);
            var top=influences.OrderByDescending(p=>p.Value).Take(4).ToArray();float total=top.Sum(p=>p.Value);var result=new BoneWeight();
            for(int i=0;i<top.Length;i++)
            {
                switch(i){case 0:result.boneIndex0=top[i].Key;result.weight0=top[i].Value/total;break;
                    case 1:result.boneIndex1=top[i].Key;result.weight1=top[i].Value/total;break;
                    case 2:result.boneIndex2=top[i].Key;result.weight2=top[i].Value/total;break;
                    case 3:result.boneIndex3=top[i].Key;result.weight3=top[i].Value/total;break;}
            }
            return result;
        }
        static Vector3 ClosestPoint(Vector3 p,Vector3 a,Vector3 b,Vector3 c)
        {
            Vector3 ab=b-a,ac=c-a,ap=p-a;float d1=Vector3.Dot(ab,ap),d2=Vector3.Dot(ac,ap);
            if(d1<=0&&d2<=0)return a;
            Vector3 bp=p-b;float d3=Vector3.Dot(ab,bp),d4=Vector3.Dot(ac,bp);if(d3>=0&&d4<=d3)return b;
            float vc=d1*d4-d3*d2;if(vc<=0&&d1>=0&&d3<=0)return a+ab*(d1/(d1-d3));
            Vector3 cp=p-c;float d5=Vector3.Dot(ab,cp),d6=Vector3.Dot(ac,cp);if(d6>=0&&d5<=d6)return c;
            float vb=d5*d2-d1*d6;if(vb<=0&&d2>=0&&d6<=0)return a+ac*(d2/(d2-d6));
            float va=d3*d6-d5*d4;if(va<=0&&d4-d3>=0&&d5-d6>=0)return b+(c-b)*((d4-d3)/(d4-d3+d5-d6));
            float inverse=1/(va+vb+vc);return a+ab*(vb*inverse)+ac*(vc*inverse);
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
                Vector3 point=actor.InverseTransformPoint(pupils.transform.TransformPoint(points[i]));point.y-=.035f;
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
