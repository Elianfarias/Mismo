using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.Dash;
using Mismo.Gameplay.Player.Movement;
using UnityEngine;

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
        private UnityEngine.Camera mapCamera;
        private RenderTexture map;
        private float nextMap;
        private GUIStyle text;
        public RenderTexture Minimap => map;
        private void Start()
        {
            combat=GetComponent<CombatState>();if(combat!=null)combat.Rewarded+=OnReward;
            Active=this;health=GetComponent<Health>();stamina=GetComponent<Stamina>();dash=GetComponent<BeltDash>();
            combo=GetComponentInChildren<BasicSwordCombo>();lunge=GetComponentInChildren<SwordLunge>();parry=GetComponentInChildren<SwordParry>();spin=GetComponentInChildren<SwordSpinAttack>();
            var go=new GameObject("Local minimap camera");go.transform.SetParent(transform,false);
            mapCamera=go.AddComponent<UnityEngine.Camera>();mapCamera.enabled=false;mapCamera.orthographic=true;mapCamera.orthographicSize=45;
            mapCamera.nearClipPlane=.1f;mapCamera.farClipPlane=220;mapCamera.clearFlags=CameraClearFlags.SolidColor;
            mapCamera.backgroundColor=new Color(.16f,.23f,.18f);mapCamera.cullingMask=~(1<<2);mapCamera.allowHDR=false;mapCamera.allowMSAA=false;
            map=new RenderTexture(256,256,16){name="Local minimap",filterMode=FilterMode.Bilinear};map.Create();mapCamera.targetTexture=map;
        }
        private void LateUpdate()
        {
            if(SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null || mapCamera==null || Time.unscaledTime<nextMap)return;
            nextMap=Time.unscaledTime+.2f;
            mapCamera.transform.SetPositionAndRotation(transform.position+new Vector3(0,80,-55),Quaternion.LookRotation(new Vector3(0,-80,55)));mapCamera.Render();
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
            if (Equipment.Inventory.InventoryPanel.AnyOpen || GetComponent<World.GatheringPlayer>()?.BlocksGameplay==true) return;
            if(health==null)return;
            Matrix4x4 old=GUI.matrix;float scale=Scale;GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);
            float width=Screen.width/scale,height=Screen.height/scale;
            FantasyUI.Panel(new Rect(24,24,300,132));
            Label(new Rect(43,34,260,22),"EXPLORADOR",16,Gold);
            Label(new Rect(43,63,260,20),"VIDA    "+Mathf.CeilToInt(health.Current)+" / "+health.Maximum.ToString("0"),14,Color.white);
            Bar(43,88,262,health.Normalized,new Color(.83f,.26f,.30f));
            Label(new Rect(43,107,260,19),"STAMINA    "+(stamina!=null?stamina.Current.ToString("0")+" / "+stamina.Maximum.ToString("0"):"—"),12,Muted);
            Bar(43,131,262,stamina!=null?stamina.Normalized:0,new Color(.29f,.73f,.52f));
            if(combat!=null)
            {
                FantasyUI.Panel(new Rect(24,162,300,57));
                Label(new Rect(43,165,262,20),"FOCUS    "+combat.Focus.ToString("0")+" / 100",13,Gold);
                Bar(43,193,262,combat.Focus/100,Gold);
            }
            if(Time.unscaledTime<rewardUntil)Label(new Rect(width/2-230,height/2+65,460,35),reward,23,Gold,TextAnchor.MiddleCenter);
            float left=(width-528)/2;
            var climbing=GetComponent<TreeClimbing>();
            if(climbing!=null&&climbing.IsClimbing)Label(new Rect(left,height-190,528,28),"TREPAR · W/S subir/bajar · Soltá ESPACIO para soltar",15,Gold,TextAnchor.MiddleCenter);
            var equipment=GetComponent<Equipment.EquipmentLoadout>();
            if(equipment!=null && equipment.ActiveDefinition!=null)
            {
                Label(new Rect(left,height-149,528,26),equipment.ActiveDefinition.DisplayName+"    [TAB]  "+(equipment.SecondaryDefinition!=null?equipment.SecondaryDefinition.DisplayName:""),16,Gold,TextAnchor.MiddleCenter);
                var cast=equipment.Runner.Current;
                if(cast!=null&&!cast.Began&&cast.Definition.aimFromCamera)
                    Label(new Rect(left,height-190,528,28),cast.Definition.chargeable?"TENSANDO  "+Mathf.RoundToInt(cast.Charge*100)+" %":"PREPARANDO",17,Gold,TextAnchor.MiddleCenter);
                string[] keys={"CLICK","Q","E","R"};
                for(int i=0;i<4;i++)
                {
                    var ability=equipment.GetAbility((Equipment.AbilitySlot)i);
                    if(ability==null)continue;
                    float remaining=equipment.Runner.Remaining(ability);
                    bool active=equipment.Runner.Current!=null&&equipment.Runner.Current.Definition==ability;
                    Ability(left+i*108,height-115,ability.IsPassive?"PASIVA":keys[i],ability.DisplayName,ability.IsPassive?"EQUIPADA":combat!=null&&combat.Focus<ability.focusCost?"FOCUS "+ability.focusCost.ToString("0"):Status(remaining,active),ability.cooldown>0?remaining/ability.cooldown:0,ability.IsPassive||stamina==null||stamina.Current>=ability.staminaCost,QuietFantasyUI.AbilityIcon(ability));
                }
                if(equipment.ActiveDefinition.isBow) Label(new Rect(width*Equipment.WeaponAim.Viewport.x-12,height*(1-Equipment.WeaponAim.Viewport.y)-12,24,24),"+",22,Gold,TextAnchor.MiddleCenter);
            }
            Ability(left+432,height-115,"C",dash!=null?dash.DisplayName.ToUpperInvariant():"ESPECIAL",Status(dash!=null?dash.CooldownRemaining:0,dash!=null&&dash.IsActive),dash!=null&&dash.CooldownDuration>0?dash.CooldownRemaining/dash.CooldownDuration:0,true);
            float mx=width-228;
            Fill(new Rect(mx-5,24,209,236),Panel);
            if(map!=null)GUI.DrawTexture(new Rect(mx,29,199,199),map);
            Matrix4x4 mapMatrix=GUI.matrix;
            var motor=GetComponent<PlayerMotor>();Vector3 facing=motor!=null?motor.Facing:transform.forward;
            // Compose in HUD coordinates before the screen scale. RotateAroundPivot
            // mixes its pivot with the existing GUI matrix at non-reference resolutions.
            float heading=Mathf.Atan2(facing.x,facing.z * .824f)*Mathf.Rad2Deg;
            GUI.matrix=mapMatrix*Matrix4x4.TRS(new Vector3(mx+99.5f,128.5f,0),Quaternion.Euler(0,0,heading),Vector3.one);
            Label(new Rect(-15,-15,30,30),"▲",24,Gold,TextAnchor.MiddleCenter);GUI.matrix=mapMatrix;
            Label(new Rect(mx+79,29,40,22),"N",15,Color.white,TextAnchor.MiddleCenter);
            Label(new Rect(mx,231,199,22),"ALREDEDORES · 90 m",12,Muted,TextAnchor.MiddleCenter);
            if(health.IsDead)
            {Fill(new Rect(width/2-210,height/2-46,420,92),Panel);Label(new Rect(width/2-200,height/2-36,400,40),"HAS CAÍDO",26,Gold,TextAnchor.MiddleCenter);Label(new Rect(width/2-200,height/2+5,400,30),"Regresando al pueblo…",16,Color.white,TextAnchor.MiddleCenter);}
            GUI.matrix=old;
        }
        private static string Status(float cooldown,bool active)=>active?"ACTIVO":cooldown>.01f?cooldown.ToString("0.0")+" s":"LISTO";
        private void Ability(float x,float y,string key,string name,string status,float cooldown,bool affordable,string icon="sprint")
        {
            Fill(new Rect(x,y,96,88),QuietFantasyUI.Surface);
            QuietFantasyUI.DrawIcon(new Rect(x+28,y+15,43,43),icon,affordable?QuietFantasyUI.Ink:QuietFantasyUI.Muted);
            if(cooldown>0)Fill(new Rect(x,y+88*(1-Mathf.Clamp01(cooldown)),96,88*Mathf.Clamp01(cooldown)),new Color(.17f,.20f,.24f,.85f));
            Label(new Rect(x+6,y+2,84,19),key,13,QuietFantasyUI.Amber);
            Label(new Rect(x+3,y+58,90,17),name,12,QuietFantasyUI.Ink,TextAnchor.MiddleCenter);
            Label(new Rect(x+3,y+74,90,14),!affordable&&status=="LISTO"?"SIN STAMINA":status,10,affordable?QuietFantasyUI.Muted:new Color(1,.4f,.35f),TextAnchor.MiddleCenter);
            QuietFantasyUI.Border(new Rect(x,y,96,88),status=="ACTIVO"?QuietFantasyUI.Amber:QuietFantasyUI.Rule);
        }
        private void OnDisable(){if(Active==this)Active=null;}
        private void OnDestroy(){if(combat!=null)combat.Rewarded-=OnReward;if(mapCamera!=null)Destroy(mapCamera.gameObject);if(map!=null){map.Release();Destroy(map);}}
    }
}
