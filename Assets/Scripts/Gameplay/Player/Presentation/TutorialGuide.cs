using System;
using System.Collections.Generic;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Equipment.Inventory;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Mismo.Gameplay.Player.Presentation
{
    [DisallowMultipleComponent, DefaultExecutionOrder(-200)]
    public sealed class TutorialGuide : MonoBehaviour
    {
        [SerializeField] TutorialSequence onStart;
        readonly HashSet<string> dismissed = new HashSet<string>();
        TutorialSequence current;
        PlayerHUD hud;
        Health health;
        EquipmentLoadout equipment;
        int page, changedFrame;
        bool startPending;
        GUIStyle bodyStyle;
        Vector2 scroll;
        public bool IsOpen => current != null;
        public int PageIndex => page;
        public TutorialSequence Current => current;
        // Dismissed means read OR skipped; it never claims the player mastered a mechanic.
        public event Action<TutorialSequence, bool> Closed;
        public bool WasDismissed(TutorialSequence sequence) => sequence != null && dismissed.Contains(sequence.id);

        void Start()
        {
            hud=GetComponent<PlayerHUD>();health=GetComponent<Health>();equipment=GetComponent<EquipmentLoadout>();
            startPending=onStart!=null;
        }

        public bool TryBegin(TutorialSequence sequence, bool replay=false)
        {
            if(!isActiveAndEnabled||sequence==null||!sequence.IsValid||IsOpen||!replay&&WasDismissed(sequence))return false;
            if(hud==null)hud=GetComponent<PlayerHUD>();
            if(health==null)health=GetComponent<Health>();
            if(equipment==null)equipment=GetComponent<EquipmentLoadout>();
            if(hud==null||health==null||health.IsDead||GameplayPause.BlocksInput||PlayerHUD.UIEditMode||
                InventoryPanel.AnyOpen||WorldMapPanel.BlocksGameplay||GetComponent<World.GatheringPlayer>()?.BlocksGameplay==true)return false;
            if(!GameplayPause.TryPause(this))return false;
            current=sequence;page=0;changedFrame=Time.frameCount;scroll=Vector2.zero;
            return true;
        }

        void Update()
        {
            if(startPending && TryBegin(onStart))startPending=false;
            if(!IsOpen)return;
            if(health==null||health.IsDead){Close(false,false);return;}
            if(Time.frameCount==changedFrame)return;
            var keyboard=Keyboard.current;
            if(keyboard==null)return;
            if(keyboard.escapeKey.wasPressedThisFrame){Close(true);return;}
            if(keyboard.leftArrowKey.wasPressedThisFrame)Previous();
            else if(keyboard.enterKey.wasPressedThisFrame||keyboard.rightArrowKey.wasPressedThisFrame)Next();
        }

        public void Previous(){if(!IsOpen||page==0)return;page--;changedFrame=Time.frameCount;scroll=Vector2.zero;}
        public void Next()
        {
            if(!IsOpen)return;
            if(page+1>=current.pages.Length){Close(false);return;}
            page++;changedFrame=Time.frameCount;scroll=Vector2.zero;
        }
        public void Skip()=>Close(true);
        void Close(bool skipped,bool remember=true)
        {
            if(!IsOpen)return;
            var sequence=current;current=null;
            if(remember)dismissed.Add(sequence.id);
            GameplayPause.Resume(this);
            Closed?.Invoke(sequence,skipped);
        }
        void OnDisable(){startPending=false;Close(true,false);}

        public string ResolveText(string text)
        {
            if(equipment==null)equipment=GetComponent<EquipmentLoadout>();
            var q=equipment?.GetAbility(AbilitySlot.Q);
            return (text??"").Replace("{q}",q!=null?q.DisplayName:"habilidad Q")
                .Replace("{e}",equipment?.GetAbility(AbilitySlot.E)?.DisplayName??"habilidad E")
                .Replace("{r}",equipment?.GetAbility(AbilitySlot.R)?.DisplayName??"habilidad R")
                .Replace("{focusCost}",q!=null?q.focusCost.ToString("0.#"):"0")
                .Replace("{basicGain}",(equipment?.GetAbility(AbilitySlot.Basic)?.focusGainOnHit??0).ToString("0.#"));
        }

        public static Rect ClampHighlight(Rect target,Rect screen) => Rect.MinMaxRect(
            Mathf.Clamp(target.xMin,screen.xMin,screen.xMax),Mathf.Clamp(target.yMin,screen.yMin,screen.yMax),
            Mathf.Clamp(target.xMax,screen.xMin,screen.xMax),Mathf.Clamp(target.yMax,screen.yMin,screen.yMax));

        public static Rect CardRect(Rect screen,Rect hole,bool highlight,float cardHeight)
        {
            float width=Mathf.Min(540,screen.width-48),height=Mathf.Min(cardHeight,screen.height-48);
            var center=new Rect((screen.width-width)/2,(screen.height-height)/2,width,height);
            if(!highlight||!center.Overlaps(hole))return center;
            var candidates=new[]{new Rect(hole.xMax+24,center.y,width,height),new Rect(hole.xMin-width-24,center.y,width,height),
                new Rect(center.x,hole.yMax+24,width,height),new Rect(center.x,hole.yMin-height-24,width,height)};
            foreach(var candidate in candidates)
                if(candidate.x>=24&&candidate.y>=24&&candidate.xMax<=screen.width-24&&candidate.yMax<=screen.height-24)return candidate;
            // Extreme custom HUD layouts may leave no room. Keep the card on screen and let its body scroll.
            return center;
        }

        void OnGUI()
        {
            if(!IsOpen||hud==null)return;
            var matrix=GUI.matrix;var color=GUI.color;bool enabled=GUI.enabled;
            try
            {
                GUI.depth=-500;GUI.enabled=true;GUI.color=Color.white;
                float scale=PlayerHUD.Scale;
                GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);
                var screen=new Rect(0,0,Screen.width/scale,Screen.height/scale);
                var step=current.pages[page];
                bool highlight=hud.TryGetTutorialRect(step.highlight,out var pixels);
                var hole=ClampHighlight(new Rect(pixels.position/scale,pixels.size/scale),screen);
                var shade=new Color(.008f,.015f,.024f,.82f);
                if(highlight)
                {
                    PlayerHUD.Fill(new Rect(0,0,screen.width,hole.yMin),shade);
                    PlayerHUD.Fill(new Rect(0,hole.yMax,screen.width,screen.height-hole.yMax),shade);
                    PlayerHUD.Fill(new Rect(0,hole.yMin,hole.xMin,hole.height),shade);
                    PlayerHUD.Fill(new Rect(hole.xMax,hole.yMin,screen.width-hole.xMax,hole.height),shade);
                    QuietFantasyUI.Border(hole,PlayerHUD.Gold,2);
                }
                else PlayerHUD.Fill(screen,shade);
                if(bodyStyle==null)bodyStyle=new GUIStyle(GUI.skin.label){wordWrap=true,fontSize=21,padding=new RectOffset()};
                bodyStyle.font=QuietFantasyUI.Body;bodyStyle.normal.textColor=QuietFantasyUI.Ink;
                string body=ResolveText(step.body);
                float textHeight=bodyStyle.CalcHeight(new GUIContent(body),Mathf.Min(540,screen.width-48)-74);
                var card=CardRect(screen,hole,highlight,Mathf.Max(306,textHeight+188));
                PlayerHUD.Fill(card,new Color(.043f,.059f,.071f,.99f));
                QuietFantasyUI.Border(card,new Color(.64f,.52f,.30f),1);
                QuietFantasyUI.Text(new Rect(card.x+28,card.y+22,card.width-56,24),"GUÍA · "+(page+1)+" / "+current.pages.Length+" · EN PAUSA",14,PlayerHUD.Gold);
                QuietFantasyUI.Text(new Rect(card.x+28,card.y+57,card.width-56,40),step.title,28,QuietFantasyUI.Ink,true);
                var viewport=new Rect(card.x+28,card.y+110,card.width-56,card.height-188);
                scroll=GUI.BeginScrollView(viewport,scroll,new Rect(0,0,viewport.width-18,textHeight));
                GUI.Label(new Rect(0,0,viewport.width-18,textHeight),body,bodyStyle);GUI.EndScrollView();
                float y=card.yMax-58;
                if(FantasyUI.Button(new Rect(card.x+24,y,110,36),"Omitir · Esc")){Skip();return;}
                GUI.enabled=page>0;
                if(FantasyUI.Button(new Rect(card.xMax-290,y,115,36),"Anterior")){Previous();return;}
                GUI.enabled=true;
                if(FantasyUI.Button(new Rect(card.xMax-163,y,139,36),page+1==current.pages.Length?current.completionLabel:"Siguiente →")){Next();return;}
                // The transparent hole is visual only. Never send its clicks to the HUD below.
                if(Event.current.isMouse||Event.current.type==EventType.ScrollWheel)Event.current.Use();
            }
            // GUI.depth belongs to this OnGUI host. Restoring it here would sort the
            // overlay alongside the HUD and let later HUD hosts paint over its dimmer.
            finally{GUI.matrix=matrix;GUI.color=color;GUI.enabled=enabled;}
        }
    }
}
