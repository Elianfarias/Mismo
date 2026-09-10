using Mismo.Gameplay.Player.World;
using UnityEngine;
namespace Mismo.Gameplay.Player.Presentation
{
    // One bounded emitter per player, reusable for water, dirt, leaves and impacts.
    public sealed class WorldSurfaceFeedback : MonoBehaviour
    {
        ParticleSystem particles;
        Material material;
        Mesh cube;
        Vector3 previous;
        float nextSplash;
        void Awake()
        {
            previous=transform.position;
            var go=new GameObject("Contextual voxel particles");go.transform.SetParent(transform,false);
            particles=go.AddComponent<ParticleSystem>();particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=particles.main;main.loop=false;main.playOnAwake=false;main.maxParticles=64;main.startLifetime=.5f;
            main.startSize=.12f;main.gravityModifier=1;main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=particles.emission;emission.enabled=false;var shape=particles.shape;shape.enabled=false;
            var geometry=new VoxelRegionGeometry();geometry.Box(Vector3.zero,Vector3.one,Color.white);cube=geometry.Mesh("Feedback cube");
            var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=cube;
            material=RuntimeParticleMaterial.Create("Contextual particles",Color.white);renderer.sharedMaterial=material;
        }
        public void Emit(Vector3 point,Color color,float strength=1,int count=12)
        {
            for(int i=0;i<Mathf.Clamp(count,1,24);i++)
            {
                float angle=i*2.39996f;float speed=Mathf.Clamp(strength,.5f,2.5f);
                particles.Emit(new ParticleSystem.EmitParams{position=point,velocity=new Vector3(Mathf.Cos(angle)*speed,1.5f+speed*(i%3)*.3f,Mathf.Sin(angle)*speed),startColor=color,startSize=.09f+.03f*(i%3),startLifetime=.4f+.05f*(i%4)},1);
            }
        }
        void LateUpdate()
        {
            Vector3 current=transform.position;
            if(Time.time>=nextSplash&&Vector3.Distance(previous,current)<8)
                foreach(var water in WorldWaterSurface.Active)
                {
                    float height=water.transform.position.y;
                    if(water.Contains(current)&&previous.y>height+.02f&&current.y<=height+.02f)
                    {Emit(new Vector3(current.x,height,current.z),water.color,Mathf.Abs(current.y-previous.y)/Mathf.Max(.001f,Time.deltaTime)*.15f);nextSplash=Time.time+.3f;break;}
                }
            previous=current;
        }
        void OnDestroy(){if(material!=null)Destroy(material);if(cube!=null)Destroy(cube);}
    }
}
