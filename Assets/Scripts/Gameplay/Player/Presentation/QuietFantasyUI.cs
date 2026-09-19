using System.Collections.Generic;
using Mismo.Gameplay.Player.Equipment;
using UnityEngine;
using L = Mismo.Gameplay.Player.Localization.GameLanguage;

namespace Mismo.Gameplay.Player.Presentation
{
    /// <summary>Quiet fantasy menus: shared typography, monochrome icons and restrained focus states.</summary>
    public static class QuietFantasyUI
    {
        public static readonly Color Ink=new Color(.91f,.89f,.82f),Muted=new Color(.64f,.66f,.60f),Amber=new Color(.87f,.70f,.37f);
        public static readonly Color Surface=new Color(.055f,.067f,.058f,.64f),Rule=new Color(.63f,.65f,.57f,.24f);
        static Font heading,body;
        static GUIStyle words,action;
        static readonly Dictionary<string,Texture2D> icons=new Dictionary<string,Texture2D>();
        static Equipment.Inventory.InventoryUIIcons sharedIcons;
        public static Font Body=>body!=null?body:body=Resources.Load<Font>("Fonts/Cagliostro-Regular");
        public static Font Heading=>heading!=null?heading:heading=Resources.Load<Font>("Fonts/Cagliostro-Regular");
        public static void Text(Rect rect,string value,int size=20,Color? color=null,bool title=false,TextAnchor alignment=TextAnchor.UpperLeft)
        {
            if(words==null)words=new GUIStyle(GUI.skin.label){padding=new RectOffset(),wordWrap=true};
            words.font=title?Heading:Body;words.fontSize=size;words.fontStyle=FontStyle.Normal;words.alignment=alignment;
            words.normal.textColor=color??Ink;GUI.Label(rect,L.Text(value??""),words);
        }
        public static bool Button(Rect rect,string text,bool selected=false,bool title=false)
        {
            if(action==null)action=new GUIStyle(){fontSize=20,alignment=TextAnchor.MiddleCenter,padding=new RectOffset(5,5,2,2)};
            action.font=title?Heading:Body;action.fontSize=title?23:20;
            foreach(var state in new[]{action.normal,action.hover,action.active,action.focused}){state.background=null;state.textColor=Ink;}
            bool hover=rect.Contains(Event.current.mousePosition);
            if(selected||hover)PlayerHUD.Fill(rect,new Color(.7f,.62f,.39f,GUI.enabled?.10f:.035f));
            action.normal.textColor=selected?Amber:Ink;
            bool result=GameAudio.Button(rect,L.Text(text),action);
            if(selected||hover)PlayerHUD.Fill(new Rect(rect.x,rect.yMax-2,rect.width,selected?2:1),GUI.enabled?Amber:Muted);
            return result;
        }
        public static void Border(Rect rect,Color color,float thickness=1)
        {
            PlayerHUD.Fill(new Rect(rect.x,rect.y,rect.width,thickness),color);
            PlayerHUD.Fill(new Rect(rect.x,rect.yMax-thickness,rect.width,thickness),color);
            PlayerHUD.Fill(new Rect(rect.x,rect.y,thickness,rect.height),color);
            PlayerHUD.Fill(new Rect(rect.xMax-thickness,rect.y,thickness,rect.height),color);
        }
        public static Texture2D Icon(string name)
        {
            if(string.IsNullOrEmpty(name))return null;
            if(!icons.TryGetValue(name,out var icon)||icon==null)
            {
                if(sharedIcons==null)sharedIcons=Resources.Load<Equipment.Inventory.InventoryUIIcons>("InventoryUIIcons");
                if(sharedIcons!=null&&sharedIcons.abilityIcons!=null)
                    foreach(var texture in sharedIcons.abilityIcons)
                        if(texture!=null&&texture.name==name){icon=texture;break;}
                if(icon==null)icon=Resources.Load<Texture2D>("UI/QuietFantasy/Icons/"+name);
                if(icon!=null)icons[name]=icon;
            }
            return icon;
        }
        public static void DrawIcon(Rect rect,string name,Color? tint=null)
        {
            var icon=Icon(name);if(icon==null)return;
            var old=GUI.color;GUI.color=tint??Ink;GUI.DrawTexture(rect,icon,ScaleMode.ScaleToFit);GUI.color=old;
        }
        // Stable ability identifiers keep the visual vocabulary independent of translation and loadout order.
        public static Texture2D AbilityIcon(AbilityDefinition ability)
        {
            return ability!=null&&ability.icon!=null?ability.icon:Icon(DefaultAbilityIcon(ability));
        }
        public static void DrawIcon(Rect rect,Texture2D icon,Color? tint=null)
        {
            if(icon==null)return;
            var old=GUI.color;GUI.color=tint??Ink;GUI.DrawTexture(rect,icon,ScaleMode.ScaleToFit);GUI.color=old;
        }
        static string DefaultAbilityIcon(AbilityDefinition ability)
        {
            if(ability==null)return "locked-chest";
            switch(ability.Id)
            {
                case "DualFlurry":return "blade-fall";
                case "DualWhirlwind":case "SwordSpin":return "sword-spin";
                case "OpenWound":return "bleeding-wound";
                case "DualCross":return "crossed-swords";
                case "DualRhythm":return "sword-clash";
                case "DualFinisher":return "sword-wound";
                case "GuardBreaker":return "sword-clash";
                case "LowSweep":return "plain-dagger";
                case "FirmGuard":case "SwordParry":return "shield-reflect";
                case "SwordTip":case "SwordLunge":return "broadsword";
                case "TwoTimes":return "crossed-swords";
                case "SideStep":return "wingfoot";
                case "ShieldBash":return "shield-reflect";
                case "ShieldCharge":return "sprint";
                case "ShieldCoverage":return "shield-reflect";
                case "ThrownBuckler":return "sword-spin";
                case "ThirdArrow":return "arrow-cluster";
                case "PoisonArrow":return "poison-bottle";
                case "HunterTrap":return "wolf-trap";
                case "AxeBasic":return "broadsword";
                case "AxeBleedingCut":return "bleeding-wound";
                case "AxeArmorRend":return "checked-shield";
                case "AxeForwardSwing":return "sword-clash";
                case "AxeSplittingBlow":return "blade-fall";
                case "AxeCruelEdge":case "AxeExecutioner":return "dead-eye";
            }
            if(ability.pose==AbilityPose.Bow)return "target-arrows";
            if(ability.pose==AbilityPose.Spin)return "sword-spin";
            if(ability.pose==AbilityPose.Parry)return "shield-reflect";
            return ability.IsPassive?"dead-eye":"broadsword";
        }
    }
}
