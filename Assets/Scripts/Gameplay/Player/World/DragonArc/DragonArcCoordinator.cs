using System.Collections.Generic;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.Equipment.Inventory;
using Mismo.Gameplay.Player.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Mismo.Gameplay.Player.World
{
    public readonly struct DragonArcMarker
    {
        public readonly Vector3 Position;
        public readonly string Label;
        public readonly bool Complete;
        public DragonArcMarker(Vector3 p,string label,bool complete=false){Position=p;Label=label;Complete=complete;}
    }
    [DisallowMultipleComponent]
    public sealed class DragonArcCoordinator : MonoBehaviour
    {
        public DragonArcDefinition Data {get;private set;}
        public DragonArcLayout Layout {get;private set;}
        public IDragonEncounter Encounter=>encounter is Object value && value!=null?encounter:null;
        public bool DialogueOpen=>dialogue;
        public bool ArrivalSeen=>inventory?.QuestState(Data?.audience)!=null;
        public bool AudienceComplete=>inventory?.QuestState(Data?.audience)?.completed==true;
        public bool WarningHeard=>Data!=null && inventory!=null && inventory.QuestCount(Data.audience,Data.audience.objectives[0])>0;
        public bool CanSummon=>inventory!=null && inventory.CanSummonDragon(Data);
        public int LitCount=>(IsLit(0)?1:0)+(IsLit(1)?1:0)+(IsLit(2)?1:0);
        PlayerInventory inventory;
        Health health;
        WorldIntroduction introduction;
        ExplorationChunks world;
        Transform sites;
        DragonArrivalSequence arrival;
        IDragonEncounter encounter;
        GameObject encounterRoot;
        bool arrivalPending,dialogue;
        float retryAt;
        string speaker,body,button,error;
        DragonArcRole role;
        int openedFrame;
        Vector2 scroll;
        public void Initialize(ExplorationChunks chunks,WorldIntroduction tutorial,DragonArcLayout layout,Transform owner)
        {
            world=chunks;introduction=tutorial;Layout=layout;Data=layout.Definition;
            inventory=GetComponent<PlayerInventory>();health=GetComponent<Health>();
            arrival=GetComponent<DragonArrivalSequence>()??gameObject.AddComponent<DragonArrivalSequence>();
            sites=new GameObject("Dragon story sites").transform;sites.SetParent(owner,false);
            AddSite(Data.lookout,Layout.Lookout,DragonArcRole.Lookout,"Conversar con el vigía",tutorial.Layout.Facing);
            AddSite(Data.king,Layout.King,DragonArcRole.King,"Hablar con el rey",tutorial.Layout.Facing*Quaternion.Euler(0,180,0));
            for(int i=0;i<3;i++)AddSite(Data.beacon,Layout.Beacons[i],DragonArcRole.Forest+i,Data.beaconNames[i],Quaternion.identity);
            AddSite(Data.altar,Layout.Altar,DragonArcRole.Altar,"Altar de la llamada",Quaternion.identity);
        }
        void AddSite(GameObject prefab,Vector3 position,DragonArcRole siteRole,string label,Quaternion rotation)
        {
            var actor=prefab!=null?Instantiate(prefab,position,rotation,sites):new GameObject(label);
            actor.name=label;actor.transform.SetParent(sites,true);actor.transform.position=position;
            actor.AddComponent<DragonArcInteractable>().Initialize(this,siteRole,label);
        }
        public bool IsLit(int i)=>Data!=null && inventory!=null && i>=0 && i<3 && inventory.QuestCount(Data.awakening,Data.awakening.objectives[i])>0;
        void Update()
        {
            if(Data==null || inventory==null || !inventory.IsReady)return;
            if(health==null || health.IsDead){Close();return;}
            if(encounterRoot!=null && Encounter?.IsActive!=true){Destroy(encounterRoot);encounterRoot=null;encounter=null;}
            if(dialogue)
            {
                if(Time.frameCount==openedFrame)return;
                if(Keyboard.current?.escapeKey.wasPressedThisFrame==true)Close();
                else if(Keyboard.current?.enterKey.wasPressedThisFrame==true)Confirm();
                return;
            }
            if(arrivalPending && Time.unscaledTime>=retryAt)
            {
                if(inventory.TryAdvanceDragonArc(Data,DragonArcStep.Arrival)){arrivalPending=false;error=null;}
                else {error="No se pudo guardar la llegada. Reintentando…";retryAt=Time.unscaledTime+3;}
            }
            if(!ArrivalSeen && !arrivalPending && !arrival.Running && introduction.Complete && world.NavigationReady && inventory.CanManage &&
                !InventoryPanel.AnyOpen && !WorldMapPanel.BlocksGameplay && Vector3.Distance(transform.position,introduction.Layout.Arrival)<22)
                arrival.Begin(Data,introduction.Layout.Arrival,introduction.Layout.Facing,()=>arrivalPending=true);
            if(encounter?.AtPhaseBoundary==true)
                PlayerInteraction.Offer(inventory,this,transform.position,"Finalizar prueba de fase 1","",ReturnToAltar);
        }
        public bool Interact(DragonArcRole siteRole)
        {
            if(!isActiveAndEnabled || !ArrivalSeen || dialogue || arrival.Running || !inventory.CanManage || GameplayPause.BlocksInput)return false;
            role=siteRole;error=null;button="Entendido";
            switch(siteRole)
            {
                case DragonArcRole.Lookout:
                    speaker="Vigía del pueblo";body=Data.warning;button=WarningHeard?"Volver al camino":"Hablaré con el rey";break;
                case DragonArcRole.King:
                    speaker="El rey";
                    body=!WarningHeard?"El vigía te espera junto a la entrada. Averiguá qué vio antes de continuar.":Data.audienceText;
                    button=!WarningHeard||AudienceComplete?"Volver al camino":"Reactivaré los tres faros";break;
                case DragonArcRole.Altar:
                    speaker="Altar de la llamada";
                    body=encounter?.IsActive==true?"Soul Eater ya respondió a la llamada.":!CanSummon?
                        "Los faros todavía no responden al unísono. Reactivá los tres antes de comenzar el ritual.\n\nFaros activos: "+LitCount+" / 3.":
                        "Los tres faros están activos. El ritual llamará a Soul Eater a este claro.\n\nSi caés, podrás volver a intentarlo desde las inmediaciones del altar. Los faros permanecerán activos.\n\n¿Estás listo para invocar al dragón?";
                    button=CanSummon && encounter?.IsActive!=true?"Invocar a Soul Eater":"Volver";break;
                default:
                    int i=(int)siteRole-(int)DragonArcRole.Forest;speaker=Data.beaconNames[i];
                    body=!AudienceComplete?"El rey conoce el antiguo ritual. Hablá con él en el pueblo.":IsLit(i)?"Este faro ya respondió a tu llamada.":"Al reactivar este faro, su señal se unirá a la de los otros dos. Podés activarlos en cualquier orden.";
                    button=AudienceComplete&&!IsLit(i)?"Activar faro":"Volver";break;
            }
            if(!GameplayPause.TryPause(this))return false;
            dialogue=true;openedFrame=Time.frameCount;scroll=Vector2.zero;return true;
        }
        public bool Confirm()
        {
            if(!dialogue)return false;
            bool saved=true;
            if(role==DragonArcRole.Lookout && !WarningHeard)saved=inventory.TryAdvanceDragonArc(Data,DragonArcStep.Warning);
            else if(role==DragonArcRole.King && WarningHeard && !AudienceComplete)saved=inventory.TryAdvanceDragonArc(Data,DragonArcStep.Audience);
            else if(role>=DragonArcRole.Forest && role<=DragonArcRole.Gorge && AudienceComplete)
            {
                int i=(int)role-(int)DragonArcRole.Forest;
                if(!IsLit(i))saved=inventory.TryAdvanceDragonArc(Data,DragonArcStep.Forest+i);
            }
            else if(role==DragonArcRole.Altar && CanSummon && encounter?.IsActive!=true)saved=TrySummon();
            if(saved){Close();return true;}
            error=inventory.HasSaveProblem?"No se pudo guardar el avance. Revisá el guardado y volvé a intentarlo.":error??"No se pudo completar la acción. Volvé a intentarlo.";
            return false;
        }
        public bool TrySummon()
        {
            // Revalidate at confirmation, not only when displaying the prompt.
            if(!CanSummon || !inventory.CanManage || encounter?.IsActive==true || Vector3.Distance(transform.position,Layout.Altar)>5 || !world.NavigationReady)return false;
            var prototype=Data.encounterPrefab!=null?Data.encounterPrefab.GetComponent<IDragonEncounter>():null;
            if(prototype?.Ready!=true){error="El ritual no está disponible.";return false;}
            if(!WorldSession.SaveIntroduction(WorldSession.Current.introductionStage,WorldSession.Current.introductionLessons,Layout.Retry+Vector3.up*.3f,transform.position,transform.eulerAngles.y))
            {error="No se pudo guardar el punto de retorno. El ritual no comenzó.";return false;}
            var root=new GameObject("Soul Eater · summoned encounter");root.SetActive(false);root.transform.SetParent(sites,false);
            var actor=Instantiate(Data.encounterPrefab,root.transform);var next=actor.GetComponent<IDragonEncounter>();
            next.Initialize(transform,Layout.Arena,Data.arenaRadius);
            if(inventory.QuestState(Data.awakening)?.completed!=true && !inventory.TryAdvanceDragonArc(Data,DragonArcStep.Summoned))
            {Destroy(root);return false;}
            encounterRoot=root;encounter=next;root.SetActive(true);return true;
        }
        public void ReturnToAltar()
        {
            Encounter?.Abort();if(encounterRoot!=null)Destroy(encounterRoot);encounterRoot=null;encounter=null;
            GetComponent<Movement.PlayerMotor>()?.ResetPosition(Layout.Retry+Vector3.up*.3f);
            WorldSession.Checkpoint(transform.position,transform.eulerAngles.y);
        }
        public void Close(){if(!dialogue)return;dialogue=false;GameplayPause.Resume(this);}
        void OnEnable(){if(arrival!=null)arrival.enabled=true;}
        void OnDisable(){Close();if(arrival!=null)arrival.enabled=false;Encounter?.Abort();encounter=null;}
        public IEnumerable<DragonArcMarker> Markers()
        {
            if(Data==null || !ArrivalSeen)yield break;
            if(!WarningHeard){yield return new DragonArcMarker(Layout.Lookout,"Conversá con el vigía");yield break;}
            if(!AudienceComplete){yield return new DragonArcMarker(Layout.King,"Hablá con el rey");yield break;}
            for(int i=0;i<3;i++)yield return new DragonArcMarker(Layout.Beacons[i],Data.beaconNames[i],IsLit(i));
            yield return new DragonArcMarker(Layout.Altar,CanSummon?"Invocar a Soul Eater":"Altar · "+LitCount+" / 3 faros");
        }
        void OnGUI()
        {
            if(Data==null || GameplayPause.InterfaceHidden)return;
            var matrix=GUI.matrix;float scale=PlayerHUD.Scale;GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);
            try
            {
                float width=Screen.width/scale,height=Screen.height/scale;
                if(dialogue)
                {
                    GUI.depth=-480;PlayerHUD.Fill(new Rect(0,0,width,height),new Color(0,0,0,.6f));
                    var card=new Rect((width-Mathf.Min(650,width-40))/2,(height-425)/2,Mathf.Min(650,width-40),425);
                    PlayerHUD.Fill(card,new Color(.035f,.052f,.06f,.99f));QuietFantasyUI.Border(card,PlayerHUD.Gold,1);
                    QuietFantasyUI.Text(new Rect(card.x+26,card.y+20,card.width-52,42),speaker,27,PlayerHUD.Gold);
                    var style=new GUIStyle(GUI.skin.label){wordWrap=true,fontSize=20,font=QuietFantasyUI.Body};style.normal.textColor=Color.white;
                    float textHeight=style.CalcHeight(new GUIContent(body),card.width-74);
                    scroll=GUI.BeginScrollView(new Rect(card.x+26,card.y+80,card.width-52,225),scroll,new Rect(0,0,card.width-74,textHeight));
                    GUI.Label(new Rect(0,0,card.width-74,textHeight),body,style);GUI.EndScrollView();
                    if(error!=null)QuietFantasyUI.Text(new Rect(card.x+26,card.y+310,card.width-52,44),error,15,new Color(1,.6f,.4f));
                    if(FantasyUI.Button(new Rect(card.x+26,card.yMax-56,110,36),"Salir · Esc"))Close();
                    if(FantasyUI.Button(new Rect(card.xMax-360,card.yMax-56,334,36),button))Confirm();
                    return;
                }
                if(GameplayPause.BlocksInput || InventoryPanel.AnyOpen || WorldMapPanel.AnyOpen)return;
                var camera=UnityEngine.Camera.main;if(camera==null)return;
                foreach(var marker in Markers())
                {
                    float distance=Vector3.Distance(transform.position,marker.Position);
                    if(marker.Complete && distance>25)continue;
                    var p=camera.WorldToScreenPoint(marker.Position+Vector3.up*3);if(p.z<=0)continue;
                    float x=p.x/scale,y=(Screen.height-p.y)/scale;if(x<0||x>width||y<30||y>height-130)continue;
                    string label=(marker.Complete?"✓ ":"◆ ")+marker.Label+" · "+Mathf.CeilToInt(distance)+" m";
                    var r=new Rect(x-155,y-14,310,32);PlayerHUD.Fill(r,new Color(.025f,.04f,.05f,.78f));
                    QuietFantasyUI.Text(r,label,16,marker.Complete?new Color(.5f,1,.65f):PlayerHUD.Gold,false,TextAnchor.MiddleCenter);
                }
                if(encounter?.AtPhaseBoundary==true)
                    QuietFantasyUI.Text(new Rect(width/2-290,72,580,70),"Fase 1 completada · La fase 2 está pendiente.\nF · Volver al altar",20,PlayerHUD.Gold,false,TextAnchor.MiddleCenter);
                else if(error!=null)QuietFantasyUI.Text(new Rect(width/2-250,72,500,65),error,18,PlayerHUD.Gold);
            }
            finally{GUI.matrix=matrix;}
        }
    }
}
