using System.Collections;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.Equipment.Inventory;
using Mismo.Gameplay.Player.Movement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using L=Mismo.Gameplay.Player.Localization.GameLanguage;
namespace Mismo.Gameplay.Player.World
{
    [DefaultExecutionOrder(-120)]
    public sealed class CompanionPlayer:MonoBehaviour
    {
        PlayerInventory inventory;GameObject companion;CreatureSpecies species;NavMeshAgent agent;CharacterController mountBody,body;
        PlayerController controller;PlayerMotor motor;Presentation.PlayerAnimationDriver animationDriver;Animator animator;
        Vector3 visualPosition,visualScale;Quaternion visualRotation;bool loading;float retryAt;string notice;
        Animator mountAnimator;bool hasMotion,hasRate;
        GameObject pendingModel;Transform mountVisual;string loadedMountId;float groundOffset;
        CapsuleCollider ridingTarget;
        public bool Riding{get;private set;}
        public static bool IsRiding(GameObject player)=>player!=null&&player.GetComponent<CompanionPlayer>()?.Riding==true;
        void Awake(){inventory=GetComponent<PlayerInventory>();controller=GetComponent<PlayerController>();body=GetComponent<CharacterController>();motor=GetComponent<PlayerMotor>();animationDriver=GetComponent<Presentation.PlayerAnimationDriver>();animator=animationDriver!=null?animationDriver.Animator:GetComponentInChildren<Animator>();}
        void OnEnable(){GetComponent<Health>().Died+=OnDeath;}
        void OnDeath(DamageInfo damage)=>Recall();
        void OnDisable(){GetComponent<Health>().Died-=OnDeath;Recall();}
        public void Recall()
        {
            StopAllCoroutines();loading=false;retryAt=0;
            RestoreRider();
            if(companion!=null){companion.SetActive(false);Destroy(companion);}companion=null;
            if(pendingModel!=null)Destroy(pendingModel);pendingModel=null;
            mountVisual=null;mountAnimator=null;loadedMountId=null;
        }
        void Update()
        {
            if (Mismo.Gameplay.Player.Presentation.GameplayPause.BlocksInput) return;
            if(inventory==null||!inventory.IsReady||GetComponent<Health>().IsDead)return;
            if(!inventory.CompanionSummoned||companion!=null&&loadedMountId!=inventory.SelectedMountId){Recall();if(!inventory.CompanionSummoned)return;}
            if(companion==null){if(!loading&&!string.IsNullOrEmpty(inventory.CompanionSpeciesId)&&Time.unscaledTime>=retryAt){retryAt=Time.unscaledTime+2;StartCoroutine(Spawn());}return;}
            if(!inventory.HasSeenSpecies(species.id)&&CreatureSpecies.Visible(companion.transform,transform))inventory.DiscoverSpecies(species.id);
            var key=Keyboard.current;
            bool menu=InventoryPanel.AnyOpen||Presentation.WorldMapPanel.AnyOpen||GetComponent<GatheringPlayer>()?.Busy==true;
            if(!menu&&key?.vKey.wasPressedThisFrame==true){if(Riding)Dismount();else if(Vector3.Distance(transform.position,companion.transform.position)<3&&inventory.CanManage)Mount();}
            if(Riding)
            {
                Vector2 move=menu?Vector2.zero:GetComponent<Input.PlayerInputReader>().Move;
                var camera=UnityEngine.Camera.main;var forward=camera!=null?Vector3.ProjectOnPlane(camera.transform.forward,Vector3.up).normalized:Vector3.forward;
                var velocity=(forward*move.y+Vector3.Cross(Vector3.up,forward)*move.x)*species.ridingSpeed;
                mountBody.SimpleMove(velocity);if(velocity.sqrMagnitude>.1f)companion.transform.rotation=Quaternion.RotateTowards(companion.transform.rotation,Quaternion.LookRotation(velocity),Time.deltaTime*species.turnSpeed);
                TickAnimation(mountBody.velocity.magnitude);
                return;
            }
            if(!menu&&key?.hKey.wasPressedThisFrame==true&&Vector3.Distance(transform.position,companion.transform.position)<4)inventory.SetCompanionWaiting(!inventory.CompanionWaiting);
            if(agent!=null&&agent.enabled&&agent.isOnNavMesh){agent.isStopped=inventory.CompanionWaiting||Vector3.Distance(transform.position,companion.transform.position)<2.5f;if(!agent.isStopped)agent.SetDestination(transform.position);}
            TickAnimation(agent!=null&&agent.enabled?agent.velocity.magnitude:0);
        }
        IEnumerator Spawn()
        {
            loading=true;string requested=inventory.SelectedMountId;species=CreatureSpecies.Find(inventory.CompanionSpeciesId);
            var prefab=species?.prefabs!=null?System.Array.Find(species.prefabs,p=>p!=null&&p.name==inventory.CompanionPrefabName):null;
            if(prefab==null||!NavMesh.SamplePosition(transform.position-transform.forward*3,out var hit,8,NavMesh.AllAreas)){loading=false;yield break;}
            var model=new GameObject(prefab.name);pendingModel=model;var visual=CloneVisual(prefab);visual.transform.SetParent(model.transform,false);visual.transform.localPosition=Vector3.zero;
            yield return null;
            if(!inventory.CompanionSummoned||requested!=inventory.SelectedMountId||GetComponent<Health>().IsDead){Destroy(model);pendingModel=null;loading=false;yield break;}
            companion=model;pendingModel=null;mountVisual=visual.transform;loadedMountId=requested;model.name="Companion - "+species.displayName;model.transform.position=hit.position;
            agent=model.AddComponent<NavMeshAgent>();agent.speed=species.followingSpeed;agent.stoppingDistance=2.5f;agent.radius=.65f;agent.height=1.4f;
            mountAnimator=model.GetComponentInChildren<Animator>();hasMotion=mountAnimator!=null&&System.Array.Exists(mountAnimator.parameters,p=>p.name=="Motion");
            hasRate=mountAnimator!=null&&System.Array.Exists(mountAnimator.parameters,p=>p.name=="PlaybackRate");TickAnimation(0);
            mountBody=model.AddComponent<CharacterController>();mountBody.radius=.65f;mountBody.height=1.4f;mountBody.center=Vector3.up*.7f;mountBody.stepOffset=.35f;mountBody.enabled=false;
            loading=false;
        }
        public static GameObject CloneVisual(GameObject prefab)
        {
            var mapping=new System.Collections.Generic.Dictionary<Transform,Transform>();
            var root=new GameObject(prefab.name);
            CopyTransforms(prefab.transform,root.transform,mapping);
            foreach(var original in prefab.GetComponentsInChildren<Renderer>(true))
            {
                if(!original.enabled)continue;
                var target=mapping[original.transform].gameObject;
                if(original is SkinnedMeshRenderer skin){var copy=target.AddComponent<SkinnedMeshRenderer>();copy.sharedMesh=skin.sharedMesh;copy.sharedMaterials=skin.sharedMaterials;copy.bones=System.Array.ConvertAll(skin.bones,b=>b!=null&&mapping.ContainsKey(b)?mapping[b]:null);copy.rootBone=skin.rootBone!=null?mapping[skin.rootBone]:null;copy.localBounds=skin.localBounds;copy.updateWhenOffscreen=true;}
                else if(original is MeshRenderer){var mesh=original.GetComponent<MeshFilter>();if(mesh!=null){target.AddComponent<MeshFilter>().sharedMesh=mesh.sharedMesh;target.AddComponent<MeshRenderer>().sharedMaterials=original.sharedMaterials;}}
            }
            foreach(var original in prefab.GetComponentsInChildren<Animator>(true)){var copy=mapping[original.transform].gameObject.AddComponent<Animator>();copy.avatar=original.avatar;copy.runtimeAnimatorController=original.runtimeAnimatorController;copy.applyRootMotion=false;copy.cullingMode=AnimatorCullingMode.AlwaysAnimate;}
            return root;
        }
        static void CopyTransforms(Transform original,Transform copy,System.Collections.Generic.Dictionary<Transform,Transform> mapping)
        {
            mapping[original]=copy;copy.localPosition=original.localPosition;copy.localRotation=original.localRotation;copy.localScale=original.localScale;
            copy.gameObject.SetActive(original.gameObject.activeSelf);
            foreach(Transform child in original){var next=new GameObject(child.name).transform;next.SetParent(copy,false);CopyTransforms(child,next,mapping);}
        }
        void TickAnimation(float speed)
        {
            if(mountAnimator==null)return;
            int motion=speed>.1f?(speed>2.1f?2:1):0;
            if(hasMotion)mountAnimator.SetInteger("Motion",motion);
            if(hasRate)mountAnimator.SetFloat("PlaybackRate",Mathf.Clamp(speed/(motion==2?3.2f:1.6f),.7f,1.5f));
        }
        void GroundVisual()
        {
            if(companion==null||mountVisual==null)return;
            mountVisual.localPosition=Vector3.zero;groundOffset=0;
            var origin=companion.transform.position+Vector3.up;
            float nearest=float.PositiveInfinity;
            foreach(var hit in Physics.RaycastAll(origin,Vector3.down,2,~0,QueryTriggerInteraction.Ignore))
            {
                if(hit.normal.y<.5f||hit.transform.IsChildOf(companion.transform)||hit.transform.IsChildOf(transform)||hit.collider.GetComponentInParent<Health>()!=null)continue;
                if(hit.distance<nearest){nearest=hit.distance;groundOffset=hit.point.y-companion.transform.position.y;}
            }
            mountVisual.position+=Vector3.up*groundOffset;
        }
        void Mount()
        {
            if(species==null||!species.mountable||species.riderPose==null||companion==null||!inventory.CanManage||motor.IsFlying)return;
            agent.enabled=false;mountBody.enabled=true;controller.enabled=false;body.enabled=false;
            if(ridingTarget==null){ridingTarget=gameObject.AddComponent<CapsuleCollider>();ridingTarget.center=body.center;ridingTarget.radius=body.radius;ridingTarget.height=body.height;}
            ridingTarget.enabled=true;Physics.IgnoreCollision(ridingTarget,mountBody,true);
            if(animationDriver!=null)animationDriver.enabled=false;if(animator!=null)animator.enabled=false;
            if(motor.Visual!=null){visualPosition=motor.Visual.localPosition;visualRotation=motor.Visual.localRotation;visualScale=motor.Visual.localScale;}
            Riding=true;notice=null;
        }
        void LateUpdate()
        {
            GroundVisual();
            if(!Riding||companion==null||GetComponent<Health>().IsDead)return;
            var pose=species.riderPose;transform.SetPositionAndRotation(companion.transform.TransformPoint(pose.position)+Vector3.up*groundOffset,companion.transform.rotation*Quaternion.Euler(pose.rotation));
            if(motor.Visual!=null){motor.Visual.localPosition=visualPosition;motor.Visual.localRotation=Quaternion.identity;motor.Visual.localScale=visualScale*pose.scale;}
            if(animator!=null&&pose.seatedAnimation!=null)pose.seatedAnimation.SampleAnimation(animator.gameObject,pose.sampleTime*pose.seatedAnimation.length);
            pose.Apply(animator!=null?animator.transform:transform);
        }
        void Dismount()
        {
            Vector3 destination=default;bool found=false;
            for(int i=0;i<12;i++)
            {
                var p=companion.transform.position+Quaternion.Euler(0,i*30,0)*Vector3.right*2.2f;
                if(!NavMesh.SamplePosition(p,out var hit,.8f,NavMesh.AllAreas))continue;
                if(Physics.CheckCapsule(hit.position+Vector3.up*.4f,hit.position+Vector3.up*1.6f,.32f,~0,QueryTriggerInteraction.Ignore))continue;
                destination=hit.position+Vector3.up*.1f;found=true;break;
            }
            if(!found){notice=L.Text("No hay espacio para desmontar.");return;}
            RestoreRider();agent.enabled=true;if(agent.isOnNavMesh)agent.Warp(companion.transform.position);
            motor.ResetPosition(destination);notice=null;
        }
        void RestoreRider()
        {
            if(!Riding)return;Riding=false;
            if(ridingTarget!=null)ridingTarget.enabled=false;if(mountBody!=null)mountBody.enabled=false;
            if(motor!=null&&motor.Visual!=null){motor.Visual.localPosition=visualPosition;motor.Visual.localRotation=visualRotation;motor.Visual.localScale=visualScale;}
            if(animator!=null){animator.enabled=true;animator.Rebind();}if(animationDriver!=null)animationDriver.enabled=true;
            if(body!=null)body.enabled=true;if(controller!=null)controller.enabled=true;
        }
        void OnGUI()
        {
            if (Mismo.Gameplay.Player.Presentation.GameplayPause.BlocksInput) return;
            if(companion==null||InventoryPanel.AnyOpen||Presentation.WorldMapPanel.AnyOpen)return;
            if(Riding||Vector3.Distance(transform.position,companion.transform.position)<4)
                GUI.Box(new Rect(Screen.width/2-230,Screen.height-250,460,40),notice??L.Text(Riding?"[V] Desmontar":"[V] Montar · [H] Seguir / esperar"));
        }
        void OnDestroy()=>Recall();
    }
}

