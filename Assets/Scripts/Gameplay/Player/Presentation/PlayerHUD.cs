using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.Dash;
using Mismo.Gameplay.Player.Movement;
using UnityEngine;
using Mismo.Gameplay.Player.Equipment.Inventory;

namespace Mismo.Gameplay.Player.Presentation
{
    /// <summary>Shared Kenney Fantasy UI Borders theme for runtime IMGUI screens.</summary>
    public static class FantasyUI
    {
        static Texture2D frame, panel;
        static GUIStyle border, buttons;
        public static Texture2D PanelTexture => Load(ref panel,"Panel");
        public static Texture2D FrameTexture => Load(ref frame,"Frame");
        static Texture2D Load(ref Texture2D texture,string name)
        {
            if(texture==null)texture=Resources.Load<Texture2D>("UI/FantasyBorders/"+name);
            if(texture!=null){texture.filterMode=FilterMode.Point;texture.wrapMode=TextureWrapMode.Clamp;}
            return texture;
        }
        public static void Frame(Rect rect,Color color)
        {
            if(Event.current.type!=EventType.Repaint||FrameTexture==null)return;
            if(border==null)border=new GUIStyle {border=new RectOffset(12,12,12,12)};
            border.normal.background=FrameTexture;
            var old=GUI.color;var background=GUI.backgroundColor;GUI.color=color;GUI.backgroundColor=Color.white;
            border.Draw(rect,GUIContent.none,false,false,false,false);GUI.color=old;GUI.backgroundColor=background;
        }
        public static void Panel(Rect rect)
        {
            PlayerHUD.Fill(rect,PlayerHUD.Panel);
            Frame(rect,new Color(.57f,.53f,.39f));
        }
        public static bool Button(Rect rect,string title)
        {
            if(buttons==null)buttons=new GUIStyle(GUI.skin.button){fontSize=16,alignment=TextAnchor.MiddleCenter};
            StyleButton(buttons);
            var old=GUI.backgroundColor;GUI.backgroundColor=new Color(.10f,.14f,.17f);
            bool clicked=GameAudio.Button(rect,title,buttons);GUI.backgroundColor=old;
            Frame(rect,rect.Contains(Event.current.mousePosition)?PlayerHUD.Gold:new Color(.64f,.59f,.44f));
            return clicked;
        }
        public static void StyleButton(GUIStyle style)
        {
            style.font=QuietFantasyUI.Body;
            style.border=new RectOffset(12,12,12,12);
            style.normal.background=PanelTexture;
            style.hover.background=PanelTexture;style.active.background=PanelTexture;
            style.focused.background=PanelTexture;
            style.normal.textColor=Color.white;style.hover.textColor=PlayerHUD.Gold;
            style.focused.textColor=PlayerHUD.Gold;style.active.textColor=Color.white;
        }
    }

