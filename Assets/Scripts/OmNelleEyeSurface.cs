using System;
using System.Linq;
using UnityEngine;

namespace CandyFlick
{
    // Keep the lower gaze and the eyelid caps on the actual animated white-eye surface.
    // The whites blend several facial joints, so following a single eye bone is insufficient.
    [DefaultExecutionOrder(110)]
    public sealed class OmNelleEyeSurface : MonoBehaviour
    {
        public SkinnedMeshRenderer whites, gaze;
        public MeshFilter lids, pupils;
        public int[] whiteTriangles, pupilSourceIndices;
        public Vector3[] authoredPupils;
        public const float GazeLowering=.035f;
        const float Clearance=.003f;
        struct Anchor {public int triangle;public Vector3 barycentric;public float distance,area;}
        struct Triangle {public Vector3 a,b,c,normal;public float area;}
        Transform actor;
        Mesh lidMesh,pupilMesh;
        Vector3[] whiteRest,gazeRest,whitePoints,whiteNormals,lidPoints,lidNormals,pupilPoints,pupilNormals;
        BoneWeight[] whiteWeights,gazeWeights;
        Matrix4x4[] whiteBind,gazeBind,whitePose,gazePose;
        Transform[] whiteBones,gazeBones;
        Triangle[] triangles;
        Anchor[] anchors;
        bool[] usedWhite;
        int[] whiteJoints,gazeJoints;
        float[] orientation;
        Matrix4x4 lidToActor,pupilToActor,actorToLid,actorToPupil;
        public float MinimumPupilClearance {get;private set;}
        public float MinimumLidClearance {get;private set;}

