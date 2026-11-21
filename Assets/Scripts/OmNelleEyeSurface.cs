using System;
using System.Collections.Generic;
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
        struct Triangle {public Vector3 a,b,c,normal,min,max;public float area,uX,uY,vX,vY,denominator;}
        struct Node {public Vector3 min,max;public int left,right,start,count;}
        Transform actor;
        Mesh lidMesh,pupilMesh;
        Vector3[] whiteRest,whitePoints,whiteNormals,lidPoints,lidNormals,pupilPoints,pupilNormals;
        BoneWeight[] whiteWeights,gazeWeights;
        Matrix4x4[] whiteBind,gazeBind,whitePose,gazePose;
        Transform[] whiteBones,gazeBones;
        Triangle[] triangles;
        Anchor[] anchors;
        int[] whiteVertices,whiteJoints,gazeJoints,triangleOrder,queryStack;
        Node[] nodes;
        int nodeCount;
        bool posed;
        float[] orientation;
        Matrix4x4 lidToActor,pupilToActor,actorToLid,actorToPupil;
        public float MinimumPupilClearance {get;private set;}
        public float MinimumLidClearance {get;private set;}

        public void Initialize(Transform owner)
        {
            actor=owner;
            whiteRest=whites.sharedMesh.vertices;whiteWeights=whites.sharedMesh.boneWeights;whiteBind=whites.sharedMesh.bindposes;whiteBones=whites.bones;
            gazeWeights=gaze.sharedMesh.boneWeights;gazeBind=gaze.sharedMesh.bindposes;gazeBones=gaze.bones;
            whitePose=new Matrix4x4[whiteBind.Length];gazePose=new Matrix4x4[gazeBind.Length];
            whitePoints=new Vector3[whiteRest.Length];whiteNormals=new Vector3[whiteRest.Length];var usedWhite=new bool[whiteRest.Length];
            foreach(int i in whiteTriangles)usedWhite[i]=true;
            whiteJoints=Joints(whiteWeights,usedWhite,whiteBind.Length);
            whiteVertices=Enumerable.Range(0,usedWhite.Length).Where(i=>usedWhite[i]).ToArray();
            var usedGaze=new bool[gazeWeights.Length];foreach(int i in pupilSourceIndices)usedGaze[i]=true;
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
            BuildTriangles();BuildTree();anchors=new Anchor[lidPoints.Length];
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
            Matrix4x4 worldToActor=actor.worldToLocalMatrix;bool changed=!posed;
            foreach(int i in whiteJoints)
            {
                var pose=worldToActor*whiteBones[i].localToWorldMatrix*whiteBind[i];
                if(!pose.Equals(whitePose[i]))changed=true;whitePose[i]=pose;
            }
            foreach(int i in gazeJoints)
            {
                var pose=worldToActor*gazeBones[i].localToWorldMatrix*gazeBind[i];
                if(!pose.Equals(gazePose[i]))changed=true;gazePose[i]=pose;
            }
            if(!changed)return;
            foreach(int i in whiteVertices){whitePoints[i]=Skin(whiteRest[i],whiteWeights[i],whitePose);whiteNormals[i]=Vector3.zero;}
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
                // Squashed eye surfaces can overlap in projection. Always use the foremost
                // hit. The tree skips irrelevant regions without relying on last frame's hit.
                bool found=FrontHit(desired,out int triangle,out Vector3 barycentric,out Vector3 hit);
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
            posed=true;
        }
        void BuildTriangles()
        {
            for(int t=0;t<triangles.Length;t++)
            {
                int a=whiteTriangles[t*3],b=whiteTriangles[t*3+1],c=whiteTriangles[t*3+2];
                Vector3 cross=Vector3.Cross(whitePoints[b]-whitePoints[a],whitePoints[c]-whitePoints[a])*orientation[t];float area=cross.magnitude;
                Vector3 pa=whitePoints[a],pb=whitePoints[b],pc=whitePoints[c];
                float uX=pb.y-pc.y,uY=pc.x-pb.x;
                triangles[t]=new Triangle{a=pa,b=pb,c=pc,normal=area>.00000001f?cross/area:Vector3.forward,area=Mathf.Max(.00000001f,area),
                    min=Vector3.Min(pa,Vector3.Min(pb,pc)),max=Vector3.Max(pa,Vector3.Max(pb,pc)),
                    uX=uX,uY=uY,vX=pc.y-pa.y,vY=pa.x-pc.x,denominator=uX*(pa.x-pc.x)+uY*(pa.y-pc.y)};
                whiteNormals[a]+=cross;whiteNormals[b]+=cross;whiteNormals[c]+=cross;
            }
            foreach(int i in whiteVertices)whiteNormals[i]=whiteNormals[i].normalized;
            if(nodes!=null)RefitTree();
        }
        Vector3 SurfaceNormal(int triangle,Vector3 barycentric)
        {
            Vector3 normal=(whiteNormals[whiteTriangles[triangle*3]]*Mathf.Max(0,barycentric.x)+whiteNormals[whiteTriangles[triangle*3+1]]*Mathf.Max(0,barycentric.y)+whiteNormals[whiteTriangles[triangle*3+2]]*Mathf.Max(0,barycentric.z)).normalized;
            return normal.sqrMagnitude>.1f?normal:triangles[triangle].normal;
        }
        static bool Ray(Vector3 point,in Triangle t,out Vector3 barycentric,out Vector3 hit)
        {
            hit=default;barycentric=default;
            if(point.x<t.min.x||point.x>t.max.x||point.y<t.min.y||point.y>t.max.y||Mathf.Abs(t.denominator)<1e-10f)return false;
            float u=(t.uX*(point.x-t.c.x)+t.uY*(point.y-t.c.y))/t.denominator;
            float v=(t.vX*(point.x-t.c.x)+t.vY*(point.y-t.c.y))/t.denominator;
            if(u<-.00001f||v<-.00001f||u+v>1.00001f)return false;
            barycentric=new Vector3(u,v,1-u-v);hit=t.a*u+t.b*v+t.c*(1-u-v);return true;
        }
        int Nearest(Vector3 point,out Vector3 hit,bool frontOnly=false)
        {
            int nearest=0,top=0;float distance=float.PositiveInfinity;hit=point;queryStack[top++]=0;
            while(top>0)
            {
                ref Node node=ref nodes[queryStack[--top]];
                if(BoxDistance(point,node)>distance)continue;
                if(node.count>0)
                {
                    for(int i=node.start;i<node.start+node.count;i++)
                    {
                        int t=triangleOrder[i];ref Triangle triangle=ref triangles[t];
                        if(frontOnly&&triangle.normal.z<=.001f)continue;
                        Vector3 p=ClosestPoint(point,triangle.a,triangle.b,triangle.c);float d=(p-point).sqrMagnitude;
                        if(d<distance||(d==distance&&t<nearest)){distance=d;nearest=t;hit=p;}
                    }
                }
                else
                {
                    int near=node.left,far=node.right;
                    if(BoxDistance(point,nodes[near])>BoxDistance(point,nodes[far])){near=node.right;far=node.left;}
                    queryStack[top++]=far;queryStack[top++]=near;
                }
            }
            return nearest;
        }
        bool FrontHit(Vector3 point,out int triangle,out Vector3 barycentric,out Vector3 hit)
        {
            triangle=0;barycentric=hit=default;bool found=false;
            int top=0;float front=float.NegativeInfinity;queryStack[top++]=0;
            while(top>0)
            {
                ref Node node=ref nodes[queryStack[--top]];
                if(point.x<node.min.x||point.x>node.max.x||point.y<node.min.y||point.y>node.max.y||node.max.z<front)continue;
                if(node.count>0)
                {
                    for(int i=node.start;i<node.start+node.count;i++)
                    {
                        int t=triangleOrder[i];
                        if(Ray(point,triangles[t],out var b,out var p)&&(p.z>front||(p.z==front&&t<triangle)))
                        {front=p.z;triangle=t;barycentric=b;hit=p;found=true;}
                    }
                }
                else
                {
                    int near=node.left,far=node.right;
                    if(nodes[near].max.z<nodes[far].max.z){near=node.right;far=node.left;}
                    queryStack[top++]=far;queryStack[top++]=near;
                }
            }
            return found;
        }
        // Build the hierarchy once from topology, then refit its bounds to each animated pose.
        // Both nearest-point and foremost-hit queries remain exact, including folded eye poses.
        void BuildTree()
        {
            triangleOrder=Enumerable.Range(0,triangles.Length).ToArray();nodes=new Node[triangles.Length*2];nodeCount=0;
            BuildNode(0,triangles.Length);queryStack=new int[nodeCount];
        }
        int BuildNode(int start,int count)
        {
            int index=nodeCount++;var min=new Vector3(float.PositiveInfinity,float.PositiveInfinity,float.PositiveInfinity);var max=-min;
            for(int i=start;i<start+count;i++){var t=triangles[triangleOrder[i]];min=Vector3.Min(min,t.min);max=Vector3.Max(max,t.max);}
            var node=new Node{min=min,max=max,start=start,count=count};
            if(count>6)
            {
                Vector3 size=max-min;int axis=size.x>=size.y&&size.x>=size.z?0:size.y>=size.z?1:2;
                Array.Sort(triangleOrder,start,count,Comparer<int>.Create((a,b)=>(triangles[a].min[axis]+triangles[a].max[axis]).CompareTo(triangles[b].min[axis]+triangles[b].max[axis])));
                int half=count/2;node.left=BuildNode(start,half);node.right=BuildNode(start+half,count-half);node.count=0;
            }
            nodes[index]=node;return index;
        }
        void RefitTree()
        {
            for(int i=nodeCount-1;i>=0;i--)
            {
                ref Node node=ref nodes[i];
                if(node.count>0)
                {
                    node.min=new Vector3(float.PositiveInfinity,float.PositiveInfinity,float.PositiveInfinity);node.max=-node.min;
                    for(int n=node.start;n<node.start+node.count;n++)
                    {ref Triangle t=ref triangles[triangleOrder[n]];node.min=Vector3.Min(node.min,t.min);node.max=Vector3.Max(node.max,t.max);}
                }
                else {node.min=Vector3.Min(nodes[node.left].min,nodes[node.right].min);node.max=Vector3.Max(nodes[node.left].max,nodes[node.right].max);}
            }
        }
        static float BoxDistance(Vector3 point,in Node node)
        {
            float x=point.x<node.min.x?node.min.x-point.x:point.x>node.max.x?point.x-node.max.x:0;
            float y=point.y<node.min.y?node.min.y-point.y:point.y>node.max.y?point.y-node.max.y:0;
            float z=point.z<node.min.z?node.min.z-point.z:point.z>node.max.z?point.z-node.max.z:0;
            return x*x+y*y+z*z;
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
        static Vector3 Skin(Vector3 point,BoneWeight w,Matrix4x4[] pose)
        {
            Vector3 result=pose[w.boneIndex0].MultiplyPoint3x4(point)*w.weight0;
            if(w.weight1>0)result+=pose[w.boneIndex1].MultiplyPoint3x4(point)*w.weight1;
            if(w.weight2>0)result+=pose[w.boneIndex2].MultiplyPoint3x4(point)*w.weight2;
            if(w.weight3>0)result+=pose[w.boneIndex3].MultiplyPoint3x4(point)*w.weight3;
            return result;
        }
        void OnDestroy(){if(lidMesh)Destroy(lidMesh);if(pupilMesh)Destroy(pupilMesh);}
    }
}
