using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Equipment.Inventory;
using Mismo.Gameplay.Player.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using L=Mismo.Gameplay.Player.Localization.GameLanguage;
namespace Mismo.Gameplay.Player.World
{
    [DefaultExecutionOrder(-110)]
    public sealed class GatheringPlayer:MonoBehaviour
    {
        PlayerInventory inventory;Health health;EquipmentLoadout loadout;PlayerInputReader input;
        GatheringNode target,harvesting;
        CraftingStation nearby,station;
        float progress,nextHit;Vector3 startPosition;GameObject tool;
        CursorLockMode oldLock;bool oldVisible;
        int closedFrame=-1;bool stationOpen;
        readonly Presentation.RecipeBookView recipeBook=new Presentation.RecipeBookView();
        public bool IsHarvesting=>harvesting!=null;
        public bool BlocksGameplay=>stationOpen||closedFrame==Time.frameCount;
        public bool Busy=>IsHarvesting||BlocksGameplay;
        void Awake(){inventory=GetComponent<PlayerInventory>();health=GetComponent<Health>();loadout=GetComponent<EquipmentLoadout>();input=GetComponent<PlayerInputReader>();health.Damaged+=Damaged;}
        void Start(){if(GetComponent<RegionRespawn>()!=null&&GetComponent<GatheringArrival>()==null)gameObject.AddComponent<GatheringArrival>();}
        void Damaged(DamageInfo damage){Cancel();Close();}
        void OnDisable(){Cancel();Close();}
        void OnDestroy(){recipeBook.Dispose();if(health!=null)health.Damaged-=Damaged;}
        void LateUpdate(){if(stationOpen)recipeBook.Render();}
        public void Cancel(){harvesting=null;progress=0;if(tool!=null)Destroy(tool);tool=null;}
        void Close(){if(!stationOpen)return;GameAudio.Play(GameSound.MenuClose);stationOpen=false;station=null;recipeBook.Dispose();closedFrame=Time.frameCount;Cursor.lockState=oldLock;Cursor.visible=oldVisible;}
        bool Interrupted()=>health.IsDead||input==null||input.Move.sqrMagnitude>.04f||input.WasAttackPressedThisFrame()||input.WasDashPressedThisFrame()||
            input.WasJumpPressedThisFrame()||input.WasLungePressedThisFrame()||input.WasParryPressedThisFrame()||input.WasSpinAttackPressedThisFrame()||
            loadout.Runner.IsBusy||loadout.Belt!=null&&loadout.Belt.IsActive||InventoryPanel.AnyOpen||Presentation.WorldMapPanel.AnyOpen;
        public bool TryOpenStation(CraftingStation candidate)
        {
            if(candidate==null||!candidate.InRange(transform.position)||inventory==null||!inventory.CanManage||health==null||health.IsDead||Busy||
                CompanionPlayer.IsRiding(gameObject)||InventoryPanel.AnyOpen||Presentation.WorldMapPanel.AnyOpen||Presentation.GameplayPause.BlocksInput)return false;
            station=candidate;stationOpen=true;GameAudio.Play(GameSound.MenuOpen);
            oldLock=Cursor.lockState;oldVisible=Cursor.visible;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;return true;
        }
        void Update()
        {
            if (Mismo.Gameplay.Player.Presentation.GameplayPause.BlocksInput) return;
            if(!inventory.IsReady)return;
            if(CompanionPlayer.IsRiding(gameObject)){Cancel();Close();return;}
            if(stationOpen)
            {
                if(station==null||!station.InRange(transform.position)||health.IsDead||loadout.InCombat||Keyboard.current?.escapeKey.wasPressedThisFrame==true)Close();
                return;
            }
            if(IsHarvesting)
            {
                if(!harvesting.isActiveAndEnabled||!harvesting.Available||!harvesting.InRange(transform)||Vector3.Distance(transform.position,startPosition)>.3f||Interrupted())
                {Cancel();return;}
                progress+=Time.deltaTime;
                if(progress>=nextHit){nextHit=progress+.55f;ResourceChips.Emit(harvesting.gameObject,transform.position);if(harvesting.definition.harvestSound!=null)AudioSource.PlayClipAtPoint(harvesting.definition.harvestSound,harvesting.transform.position,.5f);}
                if(tool!=null)tool.transform.localRotation=Quaternion.Euler(Mathf.Sin(progress*12)*40,0,0);
                if(progress>=harvesting.definition.harvestSeconds){harvesting.Complete(inventory);Cancel();}
                return;
            }
            target=null;nearby=null;
            if(health.IsDead||InventoryPanel.AnyOpen||Presentation.WorldMapPanel.AnyOpen||loadout.Runner.IsBusy)return;
            float best=float.MaxValue;
            foreach(var node in GatheringNode.Loaded)if(node!=null&&node.Available&&node.InRange(transform))
            {float distance=Vector3.SqrMagnitude(node.transform.position-transform.position);if(distance<best){target=node;best=distance;}}
            foreach(var candidate in CraftingStation.Loaded)if(candidate!=null&&candidate.InRange(transform.position)){nearby=candidate;break;}
            // G is independent from F (chest/loot) and Q/E/R (combat skills).
            if(Keyboard.current?.gKey.wasPressedThisFrame!=true)return;
            if(TryOpenStation(nearby))return;
            if(target==null||Interrupted())return;
            harvesting=target;startPosition=transform.position;progress=0;nextHit=.55f;
            ResourceChips.Emit(harvesting.gameObject,transform.position);
            if(target.definition.toolPrefab!=null){tool=Instantiate(target.definition.toolPrefab,transform);tool.transform.localPosition=new Vector3(.45f,1,.6f);}
            if(target.definition.harvestSound!=null)AudioSource.PlayClipAtPoint(target.definition.harvestSound,target.transform.position);
        }
        void OnGUI()
        {
            if (Mismo.Gameplay.Player.Presentation.GameplayPause.BlocksInput) return;
            if(inventory==null||health==null||health.IsDead)return;
            if(stationOpen&&station!=null){int depth=GUI.depth;GUI.depth=-45;DrawStation();GUI.depth=depth;return;}
            if(InventoryPanel.AnyOpen||Presentation.WorldMapPanel.AnyOpen)return;
            var style=new GUIStyle(GUI.skin.box){font=Mismo.Gameplay.Player.Presentation.QuietFantasyUI.Body,fontSize=18,wordWrap=true};
            if(IsHarvesting)
            {
                GUI.Box(new Rect(Screen.width/2-200,Screen.height-195,400,50),L.Format("Recolectando: {0} · {1:0}%",L.Text(harvesting.definition.displayName),Mathf.Clamp01(progress/harvesting.definition.harvestSeconds)*100),style);
            }
            else if(nearby!=null||target!=null)GUI.Box(new Rect(Screen.width/2-200,Screen.height-195,400,50),nearby!=null?L.Text("[G] Fabricar en el banco"):L.Format("[G] {0} · {1}",L.Text(target.definition.actionName),L.Text(target.definition.displayName)),style);
        }
        void DrawStation()
        {
            
            var old=GUI.matrix;float scale=Mathf.Min(Screen.width/1280f,Screen.height/800f);
            GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1280*scale)/2,(Screen.height-800*scale)/2),Quaternion.identity,Vector3.one*scale);
            recipeBook.Draw(inventory,station,Close,()=>{Close();GetComponent<InventoryPanel>()?.OpenRadialMenu();});
            GUI.matrix=old;
        }
    }
}
