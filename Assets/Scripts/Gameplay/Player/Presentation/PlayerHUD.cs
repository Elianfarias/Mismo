using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.Dash;
using Mismo.Gameplay.Player.Movement;
using UnityEngine;

namespace Mismo.Gameplay.Player.Presentation
{
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
            text.fontSize=size;text.fontStyle=FontStyle.Normal;text.alignment=alignment;text.normal.textColor=color;GUI.Label(rect,value,text);
        }
        private void Bar(float x,float y,float width,float value,Color color)
        {Fill(new Rect(x,y,width,12),new Color(.11f,.14f,.17f));Fill(new Rect(x,y,width*Mathf.Clamp01(value),12),color);}
        private void OnGUI()
        {
            if (Equipment.Inventory.InventoryPanel.AnyOpen || GetComponent<World.GatheringPlayer>()?.BlocksGameplay==true) return;
            if(health==null)return;
            Matrix4x4 old=GUI.matrix;float scale=Scale;GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);
            float width=Screen.width/scale,height=Screen.height/scale;
            Fill(new Rect(24,24,300,132),Panel);Fill(new Rect(24,24,3,132),Gold);
            Label(new Rect(43,34,260,22),"EXPLORADOR",16,Gold);
            Label(new Rect(43,63,260,20),"VIDA    "+Mathf.CeilToInt(health.Current)+" / "+health.Maximum.ToString("0"),14,Color.white);
            Bar(43,88,262,health.Normalized,new Color(.83f,.26f,.30f));
            Label(new Rect(43,107,260,19),"STAMINA    "+(stamina!=null?stamina.Current.ToString("0")+" / "+stamina.Maximum.ToString("0"):"—"),12,Muted);
            Bar(43,131,262,stamina!=null?stamina.Normalized:0,new Color(.29f,.73f,.52f));
            if(combat!=null)
            {
                Fill(new Rect(24,162,300,57),Panel);
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
                    var ability=equipment.ActiveDefinition.GetAbility((Equipment.AbilitySlot)i);
                    if(ability==null)continue;
                    float remaining=equipment.Runner.Remaining(ability);
                    bool active=equipment.Runner.Current!=null&&equipment.Runner.Current.Definition==ability;
                    Ability(left+i*108,height-115,keys[i],ability.displayName,combat!=null&&combat.Focus<ability.focusCost?"FOCUS "+ability.focusCost.ToString("0"):Status(remaining,active),ability.cooldown>0?remaining/ability.cooldown:0,stamina==null||stamina.Current>=ability.staminaCost);
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
        private void Ability(float x,float y,string key,string name,string status,float cooldown,bool affordable)
        {
            Fill(new Rect(x,y,96,88),Panel);
            if(cooldown>0)Fill(new Rect(x,y+88*(1-Mathf.Clamp01(cooldown)),96,88*Mathf.Clamp01(cooldown)),new Color(.17f,.20f,.24f,.85f));
            Fill(new Rect(x,y,96,2),status=="ACTIVO"?Color.white:Gold);
            Label(new Rect(x+10,y+7,76,22),key,18,Gold);Label(new Rect(x+10,y+34,80,19),name,12,Color.white);
            Label(new Rect(x+10,y+60,82,17),!affordable&&status=="LISTO"?"SIN STAMINA":status,11,affordable?Muted:new Color(1,.4f,.35f));
        }
        private void OnDisable(){if(Active==this)Active=null;}
        private void OnDestroy(){if(combat!=null)combat.Rewarded-=OnReward;if(mapCamera!=null)Destroy(mapCamera.gameObject);if(map!=null){map.Release();Destroy(map);}}
    }
}
