using System.Collections.Generic;
using UnityEngine;

namespace Mismo.Gameplay.Player.Presentation
{
    /// <summary>Bounded, cosmetic damage numbers. Animation continues through hit stop, but freezes in menus.</summary>
    public sealed class DamageNumbers : MonoBehaviour
    {
        struct Entry
        {
            public Vector3 position;
            public float age, strength, side, lane;
            public string text;
            public Color color;
        }
        static DamageNumbers instance;
        readonly List<Entry> entries = new List<Entry>();
        GUIStyle style;
        int sequence;
        const float Lifetime = 1.05f;
        public static int ActiveCount => instance != null ? instance.entries.Count : 0;
        static bool Hidden => GameplayPause.BlocksInput || Equipment.Inventory.InventoryPanel.AnyOpen || WorldMapPanel.BlocksGameplay;
        public static void Show(Vector3 position, float amount, bool player, float strength = 0)
        {
            if (amount <= 0 || float.IsNaN(amount) || float.IsInfinity(amount)) return;
            if (instance == null) instance = new GameObject("Damage numbers").AddComponent<DamageNumbers>();
            if (instance.entries.Count >= 64) instance.entries.RemoveAt(0);
            int index = instance.sequence++;
            strength = Mathf.Clamp01(strength);
            instance.entries.Add(new Entry {
                position = position, text = amount.ToString("0.#"), strength = strength,
                side = (index % 2 == 0 ? -1 : 1), lane = index % 3,
                color = player ? new Color(1,.34f,.36f) : Color.Lerp(new Color(.97f,.97f,.92f),new Color(1,.57f,.3f),strength)
            });
        }
        void Update()
        {
            if (Hidden) return;
            for (int i=entries.Count-1; i>=0; i--)
            {
                var entry=entries[i]; entry.age+=Time.unscaledDeltaTime;
                if(entry.age>=Lifetime)entries.RemoveAt(i);else entries[i]=entry;
            }
        }
        public static float PopScale(float age)
        {
            if(age<.075f)return Mathf.Lerp(.62f,1.35f,Mathf.Clamp01(age/.075f));
            return Mathf.Lerp(1.35f,1,Mathf.SmoothStep(0,1,(age-.075f)/.17f));
        }
        void OnGUI()
        {
            if (Hidden) return;
            var camera = UnityEngine.Camera.main; if (camera == null) return;
            if (style == null) style = new GUIStyle(GUI.skin.label){font=QuietFantasyUI.Body, alignment=TextAnchor.MiddleCenter, fontStyle=FontStyle.Bold};
            var previous=GUI.matrix;var previousColor=GUI.color;GUI.color=Color.white;
            foreach (var entry in entries)
            {
                var point=camera.WorldToScreenPoint(entry.position);
                if(point.z<=0)continue;
                float t=entry.age/Lifetime;
                float scale=PlayerHUD.Scale;
                float travel=1-Mathf.Pow(1-t,2);
                float x=point.x+entry.side*(10+entry.lane*9+travel*35)*scale;
                float y=Screen.height-point.y-(Mathf.Sin(t*Mathf.PI*.85f)*48+t*18+entry.lane*10)*scale;
                float size=PopScale(entry.age)*(1+entry.strength*.24f)*scale;
                float tilt=entry.side*8*(1-Mathf.Clamp01(entry.age/.3f));
                GUI.matrix=previous*Matrix4x4.TRS(new Vector3(x,y,0),Quaternion.Euler(0,0,tilt),Vector3.one*size);
                style.fontSize=29;
                var rect=new Rect(-110,-30,220,60);
                float alpha=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.62f,Lifetime,entry.age));
                style.normal.textColor=new Color(.025f,.025f,.035f,alpha*.95f);
                for(int dx=-1;dx<=1;dx++)for(int dy=-1;dy<=1;dy++)
                    if(dx!=0||dy!=0)GUI.Label(new Rect(rect.x+dx*1.5f,rect.y+dy*1.5f,rect.width,rect.height),entry.text,style);
                var color=Color.Lerp(entry.color,Color.white,Mathf.Clamp01(1-entry.age/.12f)*.65f);
                color.a=alpha;style.normal.textColor=color;GUI.Label(rect,entry.text,style);
            }
            GUI.matrix=previous;GUI.color=previousColor;
        }
        void OnDestroy() { if (instance == this) instance = null; }
    }
}
