using System;
using System.Linq;
using Mismo.Gameplay.Player.Equipment.Inventory;
using UnityEngine;
using Mismo.Gameplay.Player.Presentation;
namespace Mismo.Gameplay.Player.Quests
{
    public sealed class QuestGiver:QuestInteractable
    {
        public QuestNpcDefinition npc;
        public QuestDefinition[] quests=Array.Empty<QuestDefinition>();
        [Tooltip("Pedidos que solo se atienden cuando ya los habilitó su evento. No se ofrecen antes de que exista ese contenido.")]
        public QuestDefinition[] eventQuests=Array.Empty<QuestDefinition>();
        public VillageNpcSettings markerSettings;
        PlayerInventory markerPlayer;
        UnityEngine.Camera markerCamera;
        GUIStyle markerStyle,nameStyle;
        public bool CanTalk(PlayerInventory player,QuestDefinition quest)=>isActiveAndEnabled&&npc!=null&&InRange(player)&&player.CanManage&&VisibleFrom(player)&&(quest==null||quest.npc==npc&&Array.IndexOf(quests,quest)>=0&&player.Quests?.Contains(quest)==true&&(player.QuestState(quest)!=null||Array.IndexOf(eventQuests,quest)<0&&player.CanAcceptQuest(quest)));
        bool VisibleFrom(PlayerInventory player)
        {
            if(markerSettings==null||!markerSettings.requireLineOfSight)return true;
            var from=player.transform.position+Vector3.up*1.2f;var to=transform.TransformPoint(Vector3.up*1.2f);
            foreach(var hit in Physics.RaycastAll(from,to-from,Vector3.Distance(from,to),~0,QueryTriggerInteraction.Ignore))
                if(!hit.transform.IsChildOf(transform)&&!hit.transform.IsChildOf(player.transform))return false;
            return true;
        }
        public QuestDefinition OfferedQuest(PlayerInventory player)=>quests.FirstOrDefault(q=>q!=null&&Array.IndexOf(eventQuests,q)<0&&player.CanAcceptQuest(q));
        public string Marker(PlayerInventory player)
        {
            if(player==null||!player.IsReady)return "";
            if(quests.Any(q=>q!=null&&player.QuestReady(q)))return "?";
            if(OfferedQuest(player)!=null)return "!";
            return markerSettings!=null&&markerSettings.showActiveMarker&&quests.Any(q=>q!=null&&player.QuestState(q)!=null&&!player.QuestState(q).completed)?"…":"";
        }
        public override bool Interact(PlayerInventory player)
        {
            if(!InRange(player)||npc==null)return false;
            var quest=quests.FirstOrDefault(q=>q!=null&&player.QuestReady(q))??OfferedQuest(player)??quests.FirstOrDefault(q=>q!=null&&player.QuestState(q)!=null&&!player.QuestState(q).completed);
            return player.GetComponent<InventoryPanel>()?.OpenQuestContact(quest,this)==true;
        }
        void OnGUI()
        {
            if(markerSettings==null||npc==null||InventoryPanel.AnyOpen||Presentation.WorldMapPanel.AnyOpen||GameplayPause.BlocksInput)return;
            if(markerPlayer==null)markerPlayer=FindAnyObjectByType<PlayerInventory>();
            if(markerCamera==null)markerCamera=UnityEngine.Camera.main;
            if(markerPlayer==null||markerCamera==null||Vector3.Distance(markerPlayer.transform.position,transform.position)>markerSettings.markerDistance)return;
            var anchor=transform.TransformPoint(Vector3.up*markerSettings.markerHeight);
            var screen=markerCamera.WorldToScreenPoint(anchor);if(screen.z<=0)return;
            var cameraPosition=markerCamera.transform.position;
            foreach(var hit in Physics.RaycastAll(cameraPosition,anchor-cameraPosition,Vector3.Distance(cameraPosition,anchor),~0,QueryTriggerInteraction.Ignore))
                if(!hit.transform.IsChildOf(transform)&&!hit.transform.IsChildOf(markerPlayer.transform))return;
            string marker=Marker(markerPlayer);float scale=Mathf.Clamp(Screen.height/900f,.7f,1.6f);
            if(markerStyle==null){markerStyle=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold};nameStyle=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold};}
            var old=GUI.color;markerStyle.fontSize=Mathf.RoundToInt(markerSettings.markerSize*scale);
            var rect=new Rect(screen.x-40*scale,Screen.height-screen.y-48*scale,80*scale,56*scale);
            if(marker.Length>0){markerStyle.normal.textColor=Color.black;GUI.Label(new Rect(rect.x+2,rect.y+2,rect.width,rect.height),marker,markerStyle);markerStyle.normal.textColor=marker=="?"?markerSettings.readyColor:marker=="!"?markerSettings.availableColor:Color.white;GUI.Label(rect,marker,markerStyle);}
            if(markerSettings.showNames&&InRange(markerPlayer)){nameStyle.fontSize=Mathf.RoundToInt(15*scale);nameStyle.normal.textColor=Color.black;var nameRect=new Rect(screen.x-130*scale,rect.yMax,260*scale,25*scale);GUI.Label(new Rect(nameRect.x+1,nameRect.y+1,nameRect.width,nameRect.height),npc.displayName,nameStyle);nameStyle.normal.textColor=Color.white;GUI.Label(nameRect,npc.displayName,nameStyle);}
            GUI.color=old;
        }
    }
}