        public void Initialize(Transform owner)
        {
            actor=owner;
            whiteRest=whites.sharedMesh.vertices;whiteWeights=whites.sharedMesh.boneWeights;whiteBind=whites.sharedMesh.bindposes;whiteBones=whites.bones;
            gazeRest=gaze.sharedMesh.vertices;gazeWeights=gaze.sharedMesh.boneWeights;gazeBind=gaze.sharedMesh.bindposes;gazeBones=gaze.bones;
            whitePose=new Matrix4x4[whiteBind.Length];gazePose=new Matrix4x4[gazeBind.Length];
            whitePoints=new Vector3[whiteRest.Length];whiteNormals=new Vector3[whiteRest.Length];usedWhite=new bool[whiteRest.Length];
            foreach(int i in whiteTriangles)usedWhite[i]=true;
            whiteJoints=Joints(whiteWeights,usedWhite,whiteBind.Length);
            var usedGaze=new bool[gazeRest.Length];foreach(int i in pupilSourceIndices)usedGaze[i]=true;
            gazeJoints=Joints(gazeWeights,usedGaze,gazeBind.Length);
            lidMesh=Instantiate(lids.sharedMesh);pupilMesh=Instantiate(pupils.sharedMesh);lidMesh.MarkDynamic();pupilMesh.MarkDynamic();
            lids.sharedMesh=lidMesh;pupils.sharedMesh=pupilMesh;
            lidPoints=lidMesh.vertices;lidNormals=lidMesh.normals;pupilPoints=pupilMesh.vertices;pupilNormals=pupilMesh.normals;
            lidToActor=actor.worldToLocalMatrix*lids.transform.localToWorldMatrix;actorToLid=lidToActor.inverse;
            pupilToActor=actor.worldToLocalMatrix*pupils.transform.localToWorldMatrix;actorToPupil=pupilToActor.inverse;
            triangles=new Triangle[whiteTriangles.Length/3];orientation=new float[triangles.Length];
            Matrix4x4 rest=actor.worldToLocalMatrix*whites.transform.localToWorldMatrix,normalPose=rest.inverse.transpose;
            var normals=whites.sharedMesh.normals;
            for(int i=0;i<whiteRest.Length;i++)whitePoints[i]=rest.MultiplyPoint3x4(whiteRest[i]);
            for(int t=0;t<triangles.Length;t++)
            {
                int a=whiteTriangles[t*3],b=whiteTriangles[t*3+1],c=whiteTriangles[t*3+2];
                orientation[t]=Vector3.Dot(Vector3.Cross(whitePoints[b]-whitePoints[a],whitePoints[c]-whitePoints[a]),normalPose.MultiplyVector(normals[a]+normals[b]+normals[c]))<0?-1:1;
            }
            BuildTriangles();anchors=new Anchor[lidPoints.Length];
            for(int i=0;i<anchors.Length;i++)
            {
                Vector3 point=lidToActor.MultiplyPoint3x4(lidPoints[i]);int t=Nearest(point,out Vector3 hit);var triangle=triangles[t];
                float distance=Vector3.Dot(point-hit,triangle.normal);
                anchors[i]=new Anchor{triangle=t,barycentric=Barycentric(point-triangle.normal*distance,triangle),distance=Mathf.Max(Clearance,distance),area=triangle.area};
            }
            Refresh();
        }
        void LateUpdate(){if(actor&&whites.enabled)Refresh();}
        public void Refresh()
        {
            if(!actor)return;
            Matrix4x4 worldToActor=actor.worldToLocalMatrix;
            foreach(int i in whiteJoints)whitePose[i]=worldToActor*whiteBones[i].localToWorldMatrix*whiteBind[i];
            foreach(int i in gazeJoints)gazePose[i]=worldToActor*gazeBones[i].localToWorldMatrix*gazeBind[i];
            for(int i=0;i<whiteRest.Length;i++)if(usedWhite[i]){whitePoints[i]=Skin(whiteRest[i],whiteWeights[i],whitePose);whiteNormals[i]=Vector3.zero;}
            BuildTriangles();
            Matrix4x4 lidNormal=lidToActor.transpose,pupilNormal=pupilToActor.transpose;
            MinimumLidClearance=float.PositiveInfinity;
            for(int i=0;i<anchors.Length;i++)
            {
                var anchor=anchors[i];var triangle=triangles[anchor.triangle];
                float distance=Mathf.Max(Clearance*.5f,anchor.distance*Mathf.Sqrt(Mathf.Max(.0001f,triangle.area/anchor.area)));
                Vector3 point=triangle.a*anchor.barycentric.x+triangle.b*anchor.barycentric.y+triangle.c*anchor.barycentric.z+triangle.normal*distance;
                // Vertices beyond a triangle's boundary can meet a neighboring curved face.
                if(anchor.barycentric.x<0||anchor.barycentric.y<0||anchor.barycentric.z<0)
                {
                    int nearest=Nearest(point,out Vector3 hit);Vector3 n=triangles[nearest].normal;
                    float gap=Vector3.Dot(point-hit,n);if(gap<Clearance)point+=n*(Clearance-gap);
                }
                lidPoints[i]=actorToLid.MultiplyPoint3x4(point);lidNormals[i]=lidNormal.MultiplyVector(SurfaceNormal(anchor.triangle,anchor.barycentric)).normalized;
                MinimumLidClearance=Mathf.Min(MinimumLidClearance,distance);
            }
            MinimumPupilClearance=float.PositiveInfinity;
            for(int i=0;i<pupilPoints.Length;i++)
            {
                int source=pupilSourceIndices[i];
                // Animate the authored pupil patches before lowering them. Lowering in bind
                // space rotates the offset differently for each eye and changes their alignment.
                Vector3 desired=Skin(authoredPupils[i],gazeWeights[source],gazePose);
                desired.y-=GazeLowering;
                int triangle=0;Vector3 barycentric=default,hit=default;bool found=false;
                // Squashed eye surfaces can overlap in projection. Always use the foremost
                // triangle, even if the pupil still lies inside last frame's triangle.
                float front=float.NegativeInfinity;
                for(int t=0;t<triangles.Length;t++)if(Ray(desired,triangles[t],out var b,out var p)&&p.z>front){front=p.z;triangle=t;barycentric=b;hit=p;found=true;}
                if(!found){triangle=Nearest(desired,out hit,true);barycentric=Barycentric(hit,triangles[triangle]);}
                Vector3 normal=SurfaceNormal(triangle,barycentric);
                if(normal.z<0)normal=-normal;
                // A forward clearance keeps the authored horizontal/vertical alignment exact
                // and cannot cross another white triangle when the surface folds sharply.
                Vector3 point=hit+Vector3.forward*Clearance;
                pupilPoints[i]=actorToPupil.MultiplyPoint3x4(point);pupilNormals[i]=pupilNormal.MultiplyVector(normal).normalized;
                MinimumPupilClearance=Mathf.Min(MinimumPupilClearance,Vector3.Dot(point-hit,normal));
            }
            lidMesh.vertices=lidPoints;lidMesh.normals=lidNormals;lidMesh.RecalculateBounds();
            pupilMesh.vertices=pupilPoints;pupilMesh.normals=pupilNormals;pupilMesh.RecalculateBounds();
        }
        void BuildTriangles()
        {
            for(int t=0;t<triangles.Length;t++)
            {
                int a=whiteTriangles[t*3],b=whiteTriangles[t*3+1],c=whiteTriangles[t*3+2];
                Vector3 cross=Vector3.Cross(whitePoints[b]-whitePoints[a],whitePoints[c]-whitePoints[a])*orientation[t];float area=cross.magnitude;
                triangles[t]=new Triangle{a=whitePoints[a],b=whitePoints[b],c=whitePoints[c],normal=area>.00000001f?cross/area:Vector3.forward,area=Mathf.Max(.00000001f,area)};
                whiteNormals[a]+=cross;whiteNormals[b]+=cross;whiteNormals[c]+=cross;
            }
        }
        Vector3 SurfaceNormal(int triangle,Vector3 barycentric)
        {
            Vector3 normal=(whiteNormals[whiteTriangles[triangle*3]].normalized*Mathf.Max(0,barycentric.x)+whiteNormals[whiteTriangles[triangle*3+1]].normalized*Mathf.Max(0,barycentric.y)+whiteNormals[whiteTriangles[triangle*3+2]].normalized*Mathf.Max(0,barycentric.z)).normalized;
            return normal.sqrMagnitude>.1f?normal:triangles[triangle].normal;
        }
        static bool Ray(Vector3 point,Triangle t,out Vector3 barycentric,out Vector3 hit)
        {
            hit=default;barycentric=default;
            if(point.x<Mathf.Min(t.a.x,Mathf.Min(t.b.x,t.c.x))||point.x>Mathf.Max(t.a.x,Mathf.Max(t.b.x,t.c.x))||point.y<Mathf.Min(t.a.y,Mathf.Min(t.b.y,t.c.y))||point.y>Mathf.Max(t.a.y,Mathf.Max(t.b.y,t.c.y)))return false;
            float d=(t.b.y-t.c.y)*(t.a.x-t.c.x)+(t.c.x-t.b.x)*(t.a.y-t.c.y);if(Mathf.Abs(d)<1e-10f)return false;
            float u=((t.b.y-t.c.y)*(point.x-t.c.x)+(t.c.x-t.b.x)*(point.y-t.c.y))/d;
            float v=((t.c.y-t.a.y)*(point.x-t.c.x)+(t.a.x-t.c.x)*(point.y-t.c.y))/d;
            if(u<-.00001f||v<-.00001f||u+v>1.00001f)return false;
            barycentric=new Vector3(u,v,1-u-v);hit=t.a*u+t.b*v+t.c*(1-u-v);return true;
        }
        int Nearest(Vector3 point,out Vector3 hit,bool frontOnly=false)
        {
            int nearest=0;float distance=float.PositiveInfinity;hit=point;
            for(int t=0;t<triangles.Length;t++){var triangle=triangles[t];if(frontOnly&&triangle.normal.z<=.001f)continue;Vector3 p=ClosestPoint(point,triangle.a,triangle.b,triangle.c);float d=(p-point).sqrMagnitude;if(d<distance){distance=d;nearest=t;hit=p;}}
            return nearest;
        }
        static Vector3 Barycentric(Vector3 point,Triangle t)
        {
            Vector3 ab=t.b-t.a,ac=t.c-t.a,ap=point-t.a;float aa=Vector3.Dot(ab,ab),bb=Vector3.Dot(ab,ac),cc=Vector3.Dot(ac,ac),d=aa*cc-bb*bb;
            if(Mathf.Abs(d)<1e-15f)return new Vector3(1,0,0);
            float v=(cc*Vector3.Dot(ap,ab)-bb*Vector3.Dot(ap,ac))/d,w=(aa*Vector3.Dot(ap,ac)-bb*Vector3.Dot(ap,ab))/d;
            return new Vector3(1-v-w,v,w);
        }
        public static Vector3 ClosestPoint(Vector3 p,Vector3 a,Vector3 b,Vector3 c)
        {
            Vector3 ab=b-a,ac=c-a,ap=p-a;float d1=Vector3.Dot(ab,ap),d2=Vector3.Dot(ac,ap);if(d1<=0&&d2<=0)return a;
            Vector3 bp=p-b;float d3=Vector3.Dot(ab,bp),d4=Vector3.Dot(ac,bp);if(d3>=0&&d4<=d3)return b;
            float vc=d1*d4-d3*d2;if(vc<=0&&d1>=0&&d3<=0)return a+ab*(d1/(d1-d3));
            Vector3 cp=p-c;float d5=Vector3.Dot(ab,cp),d6=Vector3.Dot(ac,cp);if(d6>=0&&d5<=d6)return c;
            float vb=d5*d2-d1*d6;if(vb<=0&&d2>=0&&d6<=0)return a+ac*(d2/(d2-d6));
            float va=d3*d6-d5*d4;if(va<=0&&d4-d3>=0&&d5-d6>=0)return b+(c-b)*((d4-d3)/(d4-d3+d5-d6));
            float inverse=1/(va+vb+vc);return a+ab*(vb*inverse)+ac*(vc*inverse);
        }
        static int[] Joints(BoneWeight[] weights,bool[] selected,int count)
        {
            var used=new bool[count];
            for(int i=0;i<weights.Length;i++)if(selected[i])
            {
                var w=weights[i];if(w.weight0>0)used[w.boneIndex0]=true;if(w.weight1>0)used[w.boneIndex1]=true;
                if(w.weight2>0)used[w.boneIndex2]=true;if(w.weight3>0)used[w.boneIndex3]=true;
            }
            return Enumerable.Range(0,count).Where(i=>used[i]).ToArray();
        }
        static Vector3 Skin(Vector3 point,BoneWeight w,Matrix4x4[] pose)=>pose[w.boneIndex0].MultiplyPoint3x4(point)*w.weight0+pose[w.boneIndex1].MultiplyPoint3x4(point)*w.weight1+pose[w.boneIndex2].MultiplyPoint3x4(point)*w.weight2+pose[w.boneIndex3].MultiplyPoint3x4(point)*w.weight3;
        void OnDestroy(){if(lidMesh)Destroy(lidMesh);if(pupilMesh)Destroy(pupilMesh);}
    }
}
