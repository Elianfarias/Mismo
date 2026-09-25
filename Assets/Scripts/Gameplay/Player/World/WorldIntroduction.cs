using System;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Equipment.Inventory;
using Mismo.Gameplay.Player.Presentation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace Mismo.Gameplay.Player.World
{
    /// <summary>The opening belongs to the persistent continent, including its inventory and respawn.</summary>
    public sealed class WorldIntroduction:MonoBehaviour
    {
        public Transform Geometry {get;private set;}
        public WorldIntroductionLayout Layout {get;private set;}
        public int Stage=>stage;
        public bool Complete=>Layout!=null&&stage>=5+Layout.Definition.encounters.Length;
        PlayerController player;
        TutorialGuide guide;
        ExplorationChunks world;
        EquipmentLoadout equipment;
        Health health;
        Health[] opponents;
        GameObject encounter;
        int stage,lessons;
        bool accepted;
        string saveError;
        float nextSaveAttempt;
        WorldIntroductionDefinition Data=>Layout.Definition;
        int EncounterIndex=>stage-4;
        bool CombatStage=>EncounterIndex>=0&&EncounterIndex<Data.encounters.Length;
        bool WaitingForLessonWeapon=>CombatStage&&(lessons&(1<<stage))==0&&Data.encounters[EncounterIndex].lessonWeapon!=null&&equipment.ActiveDefinition!=Data.encounters[EncounterIndex].lessonWeapon;
        TutorialSequence Lesson=>stage==0?Data.arrival:stage==1?Data.sprint:stage==3?Data.story:
            CombatStage?Data.encounters[EncounterIndex].lesson:stage==4+Data.encounters.Length?Data.village:null;
        public Vector3 Destination=>stage<=0?Layout.Spawn:stage==1?Layout.Cave+Layout.CaveRotation*new Vector3(-8,.2f,8):
            stage==2?Layout.Exit:stage==3?Layout.Grove:CombatStage?Layout.Encounter(EncounterIndex):Layout.Arrival;

        public void Initialize(ExplorationChunks chunks,WorldIntroductionLayout layout,PlayerController target)
        {
            world=chunks;Layout=layout;player=target;health=target.GetComponent<Health>();equipment=target.GetComponent<EquipmentLoadout>();
            stage=Mathf.Clamp(WorldSession.Current.introductionStage,0,5+Data.encounters.Length);
            lessons=WorldSession.Current.introductionLessons;
            guide=target.GetComponent<TutorialGuide>()??target.gameObject.AddComponent<TutorialGuide>();guide.Closed+=Closed;
            Geometry=new GameObject("Introduction geometry").transform;Geometry.SetParent(transform,false);
            Instantiate(Data.cave,Layout.Cave,Layout.CaveRotation,Geometry);
            Instantiate(Data.grove,Layout.Grove,Layout.Facing,Geometry);
            var narratorPosition=Layout.Grove;narratorPosition.y=Layout.Village.y;
            var actor=Instantiate(Data.narrator,narratorPosition,Layout.Facing*Quaternion.Euler(0,180,0),transform);
            actor.name="Liria · Guardiana del claro";
            if(actor.GetComponent<Quests.NpcGrounding>()==null)actor.AddComponent<Quests.NpcGrounding>();
            var narrator=actor.AddComponent<WorldIntroductionNarrator>();narrator.Initialize(this);
        }
        public bool Talk()
        {
            if(stage<3||health.IsDead)return false;
            return guide.TryBegin(Data.story,true);
        }
        void Closed(TutorialSequence sequence,bool skipped)
        {
            if(!isActiveAndEnabled||WorldSession.Current==null||sequence!=Lesson)return;
            if(stage==3){accepted=!skipped;return;}
            // Persist on the next update, after pause has released its input frame.
            lessons|=1<<stage;
        }
        void Update()
        {
            if(Layout==null||Complete||health==null||health.IsDead||!world.NavigationReady||
                GameplayPause.BlocksInput||InventoryPanel.AnyOpen||WorldMapPanel.BlocksGameplay)return;
            if(Time.unscaledTime<nextSaveAttempt)return;
            if(WorldSession.Current.introductionLessons!=lessons&&!Save(stage,null))return;
            if(stage==3)
            {
                if(accepted)Advance(Layout.Grove+Layout.Facing*new Vector3(0,.35f,-5));
                return; // F talks to the real NPC; merely entering the grove does not accept the call.
            }
            if(Keyboard.current?.hKey.wasPressedThisFrame==true&&Lesson!=null){guide.TryBegin(Lesson,true);return;}
            if(encounter==null&&Vector2.Distance(new Vector2(player.transform.position.x,player.transform.position.z),new Vector2(Destination.x,Destination.z))>7)return;
            if(WaitingForLessonWeapon)return;
            if(Lesson!=null&&(lessons&(1<<stage))==0){guide.TryBegin(Lesson,true);return;}
            if(!CombatStage){Advance(stage==2?Layout.Exit+Vector3.up*.3f:Destination+Vector3.up*.1f);return;}
            var current=Data.encounters[EncounterIndex];
            if(current.requiredWeapon!=null&&equipment.ActiveDefinition!=current.requiredWeapon)return;
            if(current.prefab==null){Advance(null);return;}
            if(encounter==null)
            {
                // Never enable an agent before the streamed terrain has usable navigation at its feet.
                var positions=new Vector3[Mathf.Max(1,current.count)];
                for(int i=0;i<positions.Length;i++)
                {
                    var p=Destination+Layout.Facing*new Vector3(positions.Length==1?0:i==0?-3:3,0,5);
                    if(!NavMesh.SamplePosition(p,out var hit,2,NavMesh.AllAreas)||Mathf.Abs(hit.position.y-p.y)>1)return;
                    positions[i]=hit.position;
                }
                encounter=new GameObject("Introduction encounter "+EncounterIndex);encounter.transform.SetParent(transform,false);
                for(int i=0;i<positions.Length;i++)Instantiate(current.prefab,positions[i],Layout.Facing*Quaternion.Euler(0,180,0),encounter.transform);
                opponents=encounter.GetComponentsInChildren<Health>();
            }
            if(opponents.Length>0&&Array.TrueForAll(opponents,h=>h==null||h.IsDead))Advance(Destination+Vector3.up*.15f);
        }
        bool Save(int next,Vector3? respawn)
        {
            if(WorldSession.SaveIntroduction(next,lessons,respawn,player.transform.position,player.transform.eulerAngles.y)){saveError=null;return true;}
            saveError="No se pudo guardar el avance. Reintentando…";nextSaveAttempt=Time.unscaledTime+3;return false;
        }
        void Advance(Vector3? respawn)
        {
            if(!Save(stage+1,respawn))return;
            // Corpses may finish awarding loot; don't destroy their pending reward components.
            encounter=null;opponents=null;stage++;accepted=false;
        }
        void OnDisable(){if(guide!=null)guide.Closed-=Closed;}
        string Title=>stage==0?"Despertar entre cristales":stage==1?"Seguí la luz de los cristales":stage==2?"La salida de la cueva":
            stage==3?"Una voz en EnchantedGrove":CombatStage?Data.encounters[EncounterIndex].title:"El primer pueblo";
        string Instruction=>stage==0?"WASD · Moverte     Mouse · Mirar":stage==1?"Mantené Shift mientras avanzás para correr. Cuidá tu estamina.":
            stage==2?"Seguí el túnel hasta la luz del día.":stage==3?"Buscá a Liria junto al refugio. Acercate y presioná F para conversar.":
            WaitingForLessonWeapon?"Presioná Tab para equipar el arco y aprender a tensar el disparo y usar su Focus.":CombatStage?Data.encounters[EncounterIndex].instruction:"Llegá a la entrada del pueblo y conocé a sus habitantes.";
        void OnGUI()
        {
            if(Layout==null||Complete||guide==null||guide.IsOpen||GameplayPause.IsPaused||InventoryPanel.AnyOpen||WorldMapPanel.AnyOpen)return;
            GUI.depth=0;var matrix=GUI.matrix;float scale=PlayerHUD.Scale;GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);
            float width=Screen.width/scale;
            var panel=new Rect(width-410,34,386,155);PlayerHUD.Fill(panel,new Color(.035f,.05f,.06f,.9f));
            QuietFantasyUI.Text(new Rect(panel.x+16,panel.y+10,354,28),Title,21,PlayerHUD.Gold);
            QuietFantasyUI.Text(new Rect(panel.x+16,panel.y+43,354,76),saveError??Instruction,17);
            float distance=Vector2.Distance(new Vector2(player.transform.position.x,player.transform.position.z),new Vector2(Destination.x,Destination.z));
            QuietFantasyUI.Text(new Rect(panel.x+16,panel.y+119,354,24),Mathf.CeilToInt(distance)+(stage==3?" m · F: conversar":" m · H: repasar explicación"),14,PlayerHUD.Gold);
            var camera=UnityEngine.Camera.main;
            if(camera!=null)
            {
                var marker=camera.WorldToScreenPoint(Destination+Vector3.up*2.8f);
                if(marker.z>0)QuietFantasyUI.Text(new Rect(marker.x/scale-90,(Screen.height-marker.y)/scale-20,180,30),"◆ "+Mathf.CeilToInt(distance)+" m",20,PlayerHUD.Gold,false,TextAnchor.MiddleCenter);
            }
            GUI.matrix=matrix;
        }
    }
}
