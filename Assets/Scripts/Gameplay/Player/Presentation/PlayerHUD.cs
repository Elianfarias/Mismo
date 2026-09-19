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
        public static bool UIEditMode {get;private set;}
        public static readonly Color Panel=new Color(.075f,.125f,.095f,.94f),Gold=new Color(.85f,.68f,.32f),Muted=new Color(.61f,.66f,.58f);
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
        enum EditTarget {None,Health,Stamina,Focus,Tab,Skills,Dash,Consumables}
        EditTarget editTarget;
        Vector2 editMouseStart,editValueStart;



        private GUIStyle text;
        public RenderTexture Minimap => GetComponent<WorldMapPanel>()?.Preview;
        private void Start()
        {
            combat=GetComponent<CombatState>();if(combat!=null)combat.Rewarded+=OnReward;
            icons=Resources.Load<InventoryUIIcons>("InventoryUIIcons");inventory=GetComponent<PlayerInventory>();
            Active=this;health=GetComponent<Health>();stamina=GetComponent<Stamina>();dash=GetComponent<BeltDash>();
            combo=GetComponentInChildren<BasicSwordCombo>();lunge=GetComponentInChildren<SwordLunge>();parry=GetComponentInChildren<SwordParry>();spin=GetComponentInChildren<SwordSpinAttack>();
            LoadRuntimeLayout();
        }
        public static void SetUIEditMode(bool value)
        {
            UIEditMode=value;
            if(Active!=null)
            {
                Active.editTarget=EditTarget.None;
                if(!value)Active.SaveRuntimeLayout();
            }
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
            // Read layout values every frame so they can be tuned live from InventoryUIIcons.
            var vitalsPosition=icons!=null?icons.hudVitalsPosition:new Vector2(16,18);
            float vitalsWidth=icons!=null?Mathf.Max(1,icons.hudVitalsWidth):380;
            PaintedBar(0,vitalsPosition+(icons!=null?icons.hudHealthOffset:Vector2.zero),health.Normalized,new Color(.95f,.30f,.32f),vitalsWidth);
            PaintedBar(1,vitalsPosition+(icons!=null?icons.hudStaminaOffset:new Vector2(0,15)),stamina!=null?stamina.Normalized:0,new Color(.50f,.76f,.39f),vitalsWidth);
            PaintedBar(2,vitalsPosition+(icons!=null?icons.hudFocusOffset:new Vector2(0,30)),combat!=null?combat.Focus/100:0,new Color(1f,.82f,.25f),vitalsWidth);
            DrawNavigation(height);
            if(Time.unscaledTime<rewardUntil)Label(new Rect(width/2-230,height/2+65,460,35),reward,23,Gold,TextAnchor.MiddleCenter);
            float left=(width-792)/2;
            var climbing=GetComponent<TreeClimbing>();
            if(climbing!=null&&climbing.IsClimbing)Label(new Rect(left,height-190,528,28),"TREPAR · W/S subir/bajar · Soltá ESPACIO para soltar",15,Gold,TextAnchor.MiddleCenter);
            var equipment=GetComponent<Equipment.EquipmentLoadout>();
            // Both action rows are read every frame so their height can be tuned live from InventoryUIIcons.
            float skillsYOffset=icons!=null?icons.hudSkillsYOffset:0;
            float consumablesYOffset=icons!=null?icons.hudConsumablesYOffset:0;
            bool skillsAsColumn=icons!=null&&icons.hudSkillsAsColumn;
            bool consumablesAsColumn=icons!=null&&icons.hudConsumablesAsColumn;
            var skillsOffset=icons!=null?icons.hudSkillsOffset:Vector2.zero;
            var dashOffset=icons!=null?icons.hudDashOffset:Vector2.zero;
            var consumablesOffset=icons!=null?icons.hudConsumablesOffset:Vector2.zero;
            if(equipment!=null && equipment.ActiveDefinition!=null)
            {
                float tabOpacity=icons!=null?Mathf.Clamp01(icons.hudTabOpacity):1f;
                var tabOffset=icons!=null?icons.hudTabOffset:Vector2.zero;
                var switchRect=new Rect(left+99+tabOffset.x,height-162+skillsYOffset+tabOffset.y,142,36);
                GUI.DrawTexture(switchRect,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,new Color(.065f,.09f,.14f,.85f*tabOpacity),0,6);
                GUI.DrawTexture(switchRect,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,new Color(.60f,.65f,.70f,.9f*tabOpacity),1,6);
                GUI.DrawTexture(new Rect(left+145+tabOffset.x,height-157+skillsYOffset+tabOffset.y,50,26),Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,new Color(.60f,.65f,.70f,.65f*tabOpacity),1,5);
                // Opacity belongs to the Tab background/frame; keep its content fully readable.
                QuietFantasyUI.DrawIcon(new Rect(left+108+tabOffset.x,height-158+skillsYOffset+tabOffset.y,28,28),WeaponHudIcon(equipment.ActiveDefinition));
                Label(new Rect(left+140+tabOffset.x,height-158+skillsYOffset+tabOffset.y,60,28),"Tab",16,QuietFantasyUI.Ink,TextAnchor.MiddleCenter);
                if(equipment.SecondaryDefinition!=null)QuietFantasyUI.DrawIcon(new Rect(left+204+tabOffset.x,height-158+skillsYOffset+tabOffset.y,28,28),WeaponHudIcon(equipment.SecondaryDefinition));
                var cast=equipment.Runner.Current;
                if(cast!=null&&!cast.Began&&cast.Definition.aimFromCamera)
                    Label(new Rect(left,height-190+skillsYOffset,528,28),cast.Definition.chargeable?"TENSANDO  "+Mathf.RoundToInt(cast.Charge*100)+" %":"PREPARANDO",17,Gold,TextAnchor.MiddleCenter);
                string[] keys={"M1","Q","E","R"};
                for(int i=0;i<4;i++)
                {
                    var ability=equipment.GetAbility((Equipment.AbilitySlot)i);
                    if(ability==null)continue;
                    float remaining=equipment.Runner.Remaining(ability);
                    bool active=equipment.Runner.Current!=null&&equipment.Runner.Current.Definition==ability;
                    float skillX=left+skillsOffset.x+(skillsAsColumn?0:i*88);
                    float skillY=height-118+skillsYOffset+skillsOffset.y+(skillsAsColumn?i*88:0);
                    Ability(skillX,skillY,ability.IsPassive?"PASIVA":keys[i],ability.DisplayName,ability.IsPassive?"EQUIPADA":combat!=null&&combat.Focus<ability.focusCost?"FOCUS "+ability.focusCost.ToString("0"):Status(remaining,active),ability.cooldown>0?remaining/ability.cooldown:0,ability.IsPassive||((stamina==null||stamina.Current>=ability.staminaCost)&&(combat==null||combat.Focus>=ability.focusCost)),QuietFantasyUI.AbilityIcon(ability));
                }
                if(equipment.ActiveDefinition.isBow) Label(new Rect(width*Equipment.WeaponAim.Viewport.x-12,height*(1-Equipment.WeaponAim.Viewport.y)-12,24,24),"+",22,Gold,TextAnchor.MiddleCenter);
            }
            float dashX=left+376+dashOffset.x;
            float dashY=height-118+skillsYOffset+dashOffset.y;
            Ability(dashX,dashY,"C",dash!=null?dash.DisplayName.ToUpperInvariant():"ESPECIAL",Status(dash!=null?dash.CooldownRemaining:0,dash!=null&&dash.IsActive),dash!=null&&dash.CooldownDuration>0?dash.CooldownRemaining/dash.CooldownDuration:0,true,dash!=null&&dash.Definition!=null&&dash.Definition.icon!=null?dash.Definition.icon:icons?.special!=null?icons.special:QuietFantasyUI.Icon("sprint"));
            if(icons==null||icons.hudShowActionSeparators)
            {
                Fill(new Rect(left+357+dashOffset.x,height-115+skillsYOffset+dashOffset.y,1.5f,70),new Color(.65f,.69f,.73f,.8f));
                Fill(new Rect(left+469+dashOffset.x,height-115+skillsYOffset+dashOffset.y,1.5f,70),new Color(.65f,.69f,.73f,.8f));
            }
            // All slots and separators share the same vertical center (height - 80).
            var consumablePosition=ConsumablesPosition(width,height,left,consumablesAsColumn);
            DrawConsumables(consumablePosition.x,consumablePosition.y,consumablesAsColumn,width);
            if(UIEditMode)DrawUIEditOverlay(width,height,left,skillsAsColumn,consumablesAsColumn);
            if(health.IsDead)
            {Fill(new Rect(width/2-210,height/2-46,420,92),Panel);Label(new Rect(width/2-200,height/2-36,400,40),"HAS CAÍDO",26,Gold,TextAnchor.MiddleCenter);Label(new Rect(width/2-200,height/2+5,400,30),"Regresando al pueblo…",16,Color.white,TextAnchor.MiddleCenter);}
            GUI.matrix=old;
        }
        void DrawUIEditOverlay(float width,float height,float left,bool skillsAsColumn,bool consumablesAsColumn)
        {
            HandleUIEditInput(width,height,left,skillsAsColumn,consumablesAsColumn);
            EditBox(UIRect(EditTarget.Health,width,height,left,skillsAsColumn,consumablesAsColumn),"VIDA",new Color(.95f,.30f,.32f,.9f));
            EditBox(UIRect(EditTarget.Stamina,width,height,left,skillsAsColumn,consumablesAsColumn),"STAMINA",new Color(.50f,.76f,.39f,.9f));
            EditBox(UIRect(EditTarget.Focus,width,height,left,skillsAsColumn,consumablesAsColumn),"FOCUS",new Color(1f,.82f,.25f,.9f));
            EditBox(UIRect(EditTarget.Tab,width,height,left,skillsAsColumn,consumablesAsColumn),"TAB",new Color(.95f,.78f,.35f,.9f));
            EditBox(UIRect(EditTarget.Skills,width,height,left,skillsAsColumn,consumablesAsColumn),"HABILIDADES",new Color(.45f,.75f,.95f,.9f));
            EditBox(UIRect(EditTarget.Dash,width,height,left,skillsAsColumn,consumablesAsColumn),"DASH",new Color(.70f,.52f,.90f,.9f));
            EditBox(UIRect(EditTarget.Consumables,width,height,left,skillsAsColumn,consumablesAsColumn),"CONSUMIBLES",new Color(.45f,.85f,.55f,.9f));
        }
        void EditBox(Rect rect,string title,Color color)
        {
            var old=GUI.color;GUI.color=new Color(color.r,color.g,color.b,.08f);GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=old;
            QuietFantasyUI.Border(rect,color,2);
            Label(new Rect(rect.x,rect.y-22,rect.width,20),title,14,color,TextAnchor.MiddleCenter);
        }
        Rect UIRect(EditTarget target,float width,float height,float left,bool skillsAsColumn,bool consumablesAsColumn)
        {
            if(target==EditTarget.Health||target==EditTarget.Stamina||target==EditTarget.Focus)
            {
                var basePosition=icons!=null?icons.hudVitalsPosition:new Vector2(16,18);
                float barWidth=icons!=null?Mathf.Max(1,icons.hudVitalsWidth):380;
                Vector2 offset=target==EditTarget.Health?(icons!=null?icons.hudHealthOffset:Vector2.zero):target==EditTarget.Stamina?(icons!=null?icons.hudStaminaOffset:new Vector2(0,15)):(icons!=null?icons.hudFocusOffset:new Vector2(0,30));
                // Keep each hitbox tight; the bars are intentionally close together and must remain individually selectable.
                return ExpandRect(new Rect(basePosition+offset,new Vector2(barWidth,target==EditTarget.Health?28:21)),2);
            }
            if(target==EditTarget.Tab)
            {
                var offset=icons!=null?icons.hudTabOffset:Vector2.zero;
                return ExpandRect(new Rect(left+99+offset.x,height-162+(icons!=null?icons.hudSkillsYOffset:0)+offset.y,142,36),8);
            }
            if(target==EditTarget.Skills)
            {
                var offset=icons!=null?icons.hudSkillsOffset:Vector2.zero;
                float y=height-118+(icons!=null?icons.hudSkillsYOffset:0)+offset.y;
                float w=skillsAsColumn?76:4*88-12;float h=skillsAsColumn?4*88+24:100;
                return ExpandRect(new Rect(left+offset.x,y,w,h),8);
            }
            if(target==EditTarget.Dash)
            {
                var offset=icons!=null?icons.hudDashOffset:Vector2.zero;
                return ExpandRect(new Rect(left+376+offset.x,height-118+(icons!=null?icons.hudSkillsYOffset:0)+offset.y,76,100),8);
            }
            var consumablePosition=ConsumablesPosition(width,height,left,consumablesAsColumn);
            float consumableW=consumablesAsColumn?56:4*76-20;float consumableH=consumablesAsColumn?4*70+80:95;
            return ExpandRect(new Rect(consumablePosition.x,consumablePosition.y,consumableW,consumableH),8);
        }
        Vector2 ConsumablesPosition(float width,float height,float left,bool asColumn)
        {
            var offset=icons!=null?icons.hudConsumablesOffset:Vector2.zero;
            float x=left+488+offset.x;
            float y=height-80-56/2f+(icons!=null?icons.hudConsumablesYOffset:0)+offset.y;
            float contentWidth=asColumn?56:4*76-20;
            float contentHeight=asColumn?4*70+80:95;
            const float margin=8;
            x=Mathf.Clamp(x,margin,Mathf.Max(margin,width-contentWidth-margin));
            y=Mathf.Clamp(y,margin,Mathf.Max(margin,height-contentHeight-margin));
            return new Vector2(x,y);
        }
        static Rect CombineRects(Rect a,Rect b)
        {return Rect.MinMaxRect(Mathf.Min(a.xMin,b.xMin),Mathf.Min(a.yMin,b.yMin),Mathf.Max(a.xMax,b.xMax),Mathf.Max(a.yMax,b.yMax));}
        static Rect ExpandRect(Rect rect,float padding)
        {return new Rect(rect.x-padding,rect.y-padding,rect.width+padding*2,rect.height+padding*2);}
        void HandleUIEditInput(float width,float height,float left,bool skillsAsColumn,bool consumablesAsColumn)
        {
            var e=Event.current;
            if(e.type==EventType.MouseDown&&e.button==0)
            {
                foreach(var target in new[]{EditTarget.Health,EditTarget.Stamina,EditTarget.Focus,EditTarget.Tab,EditTarget.Skills,EditTarget.Dash,EditTarget.Consumables})
                    if(UIRect(target,width,height,left,skillsAsColumn,consumablesAsColumn).Contains(e.mousePosition))
                    {editTarget=target;editMouseStart=e.mousePosition;editValueStart=GetEditValue(target);e.Use();break;}
            }
            else if(e.type==EventType.MouseDrag&&editTarget!=EditTarget.None)
            {
                ApplyEditValue(editTarget,editValueStart+e.mousePosition-editMouseStart);e.Use();
            }
            else if(e.type==EventType.MouseUp&&editTarget!=EditTarget.None)
            {editTarget=EditTarget.None;SaveRuntimeLayout();e.Use();}
        }
        Vector2 GetEditValue(EditTarget target)
        {
            if(icons==null)return Vector2.zero;
            switch(target){case EditTarget.Health:return icons.hudHealthOffset;case EditTarget.Stamina:return icons.hudStaminaOffset;case EditTarget.Focus:return icons.hudFocusOffset;case EditTarget.Tab:return icons.hudTabOffset;case EditTarget.Skills:return icons.hudSkillsOffset;case EditTarget.Dash:return icons.hudDashOffset;case EditTarget.Consumables:return icons.hudConsumablesOffset;default:return Vector2.zero;}
        }
        void ApplyEditValue(EditTarget target,Vector2 value)
        {
            if(icons==null)return;
            switch(target){case EditTarget.Health:icons.hudHealthOffset=value;break;case EditTarget.Stamina:icons.hudStaminaOffset=value;break;case EditTarget.Focus:icons.hudFocusOffset=value;break;case EditTarget.Tab:icons.hudTabOffset=value;break;case EditTarget.Skills:icons.hudSkillsOffset=value;break;case EditTarget.Dash:icons.hudDashOffset=value;break;case EditTarget.Consumables:icons.hudConsumablesOffset=value;break;}
        }
        void LoadRuntimeLayout()
        {
            if(icons==null)return;
            icons.hudVitalsPosition=ReadVector("Mismo.HUD.Vitals.Position",icons.hudVitalsPosition);
            icons.hudHealthOffset=ReadVector("Mismo.HUD.Health.Offset",icons.hudHealthOffset);
            icons.hudStaminaOffset=ReadVector("Mismo.HUD.Stamina.Offset",icons.hudStaminaOffset);
            icons.hudFocusOffset=ReadVector("Mismo.HUD.Focus.Offset",icons.hudFocusOffset);
            icons.hudSkillsOffset=ReadVector("Mismo.HUD.Skills.Offset",icons.hudSkillsOffset);
            icons.hudDashOffset=ReadVector("Mismo.HUD.Dash.Offset",icons.hudDashOffset);
            icons.hudTabOffset=ReadVector("Mismo.HUD.Tab.Offset",icons.hudTabOffset);
            icons.hudConsumablesOffset=ReadVector("Mismo.HUD.Consumables.Offset",icons.hudConsumablesOffset);
            if(PlayerPrefs.HasKey("Mismo.HUD.SkillsColumn"))icons.hudSkillsAsColumn=PlayerPrefs.GetInt("Mismo.HUD.SkillsColumn")==1;
            if(PlayerPrefs.HasKey("Mismo.HUD.ConsumablesColumn"))icons.hudConsumablesAsColumn=PlayerPrefs.GetInt("Mismo.HUD.ConsumablesColumn")==1;
            if(PlayerPrefs.HasKey("Mismo.HUD.ActionSeparators"))icons.hudShowActionSeparators=PlayerPrefs.GetInt("Mismo.HUD.ActionSeparators")==1;
            icons.hudBackgroundOpacity=PlayerPrefs.GetFloat("Mismo.HUD.BackgroundOpacity",icons.hudBackgroundOpacity);
            icons.hudVitalsOpacity=PlayerPrefs.GetFloat("Mismo.HUD.VitalsOpacity",icons.hudVitalsOpacity);
            icons.hudTabOpacity=PlayerPrefs.GetFloat("Mismo.HUD.TabOpacity",icons.hudTabOpacity);
        }
        void SaveRuntimeLayout()
        {
            if(icons==null)return;
            WriteVector("Mismo.HUD.Vitals.Position",icons.hudVitalsPosition);
            WriteVector("Mismo.HUD.Health.Offset",icons.hudHealthOffset);
            WriteVector("Mismo.HUD.Stamina.Offset",icons.hudStaminaOffset);
            WriteVector("Mismo.HUD.Focus.Offset",icons.hudFocusOffset);
            WriteVector("Mismo.HUD.Skills.Offset",icons.hudSkillsOffset);
            WriteVector("Mismo.HUD.Dash.Offset",icons.hudDashOffset);
            WriteVector("Mismo.HUD.Tab.Offset",icons.hudTabOffset);
            WriteVector("Mismo.HUD.Consumables.Offset",icons.hudConsumablesOffset);
            PlayerPrefs.SetInt("Mismo.HUD.SkillsColumn",icons.hudSkillsAsColumn?1:0);
            PlayerPrefs.SetInt("Mismo.HUD.ConsumablesColumn",icons.hudConsumablesAsColumn?1:0);
            PlayerPrefs.SetInt("Mismo.HUD.ActionSeparators",icons.hudShowActionSeparators?1:0);
            PlayerPrefs.SetFloat("Mismo.HUD.BackgroundOpacity",icons.hudBackgroundOpacity);
            PlayerPrefs.SetFloat("Mismo.HUD.VitalsOpacity",icons.hudVitalsOpacity);
            PlayerPrefs.SetFloat("Mismo.HUD.TabOpacity",icons.hudTabOpacity);
            PlayerPrefs.Save();
        }
        static Vector2 ReadVector(string key,Vector2 fallback)
        {return PlayerPrefs.HasKey(key+".x")?new Vector2(PlayerPrefs.GetFloat(key+".x"),PlayerPrefs.GetFloat(key+".y")):fallback;}
        static void WriteVector(string key,Vector2 value){PlayerPrefs.SetFloat(key+".x",value.x);PlayerPrefs.SetFloat(key+".y",value.y);}
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
        void PaintedBar(int row,Vector2 position,float value,Color tint,float width)
        {
            bool healthRow=row==0;
            float height=healthRow?28:21;
            var frame = new Rect(position.x, position.y, width, height);
            float opacity=icons!=null?Mathf.Clamp01(icons.hudVitalsOpacity):1f;
            // Pixel regions in the generated atlas; Unity UVs start at the bottom.
            float top=row==0?138:row==1?353:537;
            float cropHeight=row==0?130:106;
            float cropWidth=row==0?1928:1876;
            var previousColor=GUI.color;
            var frameColor=previousColor;frameColor.a*=opacity;GUI.color=frameColor;
            if(icons?.PaintedFrames!=null)
                GUI.DrawTextureWithTexCoords(frame,icons.PaintedFrames,new Rect(38f/1983,(793-top-cropHeight)/793,cropWidth/1983,cropHeight/793));
            GUI.color=previousColor;
            var well=new Rect(frame.x+15,frame.y+(healthRow?8:6),Mathf.Max(1,width-(healthRow?53:45)),healthRow?13:9);
            if(icons?.PaintedFrames==null)Fill(well,new Color(.03f,.05f,.06f,.9f*opacity));
            if(value<=0)return;
            GUI.BeginGroup(new Rect(well.x,well.y,well.width*Mathf.Clamp01(value),well.height));
            tint.a*=opacity;
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
        void DrawConsumables(float x,float y,bool asColumn,float canvasWidth)
        {
            for(int i=0;i<4;i++)
            {
                var rect=new Rect(asColumn?x:x+i*76,asColumn?y+i*70:y,56,56);HudFrame(rect,icons);
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
                float keyX=rect.x+60;
                if(asColumn&&keyX+28>canvasWidth-4)keyX=rect.x-30;
                if(asColumn)keyX=Mathf.Clamp(keyX,4,Mathf.Max(4,canvasWidth-32));
                var keyRect=asColumn?new Rect(keyX,rect.y+16,28,24):new Rect(rect.x,rect.yMax+14,rect.width,24);
                Label(keyRect,(i+1).ToString(),17,QuietFantasyUI.Ink,TextAnchor.MiddleCenter);
            }
        }
        private void OnDisable(){if(Active==this){Active=null;UIEditMode=false;}}
        private void OnDestroy(){if(combat!=null)combat.Rewarded-=OnReward;}
    }
}

