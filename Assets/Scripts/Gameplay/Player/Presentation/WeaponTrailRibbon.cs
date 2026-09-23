using System;
using System.Collections.Generic;
using UnityEngine;
using Mismo.Gameplay.Player.Equipment;
namespace Mismo.Gameplay.Player.Presentation
{
    /// <summary>World-space blade history shared by gameplay and the weapon workshop.</summary>
    public sealed class WeaponTrailRibbon : IDisposable
    {
        struct Sample { public Vector3 a,b; public float time; public int stroke; }
        readonly List<Sample> samples=new List<Sample>(192);
        readonly List<Vector3> vertices=new List<Vector3>(384);
        readonly List<Color> colors=new List<Color>(384);
        readonly List<Vector2> uv=new List<Vector2>(384);
        readonly List<int> triangles=new List<int>(1152);
        readonly GameObject root;
        readonly Mesh mesh;
        readonly MeshRenderer renderer;
        readonly Material fallback;
        Transform lastVisual;
        bool emitting;
        int stroke;
        float lastTime=-1;
        public int SampleCount=>samples.Count;
        public Mesh Mesh=>mesh;
        public WeaponTrailRibbon(Transform parent,Material material)
        {
            fallback=material;
            root=new GameObject("Procedural melee trail"){hideFlags=HideFlags.DontSave,layer=2};
            if(parent!=null)UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,parent.gameObject.scene);
            root.transform.SetParent(parent,false);
            mesh=new Mesh{name="Procedural blade ribbon",hideFlags=HideFlags.DontSave};mesh.MarkDynamic();
            root.AddComponent<MeshFilter>().sharedMesh=mesh;
            renderer=root.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
        }
        public void Clear(){samples.Clear();mesh.Clear();emitting=false;lastTime=-1;lastVisual=null;}
        public void SampleBlade(Transform visual,WeaponPoseProfile profile,float now,bool emit,bool secondary=false)
        {
            if(profile==null||visual==null||!visual.gameObject.activeInHierarchy||!profile.meleeTrail){Clear();return;}
            if(lastVisual!=visual||now<lastTime)Clear();
            bool timeAdvanced=lastTime<0||now>lastTime+.000001f;
            lastVisual=visual;lastTime=now;
            float life=Mathf.Max(.01f,profile.trailDuration);
            while(samples.Count>0&&now-samples[0].time>life)samples.RemoveAt(0);
            Vector3 a=visual.TransformPoint(secondary?profile.secondaryTrailBase:profile.trailBase);
            Vector3 b=visual.TransformPoint(secondary?profile.secondaryTrailTip:profile.trailTip);
            if(emit&&timeAdvanced)
            {
                if(!emitting)stroke++;
                if(samples.Count>0&&(samples[samples.Count-1].b-b).sqrMagnitude>25) {samples.Clear();stroke++;}
                if(samples.Count>0&&Vector3.Dot(samples[samples.Count-1].b-samples[samples.Count-1].a,b-a)<0)stroke++;
                if(samples.Count>1)
                {
                    var prev=samples[samples.Count-1];var older=samples[samples.Count-2];
                    Vector3 before=(prev.a+prev.b-older.a-older.b)*.5f,after=(a+b-prev.a-prev.b)*.5f;
                    if(prev.stroke==older.stroke&&before.sqrMagnitude>.00001f&&after.sqrMagnitude>.00001f&&Vector3.Dot(before.normalized,after.normalized)<-.2f)stroke++;
                }
                bool moved=samples.Count==0||(samples[samples.Count-1].a-a).sqrMagnitude+(samples[samples.Count-1].b-b).sqrMagnitude>.000001f;
                if(moved){if(samples.Count>=192)samples.RemoveAt(0);samples.Add(new Sample{a=a,b=b,time=now,stroke=stroke});}
            }
            if(timeAdvanced)emitting=emit;
            vertices.Clear();colors.Clear();uv.Clear();triangles.Clear();
            for(int i=0;i<samples.Count;i++)
            {
                var s=samples[i];float age=Mathf.Clamp01((now-s.time)/life);
                float width=Mathf.Lerp(1,1-age,Mathf.Clamp01(profile.trailTaper));
                Vector3 mid=(s.a+s.b)*.5f;
                vertices.Add(root.transform.InverseTransformPoint(Vector3.Lerp(mid,s.a,width)));
                vertices.Add(root.transform.InverseTransformPoint(Vector3.Lerp(mid,s.b,width)));
                Color c=Color.Lerp(profile.trailStartColor,profile.trailEndColor,age);c.a*=1-age;
                colors.Add(c);colors.Add(c);uv.Add(new Vector2(age,0));uv.Add(new Vector2(age,1));
                if(i>0&&samples[i-1].stroke==s.stroke){int n=i*2;triangles.Add(n-2);triangles.Add(n);triangles.Add(n-1);triangles.Add(n-1);triangles.Add(n);triangles.Add(n+1);}
            }
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
            renderer.sharedMaterial=profile.trailMaterial!=null?profile.trailMaterial:fallback;
        }
        public void Dispose(){Destroy(mesh);Destroy(root);}
        static void Destroy(UnityEngine.Object value){if(value==null)return;if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);}
    }
}