    /// <summary>Read-only HUD and local north-up minimap updated at 5 Hz.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerHUD : MonoBehaviour
    {
        public static PlayerHUD Active {get;private set;}
        public static readonly Color Panel=new Color(.045f,.065f,.085f,.94f),Gold=new Color(.91f,.76f,.43f),Muted=new Color(.65f,.72f,.74f);
        private Health health;
        private InventoryUIIcons icons; private PlayerInventory inventory;
        private CombatState combat;
        private string reward;
        private float rewardUntil;
        private void OnReward(string value){reward=value;rewardUntil=Time.unscaledTime+1.2f;}
        private Stamina stamina;
        private BeltDash dash;
        private BasicSwordCombo combo;
        private SwordLunge lunge;
        private SwordParry parry;
        private SwordSpinAttack spin;



        private GUIStyle text;
        public RenderTexture Minimap => GetComponent<WorldMapPanel>()?.Preview;
        private void Start()
        {
            combat=GetComponent<CombatState>();if(combat!=null)combat.Rewarded+=OnReward;
            icons=Resources.Load<InventoryUIIcons>("InventoryUIIcons");inventory=GetComponent<PlayerInventory>();
            Active=this;health=GetComponent<Health>();stamina=GetComponent<Stamina>();dash=GetComponent<BeltDash>();
            combo=GetComponentInChildren<BasicSwordCombo>();lunge=GetComponentInChildren<SwordLunge>();parry=GetComponentInChildren<SwordParry>();spin=GetComponentInChildren<SwordSpinAttack>();
        }
        public static float Scale => Mathf.Max(.1f,Mathf.Min(Screen.width/1600f,Screen.height/900f));
        public static void Fill(Rect rect,Color color)
        {Color old=GUI.color;GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=old;}
        public void Label(Rect rect,string value,int size,Color color,TextAnchor alignment=TextAnchor.MiddleLeft)
        {
            if(text==null)text=new GUIStyle(GUI.skin.label);
            text.font=QuietFantasyUI.Body;text.fontSize=size;text.fontStyle=FontStyle.Normal;text.alignment=alignment;text.normal.textColor=color;GUI.Label(rect,value,text);
        }
        private void Bar(float x,float y,float width,float value,Color color)
        {Fill(new Rect(x,y,width,12),new Color(.11f,.14f,.17f));Fill(new Rect(x,y,width*Mathf.Clamp01(value),12),color);}
        private void OnGUI()
        {
            if (WorldMapPanel.AnyOpen || Equipment.Inventory.InventoryPanel.AnyOpen || GetComponent<World.GatheringPlayer>()?.BlocksGameplay==true) return;
            if(health==null)return;
            Matrix4x4 old=GUI.matrix;float scale=Scale;GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);
            float width=Screen.width/scale,height=Screen.height/scale;
            PaintedBar(0,30,health.Normalized,new Color(.95f,.30f,.32f),icons?.hudHealth,"heart-inside");
            PaintedBar(1,67,stamina!=null?stamina.Normalized:0,new Color(.50f,.76f,.39f),icons?.hudStamina,"wingfoot");
            PaintedBar(2,98,combat!=null?combat.Focus/100:0,new Color(1f,.82f,.25f),icons?.hudFocus,"dead-eye");
            DrawNavigation(height);
            if(Time.unscaledTime<rewardUntil)Label(new Rect(width/2-230,height/2+65,460,35),reward,23,Gold,TextAnchor.MiddleCenter);
            float left=(width-792)/2;
            var climbing=GetComponent<TreeClimbing>();
            if(climbing!=null&&climbing.IsClimbing)Label(new Rect(left,height-190,528,28),"TREPAR · W/S subir/bajar · Soltá ESPACIO para soltar",15,Gold,TextAnchor.MiddleCenter);
            var equipment=GetComponent<Equipment.EquipmentLoadout>();
            if(equipment!=null && equipment.ActiveDefinition!=null)
            {
                var switchRect=new Rect(left+99,height-162,142,36);
                GUI.DrawTexture(switchRect,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,new Color(.065f,.09f,.14f,.85f),0,6);
                GUI.DrawTexture(switchRect,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,new Color(.60f,.65f,.70f,.9f),1,6);
                GUI.DrawTexture(new Rect(left+145,height-157,50,26),Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,new Color(.60f,.65f,.70f,.65f),1,5);
                QuietFantasyUI.DrawIcon(new Rect(left+108,height-158,28,28),WeaponHudIcon(equipment.ActiveDefinition));
                Label(new Rect(left+140,height-158,60,28),"Tab",16,QuietFantasyUI.Ink,TextAnchor.MiddleCenter);
                if(equipment.SecondaryDefinition!=null)QuietFantasyUI.DrawIcon(new Rect(left+204,height-158,28,28),WeaponHudIcon(equipment.SecondaryDefinition));
                var cast=equipment.Runner.Current;
                if(cast!=null&&!cast.Began&&cast.Definition.aimFromCamera)
                    Label(new Rect(left,height-190,528,28),cast.Definition.chargeable?"TENSANDO  "+Mathf.RoundToInt(cast.Charge*100)+" %":"PREPARANDO",17,Gold,TextAnchor.MiddleCenter);
                string[] keys={"M1","Q","E","R"};
                for(int i=0;i<4;i++)
                {
                    var ability=equipment.GetAbility((Equipment.AbilitySlot)i);
                    if(ability==null)continue;
                    float remaining=equipment.Runner.Remaining(ability);
                    bool active=equipment.Runner.Current!=null&&equipment.Runner.Current.Definition==ability;
                    Ability(left+i*88,height-118,ability.IsPassive?"PASIVA":keys[i],ability.DisplayName,ability.IsPassive?"EQUIPADA":combat!=null&&combat.Focus<ability.focusCost?"FOCUS "+ability.focusCost.ToString("0"):Status(remaining,active),ability.cooldown>0?remaining/ability.cooldown:0,ability.IsPassive||((stamina==null||stamina.Current>=ability.staminaCost)&&(combat==null||combat.Focus>=ability.focusCost)),QuietFantasyUI.AbilityIcon(ability));
                }
                if(equipment.ActiveDefinition.isBow) Label(new Rect(width*Equipment.WeaponAim.Viewport.x-12,height*(1-Equipment.WeaponAim.Viewport.y)-12,24,24),"+",22,Gold,TextAnchor.MiddleCenter);
            }
            Ability(left+376,height-118,"C",dash!=null?dash.DisplayName.ToUpperInvariant():"ESPECIAL",Status(dash!=null?dash.CooldownRemaining:0,dash!=null&&dash.IsActive),dash!=null&&dash.CooldownDuration>0?dash.CooldownRemaining/dash.CooldownDuration:0,true,dash!=null&&dash.Definition!=null&&dash.Definition.icon!=null?dash.Definition.icon:icons?.special!=null?icons.special:QuietFantasyUI.Icon("sprint"));
            Fill(new Rect(left+357,height-115,1.5f,70),new Color(.65f,.69f,.73f,.8f));
            Fill(new Rect(left+469,height-115,1.5f,70),new Color(.65f,.69f,.73f,.8f));
            // All slots and separators share the same vertical center (height - 80).
            DrawConsumables(left+488,height-80-56/2f);
            if(health.IsDead)
            {Fill(new Rect(width/2-210,height/2-46,420,92),Panel);Label(new Rect(width/2-200,height/2-36,400,40),"HAS CAÍDO",26,Gold,TextAnchor.MiddleCenter);Label(new Rect(width/2-200,height/2+5,400,30),"Regresando al pueblo…",16,Color.white,TextAnchor.MiddleCenter);}
            GUI.matrix=old;
        }
        private static Texture2D WeaponHudIcon(Equipment.WeaponDefinition weapon)
        {
            if(weapon==null)return null;
            return MapIcons.Mask(weapon.hudIcon!=null?weapon.hudIcon:QuietFantasyUI.Icon(weapon.isBow?"target-arrows":"broadsword"));
        }
        private static string Status(float cooldown,bool active)=>active?"ACTIVO":cooldown>.01f?cooldown.ToString("0.0")+" s":"LISTO";
        private void Ability(float x,float y,string key,string name,string status,float cooldown,bool affordable,Texture2D icon=null)
        {
            var rect=new Rect(x,y,76,76);HudFrame(rect,icons);
            // Center the visible silhouette, independent of transparent padding in the source PNG.
            QuietFantasyUI.DrawIcon(new Rect(rect.center.x-25,rect.center.y-25,50,50),MapIcons.Mask(icon),affordable?QuietFantasyUI.Ink:QuietFantasyUI.Muted*.65f);
            if(cooldown>0)Fill(new Rect(x+3,y+3+70*(1-Mathf.Clamp01(cooldown)),70,70*Mathf.Clamp01(cooldown)),new Color(.06f,.08f,.12f,.78f));
            if(status=="ACTIVO")QuietFantasyUI.Border(rect,QuietFantasyUI.Amber);
            if(cooldown>0||!affordable)Label(new Rect(x,y+24,76,28),cooldown>0?status:"!",17,QuietFantasyUI.Ink,TextAnchor.MiddleCenter);
            Label(new Rect(x,y+80,76,24),key,17,QuietFantasyUI.Ink,TextAnchor.MiddleCenter);
        }
        public static void HudFrame(Rect rect,InventoryUIIcons theme=null)
        {
            Fill(rect,theme!=null&&theme.useOliveTheme?new Color(.12f,.17f,.12f,theme.hudBackgroundOpacity):new Color(.065f,.09f,.14f,theme!=null?theme.hudBackgroundOpacity:.65f));
            var art=theme!=null&&theme.useOliveTheme?theme.oliveConsumableSlotBackground:theme?.consumableSlotBackground;
            var uv=theme!=null&&theme.useOliveTheme?theme.oliveConsumableSlotUV:theme!=null?theme.consumableSlotUV:new Rect(0,0,1,1);
            if(art!=null)GUI.DrawTextureWithTexCoords(rect,art,uv);
            else QuietFantasyUI.Border(rect,new Color(.58f,.63f,.68f,.8f));
        }
        void PaintedBar(int row,float y,float value,Color tint,Texture2D icon,string fallback)
        {
            bool healthRow=row==0;
            float height=healthRow?28:21;
            var frame=new Rect(66,y,380,height);
            QuietFantasyUI.DrawIcon(new Rect(30,y+height/2-12,24,24),MapIcons.Mask(icon!=null?icon:QuietFantasyUI.Icon(fallback)),tint);
            // Pixel regions in the generated atlas; Unity UVs start at the bottom.
            float top=row==0?138:row==1?353:537;
            float cropHeight=row==0?130:106;
            float cropWidth=row==0?1928:1876;
            if(icons?.PaintedFrames!=null)
                GUI.DrawTextureWithTexCoords(frame,icons.PaintedFrames,new Rect(38f/1983,(793-top-cropHeight)/793,cropWidth/1983,cropHeight/793));
            var well=new Rect(frame.x+15,frame.y+(healthRow?8:6),healthRow?327:335,healthRow?13:9);
            if(icons?.PaintedFrames==null)Fill(well,new Color(.03f,.05f,.06f,.9f));
            if(value<=0)return;
            GUI.BeginGroup(new Rect(well.x,well.y,well.width*Mathf.Clamp01(value),well.height));
            GUI.DrawTexture(new Rect(0,0,well.width,well.height),icons?.PaintedFill!=null?icons.PaintedFill:Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,tint,0,3);
            GUI.EndGroup();
        }
        void DrawNavigation(float height)
        {
            QuietFantasyUI.DrawIcon(new Rect(28,height-68,32,32),icons?.menu!=null?MapIcons.Mask(icons.menu):null);
            Label(new Rect(67,height-68,28,32),"B",17,QuietFantasyUI.Ink);
            QuietFantasyUI.DrawIcon(new Rect(110,height-68,32,32),icons?.backpack!=null?MapIcons.Mask(icons.backpack):null);
            Label(new Rect(149,height-68,28,32),"I",17,QuietFantasyUI.Ink);
        }
        void DrawConsumables(float x,float y)
        {
            for(int i=0;i<4;i++)
            {
                var rect=new Rect(x+i*76,y,56,56);HudFrame(rect,icons);
                string id=inventory!=null?inventory.ConsumableSlot(i):null;
                var item=inventory!=null?inventory.Material(id):null;
                if(item!=null)
                {
                    int count=inventory.MaterialCount(id);var previous=GUI.color;
                    if(count==0)GUI.color=new Color(1,1,1,.3f);
                    if(item.icon!=null)
                    {
                        var sprite=item.icon;var uv=sprite.textureRect;var texture=sprite.texture;
                        float aspect=sprite.rect.width/sprite.rect.height,w=Mathf.Min(36,36*aspect),h=w/aspect;
                        GUI.DrawTextureWithTexCoords(new Rect(rect.center.x-w/2,rect.center.y-h/2,w,h),texture,new Rect(uv.x/texture.width,uv.y/texture.height,uv.width/texture.width,uv.height/texture.height));
                    }
                    GUI.color=previous;
                    if(item.useCooldownSeconds>0&&inventory.PotionCooldownRemaining>0)
                    {
                        Fill(new Rect(rect.x+3,rect.y+3,50,50),new Color(.05f,.07f,.10f,.65f));
                        Label(rect,Mathf.CeilToInt(inventory.PotionCooldownRemaining).ToString(),18,QuietFantasyUI.Ink,TextAnchor.MiddleCenter);
                    }
                    Label(new Rect(rect.x+30,rect.y+33,22,20),count.ToString(),14,QuietFantasyUI.Ink,TextAnchor.MiddleRight);
                }
                Label(new Rect(rect.x,rect.yMax+14,rect.width,24),(i+1).ToString(),17,QuietFantasyUI.Ink,TextAnchor.MiddleCenter);
            }
        }
        private void OnDisable(){if(Active==this)Active=null;}
        private void OnDestroy(){if(combat!=null)combat.Rewarded-=OnReward;}
    }
}

