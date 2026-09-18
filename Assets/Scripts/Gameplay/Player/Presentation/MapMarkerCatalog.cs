using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mismo.Gameplay.Player.Presentation
{
    public enum MapSymbol { Flag, House, Chest, Danger, Resource, Portal, Center, Close, Confirm, Delete, Player }

    [Serializable]
    public sealed class MapMarkerType
    {
        public string id = "flag";
        public string label = "Lugar";
        public MapSymbol symbol;
        public Texture2D icon;
        public Color color = new Color(1, .76f, .25f);
        public bool available = true;
    }

    [CreateAssetMenu(menuName = "Mismo/Map/Marker catalog")]
    public sealed class MapMarkerCatalog : ScriptableObject
    {
        [Header("Apariencia del mapa")]
        [InspectorName("Opacidad del fondo"), Range(0f, 1f)]
        [Tooltip("Fondo del mapa abierto: 0 = transparente, 1 = opaco. Se puede ajustar mientras jugás.")]
        public float backgroundOpacity = .30f;
        [Header("Minimapa")]
        [InspectorName("Distancia visible (metros)"), Range(20f, 512f)]
        [Tooltip("Menor valor = más cerca y más detalle. Mayor valor = más terreno visible. Se aplica en vivo al minimapa.")]
        public float minimapDistance = 60f;
        [Header("Marcadores")]
        public Texture2D villageIcon;
        public List<MapMarkerType> types = Defaults();
        public static List<MapMarkerType> Defaults() => new List<MapMarkerType>
        {
            new MapMarkerType { id="flag", label="Lugar", symbol=MapSymbol.Flag },
            new MapMarkerType { id="chest", label="Tesoro", symbol=MapSymbol.Chest },
            new MapMarkerType { id="danger", label="Peligro", symbol=MapSymbol.Danger, color=new Color(1,.4f,.3f) },
            new MapMarkerType { id="resource", label="Recurso", symbol=MapSymbol.Resource, color=new Color(.4f,.9f,.65f) },
            new MapMarkerType { id="home", label="Refugio", symbol=MapSymbol.House }
        };
        public MapMarkerType Find(string id) => types.Find(t => t != null && t.id == id);
        public string ValidationError()
        {
            var ids = new HashSet<string>();
            foreach(var type in types)
                if(type == null || string.IsNullOrWhiteSpace(type.id) || !ids.Add(type.id))
                    return "Cada tipo necesita un ID único y no vacío. Conservá los IDs usados en partidas.";
            return null;
        }
    }

    [Serializable]
    public sealed class MapPin
    {
        public string id, typeId, name;
        public float x, z;
    }

    // Small procedural icons avoid font-dependent Unicode glyphs; custom textures are optional.
    public static class MapIcons
    {
        static readonly Dictionary<MapSymbol, Texture2D> cache = new Dictionary<MapSymbol, Texture2D>();
        static readonly Dictionary<Texture2D,Texture2D> masks=new Dictionary<Texture2D,Texture2D>();
        public static Texture2D Mask(Texture2D source)
        {
            if(source==null)return null;
            if(masks.TryGetValue(source,out var cached)&&cached!=null)return cached;
            var target=RenderTexture.GetTemporary(source.width,source.height,0,RenderTextureFormat.ARGB32);
            var previous=RenderTexture.active;Texture2D readable=null;
            try
            {
                Graphics.Blit(source,target);RenderTexture.active=target;
                readable=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false);
                readable.ReadPixels(new Rect(0,0,source.width,source.height),0,0);readable.Apply();
                var pixels=readable.GetPixels32();int minX=source.width,minY=source.height,maxX=0,maxY=0;
                for(int y=0;y<source.height;y++)for(int x=0;x<source.width;x++)if(pixels[y*source.width+x].a>16)
                {minX=Mathf.Min(minX,x);minY=Mathf.Min(minY,y);maxX=Mathf.Max(maxX,x);maxY=Mathf.Max(maxY,y);}
                if(minX>maxX){minX=minY=0;maxX=source.width-1;maxY=source.height-1;}
                int width=maxX-minX+1,height=maxY-minY+1,pad=Mathf.Max(width,height)/10;
                var result=new Texture2D(width+pad*2,height+pad*2,TextureFormat.RGBA32,false){name=source.name+" map mask",hideFlags=HideFlags.HideAndDontSave};
                var output=new Color32[result.width*result.height];
                for(int y=0;y<height;y++)for(int x=0;x<width;x++)output[(y+pad)*result.width+x+pad]=new Color32(255,255,255,pixels[(y+minY)*source.width+x+minX].a);
                result.SetPixels32(output);result.Apply();masks[source]=result;return result;
            }
            finally
            {
                RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);
                if(readable!=null){if(Application.isPlaying)UnityEngine.Object.Destroy(readable);else UnityEngine.Object.DestroyImmediate(readable);}
            }
        }
        public static Texture2D Get(MapSymbol symbol)
        {
            if(cache.TryGetValue(symbol, out var result) && result != null) return result;
            result = new Texture2D(48,48,TextureFormat.RGBA32,false) { name="Map "+symbol, hideFlags=HideFlags.HideAndDontSave, filterMode=FilterMode.Bilinear };
            var pixels = new Color[48*48];
            for(int y=0;y<48;y++) for(int x=0;x<48;x++)
            {
                float u=(x-23.5f)/19f, v=(y-23.5f)/19f;
                float ax=Mathf.Abs(u), ay=Mathf.Abs(v), r=Mathf.Sqrt(u*u+v*v);
                bool ink=false;
                switch(symbol)
                {
                    case MapSymbol.Center: ink=(r>.48f&&r<.61f)||(ax<.07f&&ay>.28f)||(ay<.07f&&ax>.28f); break;
                    case MapSymbol.Close: ink=Mathf.Abs(ax-ay)<.09f&&ax<.75f; break;
                    case MapSymbol.Confirm: ink=Distance(u,v,-.75f,0,-.2f,-.55f)<.09f||Distance(u,v,-.2f,-.55f,.8f,.65f)<.09f; break;
                    case MapSymbol.Delete: ink=(ax<.55f&&v>-.75f&&v<.5f&&(ax>.43f||v<-.63f))|| (Mathf.Abs(v-.62f)<.07f&&ax<.72f)||(ax<.2f&&v>.62f&&v<.85f)||(ax>.15f&&ax<.23f&&v>-.45f&&v<.28f); break;
                    case MapSymbol.House: ink=(Mathf.Abs(v-(.8f-ax))<.12f&&ax<.85f)||(ax<.62f&&v<.2f&&v>-.7f&&!(ax<.18f&&v<-.1f)); break;
                    case MapSymbol.Flag: ink=(u>-.65f&&u<-.5f&&ay<.85f)||(u>=-.5f&&u<.7f&&v>.1f+.12f*u&&v<.75f+.12f*u); break;
                    case MapSymbol.Chest: ink=ax<.8f&&ay<.55f&&(ax>.65f||ay>.4f||Mathf.Abs(v-.08f)<.07f||ax<.1f&&ay<.25f); break;
                    case MapSymbol.Resource: ink=ax+ay<.85f&&(ax+ay>.62f||ax<.06f); break;
                    case MapSymbol.Danger: ink=Distance(u,v,0,.85f,-.85f,-.7f)<.08f||Distance(u,v,0,.85f,.85f,-.7f)<.08f||(Mathf.Abs(v+.7f)<.08f&&ax<.85f)||(ax<.08f&&v>-.2f&&v<.4f)||(ax<.09f&&v>-.52f&&v<-.35f); break;
                    case MapSymbol.Portal: ink=(Mathf.Abs(Mathf.Sqrt(u*u*1.8f+v*v)-.75f)<.1f)||(Distance(u,v,-.65f,0,.55f,0)<.07f)||(Distance(u,v,.2f,.3f,.55f,0)<.07f)||(Distance(u,v,.2f,-.3f,.55f,0)<.07f); break;
                    case MapSymbol.Player: ink=v>-.7f&&v<.9f&&ax<(.9f-v)*.48f&&v>-.45f+ax*.3f; break;
                }
                pixels[y*48+x]=ink?Color.white:Color.clear;
            }
            result.SetPixels(pixels);result.Apply();cache[symbol]=result;return result;
        }
        static float Distance(float x,float y,float ax,float ay,float bx,float by)
        {var p=new Vector2(x-ax,y-ay);var d=new Vector2(bx-ax,by-ay);return (p-d*Mathf.Clamp01(Vector2.Dot(p,d)/d.sqrMagnitude)).magnitude;}
        public static bool Button(Rect rect, MapSymbol symbol, string tooltip, Color? color=null, Texture2D custom=null)
        {
            bool hover=rect.Contains(Event.current.mousePosition);
            PlayerHUD.Fill(rect,hover?new Color(.16f,.21f,.25f,.96f):new Color(.035f,.055f,.075f,.9f));
            if(hover)QuietFantasyUI.Border(rect,new Color(.8f,.72f,.48f,.8f));
            bool clicked=GUI.Button(rect,new GUIContent("",tooltip),GUIStyle.none);
            var old=GUI.color;GUI.color=color??Color.white;
            var imageRect=new Rect(rect.x+4,rect.y+4,rect.width-8,rect.height-8);
            GUI.DrawTexture(imageRect,custom!=null?Mask(custom):Get(symbol),ScaleMode.ScaleToFit);GUI.color=old;return clicked;
        }
    }
}



