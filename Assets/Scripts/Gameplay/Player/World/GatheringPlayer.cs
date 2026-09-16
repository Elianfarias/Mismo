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
        int category,slot,closedFrame=-1;Vector2 scroll;
        public bool IsHarvesting=>harvesting!=null;
        public bool BlocksGameplay=>station!=null||closedFrame==Time.frameCount;
        public bool Busy=>IsHarvesting||BlocksGameplay;
        void Awake(){inventory=GetComponent<PlayerInventory>();health=GetComponent<Health>();loadout=GetComponent<EquipmentLoadout>();input=GetComponent<PlayerInputReader>();health.Damaged+=Damaged;}
        void Start(){if(GetComponent<RegionRespawn>()!=null&&GetComponent<GatheringArrival>()==null)gameObject.AddComponent<GatheringArrival>();}
        void Damaged(DamageInfo damage){Cancel();Close();}
        void OnDisable(){Cancel();Close();}
        void OnDestroy(){if(health!=null)health.Damaged-=Damaged;}
        public void Cancel(){harvesting=null;progress=0;if(tool!=null)Destroy(tool);tool=null;}
        void Close(){if(station==null)return;GameAudio.Play(GameSound.MenuClose);station=null;closedFrame=Time.frameCount;Cursor.lockState=oldLock;Cursor.visible=oldVisible;}
        bool Interrupted()=>health.IsDead||input==null||input.Move.sqrMagnitude>.04f||input.WasAttackPressedThisFrame()||input.WasDashPressedThisFrame()||
            input.WasJumpPressedThisFrame()||input.WasLungePressedThisFrame()||input.WasParryPressedThisFrame()||input.WasSpinAttackPressedThisFrame()||
            loadout.Runner.IsBusy||loadout.Belt!=null&&loadout.Belt.IsActive||InventoryPanel.AnyOpen||Presentation.WorldMapPanel.AnyOpen;
        void Update()
        {
            if (Mismo.Gameplay.Player.Presentation.GameplayPause.BlocksInput) return;
            if(!inventory.IsReady)return;
            if(CompanionPlayer.IsRiding(gameObject)){Cancel();Close();return;}
            if(station!=null)
            {
                if(!station.InRange(transform.position)||health.IsDead||loadout.InCombat||Keyboard.current?.escapeKey.wasPressedThisFrame==true)Close();
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
            if(nearby!=null&&inventory.CanManage)
            {station=nearby;GameAudio.Play(GameSound.MenuOpen);oldLock=Cursor.lockState;oldVisible=Cursor.visible;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;return;}
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
            if(station!=null){int depth=GUI.depth;GUI.depth=-45;DrawStation();GUI.depth=depth;return;}
            if(InventoryPanel.AnyOpen||Presentation.WorldMapPanel.AnyOpen)return;
            var style=new GUIStyle(GUI.skin.box){fontSize=18,wordWrap=true};
            if(IsHarvesting)
            {
                GUI.Box(new Rect(Screen.width/2-200,Screen.height-195,400,50),L.Format("Recolectando: {0} · {1:0}%",L.Text(harvesting.definition.displayName),Mathf.Clamp01(progress/harvesting.definition.harvestSeconds)*100),style);
            }
            else if(nearby!=null||target!=null)GUI.Box(new Rect(Screen.width/2-200,Screen.height-195,400,50),nearby!=null?L.Text("[G] Fabricar en el banco"):L.Format("[G] {0} · {1}",L.Text(target.definition.actionName),L.Text(target.definition.displayName)),style);
        }
        void DrawStation()
        {
            Presentation.PlayerHUD.Fill(new Rect(0,0,Screen.width,Screen.height),new Color(.015f,.022f,.03f,.96f));
            var old=GUI.matrix;float scale=Mathf.Min(Screen.width/1000f,Screen.height/720f);GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1000*scale)/2,(Screen.height-720*scale)/2),Quaternion.identity,Vector3.one*scale);
            var previousBackground=GUI.backgroundColor;GUI.backgroundColor=new Color(.14f,.18f,.20f);
            var label=new GUIStyle(GUI.skin.label){fontSize=18,wordWrap=true};var button=new GUIStyle(GUI.skin.button){fontSize=18,wordWrap=true};
            button.normal.background=Texture2D.whiteTexture;button.hover.background=Texture2D.whiteTexture;button.active.background=Texture2D.whiteTexture;
            button.normal.textColor=Color.white;button.hover.textColor=Presentation.PlayerHUD.Gold;
            label.normal.textColor=Presentation.PlayerHUD.Gold;label.fontSize=26;
            GUI.Label(new Rect(50,42,700,40),L.Text("BANCO DE TRABAJO"),label);
            label.normal.textColor=Color.white;label.fontSize=18;
            if(GameAudio.Button(new Rect(780,42,160,36),L.Text("Cerrar [ESC]"),button)){Close();GUI.matrix=old;GUI.backgroundColor=previousBackground;return;}
            if(GameAudio.Button(new Rect(50,92,890,40),L.Format("Arma a mejorar: {0} · Ranura {1}",inventory.ItemName(inventory.EquippedId(slot)),slot+1),button))slot=1-slot;
            string[] tabs={"Preparación","Mejoras","Equipo básico"};
            for(int tab=0;tab<tabs.Length;tab++)
            {GUI.backgroundColor=tab==category?Presentation.PlayerHUD.Gold:new Color(.14f,.18f,.20f);
                if(GameAudio.Button(new Rect(50+tab*300,145,290,35),L.Text(tabs[tab]),button)){category=tab;scroll=Vector2.zero;}}
            GUI.backgroundColor=new Color(.14f,.18f,.20f);
            int visible=0;if(station.recipes!=null)foreach(var r in station.recipes)if(r!=null&&(int)r.Category==category)visible++;
            scroll=GUI.BeginScrollView(new Rect(50,190,900,410),scroll,new Rect(0,0,875,visible*200));int row=0;
            if(station.recipes!=null)for(int i=0;i<station.recipes.Length;i++)
            {
                var recipe=station.recipes[i];if(recipe==null||(int)recipe.Category!=category)continue;float y=row++*200;
                Presentation.PlayerHUD.Fill(new Rect(0,y,865,188),new Color(.055f,.073f,.08f,.95f));
                GUI.Label(new Rect(15,y+8,630,28),L.Text(recipe.displayName),label);
                GUI.Label(new Rect(15,y+38,825,46),L.Text(recipe.description),label);
                string cost="";foreach(var item in recipe.ingredients)if(item?.material!=null)cost+=(cost.Length>0?" · ":"")+L.Text(item.material.displayName)+" "+inventory.MaterialCount(item.material.id)+"/"+item.quantity;
                GUI.Label(new Rect(15,y+86,825,40),cost,label);
                string effect=recipe.upgradeWeapon?L.Format("Mejora del arma elegida: T{0} → T{1}",recipe.fromTier,recipe.toTier):L.Format("Produce {0} × {1}",recipe.quantity,L.Text(recipe.weaponResult!=null?recipe.weaponResult.DisplayName:recipe.result?.displayName));
                if(recipe.upgradeWeapon)
                {
                    var current=inventory.Item(inventory.EquippedId(slot));
                    if(current!=null&&current.tier==recipe.fromTier)
                    {
                        var after=current.Copy();after.tier=recipe.toTier;
                        var a=inventory.Rules.Bonuses(current);var b=inventory.Rules.Bonuses(after);
                        effect+=L.Format(" · Daño {0:+0.#;-0.#;0}% · Vel. {1:+0.#;-0.#;0}% · Vida {2:+0.#;-0.#;0} · Armadura {3:+0.#;-0.#;0}",(b.damage-a.damage)*100,(b.speed-a.speed)*100,b.life-a.life,b.armor-a.armor);
                    }
                }
                label.fontSize=16;GUI.Label(new Rect(15,y+131,630,48),effect,label);label.fontSize=18;
                bool enabled=GUI.enabled;GUI.enabled=inventory.CanCraft(recipe,inventory.EquippedId(slot));
                if(GameAudio.Button(new Rect(665,y+132,185,40),L.Text(recipe.upgradeWeapon?"Mejorar":"Crear"),button))inventory.TryCraft(recipe,inventory.EquippedId(slot),station);
                GUI.enabled=enabled;
            }
            GUI.EndScrollView();GUI.Label(new Rect(50,620,890,55),L.Text(inventory.Notice),label);GUI.matrix=old;GUI.backgroundColor=previousBackground;
        }
    }
}
