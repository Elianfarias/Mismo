using System;
using System.Collections;
using Mismo.Core;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Equipment.Inventory;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Mismo.Gameplay.Player.Presentation
{
    public enum DemoObjective { Reach, Defeat, EquipWeapon }
    [Serializable]
    public sealed class DemoStage
    {
        public string title;
        [TextArea] public string instruction;
        public Transform destination;
        public float radius=7;
        public TutorialSequence lesson;
        public GameObject encounter;
        public DemoObjective objective;
        public WeaponDefinition weapon;
    }

    /// <summary>Authored demo only. Uses a fresh in-memory inventory, never the adventure save.</summary>
    public sealed class DemoIntroduction : MonoBehaviour
    {
        public PlayerController player;
        public TutorialGuide guide;
        public UnityEngine.Camera view;
        public DemoStage[] stages=Array.Empty<DemoStage>();
        public float introductionSeconds=5;
        public int StageIndex=>stage;
        public bool Introducing=>introducing;
        int stage;
        bool introducing,entered,started,reloading;
        float introStart;
        Vector3 cameraPosition;
        Quaternion cameraRotation;
        Health health;
        Health[] opponents=Array.Empty<Health>();
        EquipmentLoadout equipment;
        DemoStage current=>stage<stages.Length?stages[stage]:null;

        sealed class SessionInventory : IProfileRepository
        {
            public ProfileReadResult Read(Func<string,bool> validate,out string payload){payload=null;return ProfileReadResult.Missing;}
            public void Write(string payload){} // This practice session intentionally ends when the scene is reloaded.
        }

        IEnumerator Start()
        {
            if(player==null||guide==null){enabled=false;yield break;}
            health=player.GetComponent<Health>();equipment=player.GetComponent<EquipmentLoadout>();
            var inventory=player.GetComponent<PlayerInventory>()??player.gameObject.AddComponent<PlayerInventory>();
            inventory.Initialize(ProjectAssets.Load<ItemCatalog>("ItemCatalog"),new SessionInventory());
            if(player.GetComponent<InventoryPanel>()==null)player.gameObject.AddComponent<InventoryPanel>();
            foreach(var item in stages)if(item.encounter!=null)item.encounter.SetActive(false);
            // Let the normal camera and HUD initialize before owning pause/cursor state.
            yield return null;yield return null;
            if(view!=null&&introductionSeconds>0&&GameplayPause.TryPause(this))
            {
                cameraPosition=view.transform.position;cameraRotation=view.transform.rotation;
                introducing=true;introStart=Time.unscaledTime;
                while(introducing&&Time.unscaledTime-introStart<introductionSeconds)
                {
                    float t=Mathf.SmoothStep(0,1,(Time.unscaledTime-introStart)/introductionSeconds);
                    view.transform.position=Vector3.Lerp(cameraPosition+view.transform.right*3+Vector3.up*2,cameraPosition,t);
                    view.transform.rotation=Quaternion.Slerp(Quaternion.LookRotation(player.transform.position+Vector3.up-view.transform.position),cameraRotation,t);
                    if(Keyboard.current?.enterKey.wasPressedThisFrame==true||Keyboard.current?.escapeKey.wasPressedThisFrame==true)break;
                    yield return null;
                }
                EndIntroduction();
            }
            started=true;
        }
        void EndIntroduction()
        {
            if(!introducing)return;
            introducing=false;
            if(view!=null)view.transform.SetPositionAndRotation(cameraPosition,cameraRotation);
            GameplayPause.Resume(this);
        }
        void OnDisable(){StopAllCoroutines();EndIntroduction();}
        void Update()
        {
            if(!started||reloading)return;
            if(health!=null&&health.IsDead){reloading=true;StartCoroutine(Restart());return;}
            if(GameplayPause.BlocksInput||InventoryPanel.AnyOpen||WorldMapPanel.BlocksGameplay)return;
            var item=current;if(item==null)return;
            if(!entered)
            {
                if(item.destination!=null&&Vector3.Distance(player.transform.position,item.destination.position)>item.radius)return;
                if(item.lesson!=null&&!guide.WasDismissed(item.lesson))
                {guide.TryBegin(item.lesson);return;}
                entered=true;
                if(item.encounter!=null)
                {item.encounter.SetActive(true);opponents=item.encounter.GetComponentsInChildren<Health>();}
                else opponents=Array.Empty<Health>();
            }
            bool done=item.objective==DemoObjective.Reach||
                item.objective==DemoObjective.EquipWeapon&&equipment.ActiveDefinition==item.weapon||
                item.objective==DemoObjective.Defeat&&opponents.Length>0&&Array.TrueForAll(opponents,h=>h==null||h.IsDead);
            if(done){stage++;entered=false;}
            else if(Keyboard.current?.hKey.wasPressedThisFrame==true&&item.lesson!=null)guide.TryBegin(item.lesson,true);
        }
        IEnumerator Restart()
        {
            yield return new WaitForSecondsRealtime(2);
            SceneManager.LoadScene(gameObject.scene.path);
        }
        void OnGUI()
        {
            if(introducing)
            {
                var matrix=GUI.matrix;GUI.depth=-400;
                float scale=PlayerHUD.Scale;GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);
                float w=Screen.width/scale,h=Screen.height/scale;
                PlayerHUD.Fill(new Rect(0,0,w,70),Color.black);PlayerHUD.Fill(new Rect(0,h-110,w,110),Color.black);
                QuietFantasyUI.Text(new Rect(0,h-91,w,35),"Al otro lado de la cueva, un nuevo comienzo.",25,Color.white,false,TextAnchor.MiddleCenter);
                QuietFantasyUI.Text(new Rect(0,h-43,w,25),"Enter · Omitir introducción",15,PlayerHUD.Gold,false,TextAnchor.MiddleCenter);
                float fade=1-Mathf.Clamp01((Time.unscaledTime-introStart)/1.1f);
                if(fade>0)PlayerHUD.Fill(new Rect(0,0,w,h),new Color(0,0,0,fade));
                GUI.matrix=matrix;return;
            }
            if(!started||guide==null||guide.IsOpen||GameplayPause.IsPaused||InventoryPanel.AnyOpen||WorldMapPanel.AnyOpen)return;
            GUI.depth=0;
            var previous=GUI.matrix;GUI.matrix=Matrix4x4.Scale(Vector3.one*PlayerHUD.Scale);
            float width=Screen.width/PlayerHUD.Scale;
            var panel=new Rect(width-430,34,402,132);PlayerHUD.Fill(panel,new Color(.035f,.05f,.06f,.88f));
            QuietFantasyUI.Text(new Rect(panel.x+18,panel.y+12,366,28),current!=null?current.title:"Llegaste al pueblo",23,PlayerHUD.Gold);
            QuietFantasyUI.Text(new Rect(panel.x+18,panel.y+48,366,76),current!=null?current.instruction:"Acercate a los habitantes y usá F para conversar. J abre el diario de misiones.",17);
            GUI.matrix=previous;
        }
    }
}
