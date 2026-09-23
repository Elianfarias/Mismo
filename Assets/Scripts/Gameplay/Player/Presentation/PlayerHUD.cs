using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.Dash;
using Mismo.Gameplay.Player.Movement;
using UnityEngine;
using Mismo.Gameplay.Player.Equipment.Inventory;

namespace Mismo.Gameplay.Player.Presentation
{
    /// <summary>Shared pixel-stepped frames for IMGUI and canvas screens.</summary>
    public static class FantasyUI
    {
        public const int Slice = 9;
        static Texture2D frame, panel, crosshair;
        static GUIStyle border, buttons;
        static InventoryUIIcons menuTheme;
        static Color lastPanelColor;
        static Vector3 lastPanelGradient;
        public static Texture2D PanelTexture
        {
            get
            {
                if(menuTheme==null)menuTheme=Mismo.Core.ProjectAssets.Load<InventoryUIIcons>("InventoryUIIcons");
                var color=menuTheme!=null?menuTheme.menuBackgroundColor:new Color(.12f,.15f,.17f,1);
                var gradient=menuTheme!=null?new Vector3(Mathf.Clamp01(menuTheme.menuCenterOpacity),Mathf.Clamp01(menuTheme.menuEdgeOpacity),Mathf.Clamp(menuTheme.menuEdgeFade,.05f,1)):new Vector3(.88f,.18f,.65f);
                bool rebuild=panel==null;
                if(rebuild)panel=new Texture2D(128,128,TextureFormat.RGBA32,false){name="Shared menu gradient",hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
                if(rebuild||color!=lastPanelColor||gradient!=lastPanelGradient)
                {
                    var pixels=new Color32[128*128];
                    for(int y=0;y<128;y++)for(int x=0;x<128;x++)
                    {
                        float edge=2*Mathf.Min(Mathf.Min(x,127-x),Mathf.Min(y,127-y))/127f;
                        float blend=Mathf.SmoothStep(0,1,Mathf.Clamp01(edge/gradient.z));
                        var pixel=color;pixel.a*=Mathf.Lerp(gradient.y,gradient.x,blend);pixels[y*128+x]=pixel;
                    }
                    panel.SetPixels32(pixels);panel.Apply(false,false);lastPanelColor=color;lastPanelGradient=gradient;
                }
                return panel;
            }
        }
        public static Texture2D FrameTexture => Load(ref frame,"Frame");
        static Texture2D Load(ref Texture2D texture,string name)
        {
            if(texture==null)texture=Mismo.Core.ProjectAssets.Load<Texture2D>("UI/PixelFrames/"+name);
            if(texture!=null){texture.filterMode=FilterMode.Point;texture.wrapMode=TextureWrapMode.Clamp;}
            return texture;
        }
        public static void Frame(Rect rect,Color color)
        {
            if(Event.current.type!=EventType.Repaint||FrameTexture==null)return;
            if(border==null)border=new GUIStyle {border=new RectOffset(Slice,Slice,Slice,Slice)};
            border.normal.background=FrameTexture;
            var old=GUI.color;var background=GUI.backgroundColor;GUI.color=old*color;GUI.backgroundColor=Color.white;
            border.Draw(rect,GUIContent.none,false,false,false,false);GUI.color=old;GUI.backgroundColor=background;
        }
        public static void Panel(Rect rect,float opacity=1)
        {
            Surface(rect,opacity);
            Frame(rect,new Color(1,1,1,Mathf.Clamp01(opacity)));
        }
        public static void Surface(Rect rect,float opacity=1)
        {
            if(Event.current.type!=EventType.Repaint)return;
            var old=GUI.color;GUI.color=old*new Color(1,1,1,Mathf.Clamp01(opacity));
            DrawClippedSurface(rect,PanelTexture);GUI.color=old;
        }
        public static void FillSurface(Rect rect,Color color)
        {
            if(Event.current.type!=EventType.Repaint)return;
            var old=GUI.color;GUI.color=old*color;
            DrawClippedSurface(rect,Texture2D.whiteTexture);GUI.color=old;
        }
        static void DrawClippedSurface(Rect rect,Texture2D texture)
        {
            // Frame.png has a five-pixel inner outline and three-pixel corner steps.
            // Clip in destination units so wide panels never stretch their corner cutouts.
            float unit=Mathf.Min(1,Mathf.Min(rect.width,rect.height)/18f);
            if(unit<=0)return;
            float inset=5*unit,corner=8*unit,step=corner-inset;
            DrawSurfaceStrip(rect,new Rect(rect.x+corner,rect.y+inset,rect.width-2*corner,step),texture);
            DrawSurfaceStrip(rect,new Rect(rect.x+inset,rect.y+corner,rect.width-2*inset,rect.height-2*corner),texture);
            DrawSurfaceStrip(rect,new Rect(rect.x+corner,rect.yMax-corner,rect.width-2*corner,step),texture);
        }
        static void DrawSurfaceStrip(Rect full,Rect strip,Texture2D texture)
        {
            if(strip.width<=0||strip.height<=0)return;
            var uv=new Rect((strip.x-full.x)/full.width,1-(strip.yMax-full.y)/full.height,strip.width/full.width,strip.height/full.height);
            GUI.DrawTextureWithTexCoords(strip,texture,uv,true);
        }
        public static void Crosshair(Vector2 center)
        {
            var texture=Load(ref crosshair,"Crosshair");if(texture==null)return;
            GUI.DrawTexture(new Rect(Mathf.Round(center.x)-12,Mathf.Round(center.y)-12,25,25),texture);
        }
        public static bool Button(Rect rect,string title)
        {
            if(buttons==null)buttons=new GUIStyle(GUI.skin.button){fontSize=16,alignment=TextAnchor.MiddleCenter};
            StyleButton(buttons);
            Surface(rect);
            var old=GUI.backgroundColor;GUI.backgroundColor=Color.white;
            bool clicked=GameAudio.Button(rect,title,buttons);GUI.backgroundColor=old;
            Frame(rect,rect.Contains(Event.current.mousePosition)?PlayerHUD.Gold:Color.white);
            return clicked;
        }
        public static void StyleButton(GUIStyle style)
        {
            style.font=QuietFantasyUI.Body;
            style.border=new RectOffset();
            style.normal.background=null;
            style.hover.background=null;style.active.background=null;
            style.focused.background=null;
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
        public static readonly Color Panel=new Color(.047f,.071f,.082f,.86f),Gold=new Color(.94f,.81f,.50f),Muted=new Color(.61f,.66f,.58f);
        private Health health;
        private InventoryUIIcons icons; private PlayerInventory inventory;
        private CombatState combat;
        private Stamina stamina;
        private BeltDash dash;
        private BasicSwordCombo combo;
        private SwordLunge lunge;
        private SwordParry parry;
        private SwordSpinAttack spin;
        enum EditTarget {None,Health,Stamina,Tab,Skills,Dash,Consumables}
        EditTarget editTarget;
        Vector2 editMouseStart,editValueStart;



        private GUIStyle text;
        public RenderTexture Minimap => GetComponent<WorldMapPanel>()?.Preview;
        private void Start()
        {
            combat=GetComponent<CombatState>();
            icons=Mismo.Core.ProjectAssets.Load<InventoryUIIcons>("InventoryUIIcons");inventory=GetComponent<PlayerInventory>();
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
            DrawNavigation(height);
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
                FantasyUI.Panel(switchRect,tabOpacity);
                // Opacity belongs to the Tab background/frame; keep its content fully readable.
                QuietFantasyUI.DrawIcon(new Rect(left+108+tabOffset.x,height-158+skillsYOffset+tabOffset.y,28,28),WeaponHudIcon(equipment.ActiveDefinition));
                Label(new Rect(left+140+tabOffset.x,height-158+skillsYOffset+tabOffset.y,60,28),"Tab",16,QuietFantasyUI.Ink,TextAnchor.MiddleCenter);
                if(equipment.SecondaryDefinition!=null)QuietFantasyUI.DrawIcon(new Rect(left+204+tabOffset.x,height-158+skillsYOffset+tabOffset.y,28,28),WeaponHudIcon(equipment.SecondaryDefinition));
                string[] keys={"M1","Q","E","R"};
                for(int i=0;i<4;i++)
                {
                    var ability=equipment.GetAbility((Equipment.AbilitySlot)i);
                    if(ability==null)continue;
                    float remaining=equipment.Runner.Remaining(ability);
                    bool active=equipment.Runner.Current!=null&&equipment.Runner.Current.Definition==ability;
                    float skillX=left+skillsOffset.x+(skillsAsColumn?0:i*88);
                    float skillY=height-118+skillsYOffset+skillsOffset.y+(skillsAsColumn?i*88:0);
                    float focusProgress=AbilityFocusProgress(combat!=null?combat.Focus:0,ability.focusCost);
                    bool showFocus=!ability.IsPassive&&ability.focusCost>0;
                    bool hasStamina=ability.IsPassive||stamina==null||stamina.Current>=ability.staminaCost;
                    Ability(skillX,skillY,ability.IsPassive?"PASIVA":keys[i],ability.DisplayName,ability.IsPassive?"EQUIPADA":Status(remaining,active),ability.cooldown>0?remaining/ability.cooldown:0,hasStamina&&(!showFocus||focusProgress>=1),QuietFantasyUI.AbilityIcon(ability),showFocus?focusProgress:-1,hasStamina);
                }
            }
            if(!health.IsDead&&!GameplayPause.BlocksInput&&!UIEditMode)
                FantasyUI.Crosshair(new Vector2(width*Equipment.WeaponAim.Viewport.x,height*(1-Equipment.WeaponAim.Viewport.y)));
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
            if(target==EditTarget.Health||target==EditTarget.Stamina)
            {
                var basePosition=icons!=null?icons.hudVitalsPosition:new Vector2(16,18);
                float barWidth=icons!=null?Mathf.Max(1,icons.hudVitalsWidth):380;
                Vector2 offset=target==EditTarget.Health?(icons!=null?icons.hudHealthOffset:Vector2.zero):(icons!=null?icons.hudStaminaOffset:new Vector2(0,15));
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
                foreach(var target in new[]{EditTarget.Health,EditTarget.Stamina,EditTarget.Tab,EditTarget.Skills,EditTarget.Dash,EditTarget.Consumables})
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
            switch(target){case EditTarget.Health:return icons.hudHealthOffset;case EditTarget.Stamina:return icons.hudStaminaOffset;case EditTarget.Tab:return icons.hudTabOffset;case EditTarget.Skills:return icons.hudSkillsOffset;case EditTarget.Dash:return icons.hudDashOffset;case EditTarget.Consumables:return icons.hudConsumablesOffset;default:return Vector2.zero;}
        }
        void ApplyEditValue(EditTarget target,Vector2 value)
        {
            if(icons==null)return;
            switch(target){case EditTarget.Health:icons.hudHealthOffset=value;break;case EditTarget.Stamina:icons.hudStaminaOffset=value;break;case EditTarget.Tab:icons.hudTabOffset=value;break;case EditTarget.Skills:icons.hudSkillsOffset=value;break;case EditTarget.Dash:icons.hudDashOffset=value;break;case EditTarget.Consumables:icons.hudConsumablesOffset=value;break;}
        }
        void LoadRuntimeLayout()
        {
            if(icons==null)return;
            icons.hudVitalsPosition=ReadVector("Mismo.HUD.Vitals.Position",icons.hudVitalsPosition);
            icons.hudHealthOffset=ReadVector("Mismo.HUD.Health.Offset",icons.hudHealthOffset);
            icons.hudStaminaOffset=ReadVector("Mismo.HUD.Stamina.Offset",icons.hudStaminaOffset);
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
        public static float AbilityFocusProgress(float available,float cost)=>cost<=0?1:Mathf.Clamp01(available/cost);

        // The same 76px square and stepped corners as the approved mockup, starting at twelve o'clock.
        static readonly Vector2[] focusOutline={
            new Vector2(38,3),new Vector2(68,3),new Vector2(68,5),new Vector2(70,5),new Vector2(70,7),new Vector2(72,7),
            new Vector2(72,68),new Vector2(70,68),new Vector2(70,70),new Vector2(68,70),new Vector2(68,72),
            new Vector2(7,72),new Vector2(7,70),new Vector2(5,70),new Vector2(5,68),new Vector2(3,68),
            new Vector2(3,7),new Vector2(5,7),new Vector2(5,5),new Vector2(7,5),new Vector2(7,3),new Vector2(38,3)
        };
        static void FocusContour(Rect rect,float progress,Color color)
        {
            if(Event.current.type!=EventType.Repaint)return;
            float remaining=Mathf.Clamp01(progress)*276; // Manhattan perimeter, including corner steps.
            for(int i=1;i<focusOutline.Length&&remaining>0;i++)
            {
                var start=focusOutline[i-1];var delta=focusOutline[i]-start;
                float length=Mathf.Abs(delta.x)+Mathf.Abs(delta.y);
                float amount=Mathf.Min(remaining,length);
                var end=start+delta*(amount/length);
                // Square caps meet at each step without diagonal joins or rounded corners.
                Fill(new Rect(rect.x+Mathf.Min(start.x,end.x)-1,rect.y+Mathf.Min(start.y,end.y)-1,
                    Mathf.Abs(end.x-start.x)+2,Mathf.Abs(end.y-start.y)+2),color);
                remaining-=amount;
            }
        }
        private void Ability(float x,float y,string key,string name,string status,float cooldown,bool affordable,Texture2D icon=null,float focusProgress=-1,bool hasStamina=true)
        {
            var rect=new Rect(x,y,76,76);HudFrame(rect,icons);
            // Center the visible silhouette, independent of transparent padding in the source PNG.
            QuietFantasyUI.DrawIcon(new Rect(rect.center.x-25,rect.center.y-25,50,50),MapIcons.Mask(icon),affordable?QuietFantasyUI.Ink:QuietFantasyUI.Muted*.65f);
            if(cooldown>0)Fill(new Rect(x+6,y+6+64*(1-Mathf.Clamp01(cooldown)),64,64*Mathf.Clamp01(cooldown)),new Color(.06f,.08f,.12f,.78f));
            if(status=="ACTIVO")QuietFantasyUI.Border(rect,QuietFantasyUI.Amber);
            if(focusProgress>=0)
            {
                FocusContour(rect,1,new Color(.275f,.353f,.376f));
                FocusContour(rect,focusProgress,new Color(.463f,.89f,.859f));
            }
            // Focus is communicated by the perimeter; cooldown and stamina remain distinct.
            if(cooldown>0||!hasStamina)Label(new Rect(x,y+24,76,28),cooldown>0?status:"!",17,QuietFantasyUI.Ink,TextAnchor.MiddleCenter);
            Label(new Rect(x,y+80,76,24),key,17,QuietFantasyUI.Ink,TextAnchor.MiddleCenter);
        }
        public static void HudFrame(Rect rect,InventoryUIIcons theme=null)
        {
            FantasyUI.Panel(rect,theme!=null?theme.hudBackgroundOpacity:.85f);
            FantasyUI.Frame(rect,Color.white);
        }
        void PaintedBar(int row,Vector2 position,float value,Color tint,float width)
        {
            float height=row==0?28:21;
            var frame = new Rect(position.x, position.y, width, height);
            float opacity=icons!=null?Mathf.Clamp01(icons.hudVitalsOpacity):1f;
            FantasyUI.Panel(frame,opacity);
            var well=new Rect(frame.x+6,frame.y+6,Mathf.Max(0,width-12),height-12);
            tint.a*=opacity;
            Fill(new Rect(well.x,well.y,well.width*Mathf.Clamp01(value),well.height),tint);
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
    }
}

