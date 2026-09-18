using System.Collections.Generic;
using UnityEngine;

namespace Mismo.Gameplay.Player.Presentation
{
    public sealed class DamageNumbers : MonoBehaviour
    {
        struct Entry { public Vector3 position; public float born; public string text; public Color color; }
        static DamageNumbers instance;
        readonly List<Entry> entries = new List<Entry>();
        GUIStyle style;
        public static int ActiveCount => instance != null ? instance.entries.Count : 0;
        public static void Show(Vector3 position, float amount, bool player)
        {
            if (amount <= 0) return;
            if (instance == null) instance = new GameObject("Damage numbers").AddComponent<DamageNumbers>();
            if (instance.entries.Count >= 64) instance.entries.RemoveAt(0);
            instance.entries.Add(new Entry { position = position + Vector3.right * Random.Range(-.2f,.2f),
                born = Time.time, text = amount.ToString("0.#"), color = player ? new Color(1,.35f,.3f) : new Color(1,.88f,.25f) });
        }
        void Update() => entries.RemoveAll(e => Time.time - e.born > 1f);
        void OnGUI()
        {
            if (Equipment.Inventory.InventoryPanel.AnyOpen) return;
            var camera = UnityEngine.Camera.main; if (camera == null) return;
            if (style == null) style = new GUIStyle(GUI.skin.label){font=Mismo.Gameplay.Player.Presentation.QuietFantasyUI.Body, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            foreach (var entry in entries)
            {
                float age = Time.time - entry.born;
                var point = camera.WorldToScreenPoint(entry.position + Vector3.up * age * .9f);
                if (point.z <= 0) continue;
                style.fontSize = Mathf.RoundToInt(27 * PlayerHUD.Scale * (1 + .25f * Mathf.Clamp01(1-age*5)));
                var rect = new Rect(point.x-60, Screen.height-point.y-25,120,50);
                float alpha = Mathf.Clamp01((1-age)*3);
                style.normal.textColor = new Color(0,0,0,alpha);
                for (int x=-1;x<=1;x++) for(int y=-1;y<=1;y++) if(x!=0||y!=0) GUI.Label(new Rect(rect.x+x*2,rect.y+y*2,rect.width,rect.height),entry.text,style);
                var color = entry.color; color.a = alpha; style.normal.textColor = color; GUI.Label(rect,entry.text,style);
            }
        }
        void OnDestroy() { if (instance == this) instance = null; }
    }
}
