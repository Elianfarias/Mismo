using System;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.Presentation;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

namespace Mismo.Gameplay.Player.World
{
    [DefaultExecutionOrder(100)]
    public sealed class DragonArrivalSequence : MonoBehaviour
    {
        public bool Running {get;private set;}
        public bool Departing => !Running && visual!=null;
        public float Elapsed {get;private set;}
        public Vector3 FlightPosition => visual!=null?visual.transform.position:Vector3.zero;
        GameObject visual;
        Renderer[] renderers;
        readonly Plane[] frustum=new Plane[6];
        DragonArcDefinition definition;
        UnityEngine.Camera view;
        Vector3 cameraPosition,start,velocity;
        Quaternion cameraRotation;
        float cameraFov;
        Health health;
        PlayableGraph graph;
        Action finished;
        public bool Begin(DragonArcDefinition data, Vector3 arrival, Quaternion facing, Action onFinished)
        {
            if(Running || Departing || data.flyingVisual==null || data.flight==null || UnityEngine.Camera.main==null || !GameplayPause.TryBlockInput(this))return false;
            Running=true;Elapsed=0;finished=onFinished;definition=data;health=GetComponent<Health>();
            view=UnityEngine.Camera.main;cameraPosition=view.transform.position;cameraRotation=view.transform.rotation;
            cameraFov=view.fieldOfView;
            try
            {
                start=arrival+facing*data.flybyStartOffset;
                var direction=data.flybyDirection.sqrMagnitude>.001f?data.flybyDirection.normalized:Vector3.right;
                velocity=facing*direction*Mathf.Max(1,data.flybySpeed);
                visual=Instantiate(data.flyingVisual,start,Quaternion.LookRotation(velocity));visual.transform.localScale*=data.flyingVisualScale;
                foreach(var collider in visual.GetComponentsInChildren<Collider>())collider.enabled=false;
                renderers=visual.GetComponentsInChildren<Renderer>();
                var animator=visual.GetComponentInChildren<Animator>();
                animator.runtimeAnimatorController=null;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                graph=PlayableGraph.Create("Dragon arrival visual");
                var clip=AnimationClipPlayable.Create(graph,data.flight);clip.SetApplyFootIK(false);
                AnimationPlayableOutput.Create(graph,"Flight",animator).SetSourcePlayable(clip);graph.Play();
                return true;
            }
            catch { Abort();throw; }
        }
        void LateUpdate()
        {
            if(visual==null)return;
            if(health==null || health.IsDead){Abort();return;}
            Elapsed+=Time.deltaTime;visual.transform.position=start+velocity*Elapsed;
            if(Running)
            {
                if(view==null){Abort();return;}
                float duration=Mathf.Max(2,definition.flybyCinematicTime);
                Quaternion tracking=Quaternion.LookRotation(visual.transform.position+Vector3.up*2-cameraPosition);
                float returnBlend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(duration-Mathf.Min(duration,definition.flybyCameraReturnTime),duration,Elapsed));
                view.fieldOfView=Mathf.Lerp(Mathf.Lerp(cameraFov,Mathf.Min(cameraFov,definition.flybyCameraFov),Mathf.SmoothStep(0,1,Elapsed/1.2f)),cameraFov,returnBlend);
                view.transform.SetPositionAndRotation(cameraPosition,Quaternion.Slerp(Quaternion.Slerp(cameraRotation,tracking,Mathf.SmoothStep(0,1,Elapsed/1.2f)),cameraRotation,returnBlend));
                if(Elapsed>=duration || Keyboard.current?.escapeKey.wasPressedThisFrame==true)FinishCinematic(true);
            }
            // Releasing the camera never removes the dragon. It keeps its speed and animation
            // until its complete silhouette is outside the player's current view.
            else if(Vector3.Distance(visual.transform.position,transform.position)>=definition.flybyCleanupDistance && !VisibleInCamera())DisposeVisual();
        }
        bool VisibleInCamera()
        {
            var camera=UnityEngine.Camera.main;if(camera==null)return false;
            GeometryUtility.CalculateFrustumPlanes(camera,frustum);
            foreach(var renderer in renderers)
                if(renderer!=null && renderer.enabled && GeometryUtility.TestPlanesAABB(frustum,renderer.bounds))return true;
            return false;
        }
        public void Skip(){if(Running)FinishCinematic(true);}
        void FinishCinematic(bool complete)
        {
            if(!Running)return;
            Running=false;
            if(view!=null){view.transform.SetPositionAndRotation(cameraPosition,cameraRotation);view.fieldOfView=cameraFov;}
            GameplayPause.ReleaseInput(this);
            var callback=finished;finished=null;if(complete)callback?.Invoke();
        }
        void DisposeVisual(){if(graph.IsValid())graph.Destroy();if(visual!=null)Destroy(visual);visual=null;renderers=null;}
        void Abort(){FinishCinematic(false);DisposeVisual();}
        void OnDisable()=>Abort();
        void OnGUI()
        {
            if(!Running)return;
            GUI.depth=-450;float bar=Mathf.Min(80,Screen.height*.09f);
            PlayerHUD.Fill(new Rect(0,0,Screen.width,bar),Color.black);
            PlayerHUD.Fill(new Rect(0,Screen.height-bar,Screen.width,bar),Color.black);
            QuietFantasyUI.Text(new Rect(30,Screen.height-bar+18,Screen.width-60,34),"Una sombra sobre el pueblo                                      Esc · Omitir",20,Color.white);
        }
    }
}
