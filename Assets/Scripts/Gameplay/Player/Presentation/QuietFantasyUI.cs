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
        public static readonly Color Surface=new Color(.047f,.071f,.082f,.86f),Rule=new Color(.88f,.90f,.88f,.7f);
        public static readonly Rect MenuWindow=new Rect(28,20,1224,760);
        public static readonly Rect MenuBackButton=new Rect(1090,38,52,47);
        public static readonly Rect MenuCloseButton=new Rect(1162,38,52,47);
        public static void MenuBackground(Mismo.Gameplay.Player.Equipment.Inventory.InventoryUIIcons icons)
        {
            float opacity=icons!=null?Mathf.Clamp01(icons.panelOpacity)*Mathf.Clamp01(icons.inventoryBackgroundOpacity):.7f;
            FantasyUI.Panel(MenuWindow,opacity);
        }
        public static void MenuDivider()=>Border(new Rect(64,94,1150,1),new Color(.78f,.70f,.51f,.35f));
        public static void MenuHeading(Texture2D icon,string caption)
        {
            DrawIcon(new Rect(66,45,30,34),MapIcons.Mask(icon));
            GUI.Label(new Rect(58,38,48,47),new GUIContent("",L.Text(caption)),GUIStyle.none);
        }
        static Font heading,body;
        static GUIStyle words,action;
        static GUISkin scrollSkin,scrollSource;
        static Texture2D scrollNormal,scrollHover;
        // Keep Unity's wheel, drag and track-click handling, with a wider invisible grab area.
        public static Vector2 BeginScrollView(Rect viewport,Vector2 position,Rect content)
        {
            var previous=GUI.skin;
            if(scrollSkin==null||scrollSource!=previous)
            {
                if(scrollSkin!=null)Object.Destroy(scrollSkin);
                scrollSource=previous;scrollSkin=Object.Instantiate(previous);
                scrollSkin.hideFlags=HideFlags.HideAndDontSave;
                if(scrollNormal==null)scrollNormal=ScrollThumb(6,.48f);
                if(scrollHover==null)scrollHover=ScrollThumb(8,.82f);
                scrollSkin.verticalScrollbar=new GUIStyle{name="verticalscrollbar",fixedWidth=18,stretchHeight=true};
                var thumb=new GUIStyle{name="verticalscrollbarthumb",fixedWidth=18,border=new RectOffset(0,0,8,8)};
                thumb.normal.background=scrollNormal;
                thumb.hover.background=scrollHover;thumb.active.background=scrollHover;
                scrollSkin.verticalScrollbarThumb=thumb;
                scrollSkin.verticalScrollbarUpButton=new GUIStyle{name="verticalscrollbarupbutton",fixedHeight=0};
                scrollSkin.verticalScrollbarDownButton=new GUIStyle{name="verticalscrollbardownbutton",fixedHeight=0};
                var custom=new List<GUIStyle>();
                foreach(var style in scrollSkin.customStyles)
                    if(style!=null&&!style.name.StartsWith("verticalscrollbar",System.StringComparison.OrdinalIgnoreCase))custom.Add(style);
                scrollSkin.customStyles=custom.ToArray();
            }
            GUI.skin=scrollSkin;
            try{return GUI.BeginScrollView(viewport,position,content);}
            finally{GUI.skin=previous;}
        }
        static Texture2D ScrollThumb(float width,float opacity)
        {
            var texture=new Texture2D(18,18,TextureFormat.RGBA32,false){name="Minimal scroll thumb",hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color[18*18];
            for(int y=0;y<18;y++)for(int x=0;x<18;x++)
            {
                float centerY=Mathf.Clamp(y+.5f,width/2,18-width/2);
                float distance=Vector2.Distance(new Vector2(x+.5f,y+.5f),new Vector2(9,centerY));
                pixels[y*18+x]=new Color(.73f,.76f,.78f,opacity*Mathf.Clamp01(width/2+.5f-distance));
            }
            texture.SetPixels(pixels);texture.Apply(false,true);return texture;
        }
        static readonly Dictionary<string,Texture2D> icons=new Dictionary<string,Texture2D>();
        static Equipment.Inventory.InventoryUIIcons sharedIcons;
        public static Font Body=>body!=null?body:body=Mismo.Core.ProjectAssets.Load<Font>("Fonts/Cagliostro-Regular");
        public static Font Heading=>heading!=null?heading:heading=Mismo.Core.ProjectAssets.Load<Font>("Fonts/Cagliostro-Regular");
        public static void Text(Rect rect,string value,int size=20,Color? color=null,bool title=false,TextAnchor alignment=TextAnchor.UpperLeft)
        {
            if(words==null)words=new GUIStyle(GUI.skin.label){padding=new RectOffset(),wordWrap=true};
            words.font=title?Heading:Body;words.fontSize=size;words.fontStyle=FontStyle.Normal;words.alignment=alignment;
            words.normal.textColor=color??Ink;GUI.Label(rect,L.Text(value??""),words);
        }
        public static bool NavigationButton(Rect rect,Texture2D icon,string caption,Mismo.Gameplay.Player.Equipment.Inventory.InventoryUIIcons settings,bool selected=false,bool close=false)
        {
            bool clicked=Button(rect,icon==null?(close?"×":caption):"",selected);
            if(icon!=null)
            {
                float size=Mathf.Min(Mathf.Min(rect.width,rect.height)-12,close?(settings!=null?settings.closeIconSize:14):(settings!=null?settings.navigationIconSize:24));
                var tint=selected?Amber:Ink;tint.a*=settings!=null?Mathf.Clamp01(settings.navigationIconOpacity):1;
                DrawIcon(new Rect(rect.center.x-size/2,rect.center.y-size/2,size,size),MapIcons.Mask(icon),tint);
            }
            GUI.Label(rect,new GUIContent("",L.Text(caption)),GUIStyle.none);return clicked;
        }
        public static bool CloseButton(Rect rect)
        {
            var settings=Mismo.Core.ProjectAssets.Load<Mismo.Gameplay.Player.Equipment.Inventory.InventoryUIIcons>("InventoryUIIcons");
            return NavigationButton(rect,settings?.close,"Cerrar",settings,false,true);
        }
        public static bool Button(Rect rect,string text,bool selected=false,bool title=false)
        {
            if(action==null)action=new GUIStyle(){fontSize=20,alignment=TextAnchor.MiddleCenter,padding=new RectOffset(5,5,2,2)};
            action.font=title?Heading:Body;action.fontSize=title?23:20;
            foreach(var state in new[]{action.normal,action.hover,action.active,action.focused}){state.background=null;state.textColor=Ink;}
            bool hover=rect.Contains(Event.current.mousePosition);
            FantasyUI.Panel(rect,GUI.enabled?1:.45f);
            action.normal.textColor=selected?Amber:Ink;
            bool result=GameAudio.Button(rect,L.Text(text),action);
            FantasyUI.Frame(rect,selected?Amber:hover?Color.white:new Color(1,1,1,GUI.enabled?1:.45f));
            return result;
        }
        public static void Border(Rect rect,Color color,float thickness=1)
        {
            if(rect.width>=20&&rect.height>=20){FantasyUI.Frame(rect,color);return;}
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
                if(sharedIcons==null)sharedIcons=Mismo.Core.ProjectAssets.Load<Equipment.Inventory.InventoryUIIcons>("InventoryUIIcons");
                if(sharedIcons!=null&&sharedIcons.abilityIcons!=null)
                    foreach(var texture in sharedIcons.abilityIcons)
                        if(texture!=null&&texture.name==name){icon=texture;break;}
                if(icon==null)icon=Mismo.Core.ProjectAssets.Load<Texture2D>("UI/QuietFantasy/Icons/"+name);
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
                case "AxeFuriousCombo":return "hatchets";
                case "AxeCruelEdge":case "AxeExecutioner":return "dead-eye";
            }
            if(ability.pose==AbilityPose.Bow)return "target-arrows";
            if(ability.pose==AbilityPose.Spin)return "sword-spin";
            if(ability.pose==AbilityPose.Parry)return "shield-reflect";
            return ability.IsPassive?"dead-eye":"broadsword";
        }
    }
}
