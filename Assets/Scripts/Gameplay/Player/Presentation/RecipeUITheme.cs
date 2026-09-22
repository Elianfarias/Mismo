using System;
using UnityEngine;
namespace Mismo.Gameplay.Player.Presentation
{
    /// <summary>Explicit references to the approved recipe and radial artwork.</summary>
    public sealed class RecipeUITheme:ScriptableObject
    {
        [Serializable] public sealed class Piece { public string key; public Texture2D texture; public Vector4 border; }
        public Piece[] pieces=Array.Empty<Piece>();
        public Texture2D radialSurface,radialOutline;
        public Texture2D[] radialSelected=new Texture2D[7];
        public Texture2D Texture(string key)
        {
            foreach(var p in pieces)if(p!=null&&p.key==key)return p.texture;
            return null;
        }
        public Vector4 Border(string key)
        {
            foreach(var p in pieces)if(p!=null&&p.key==key)return p.border;
            return Vector4.zero;
        }
        public static RecipeUITheme Load()=>Mismo.Core.ProjectAssets.Load<RecipeUITheme>("UI/Recipes");
    }
}
