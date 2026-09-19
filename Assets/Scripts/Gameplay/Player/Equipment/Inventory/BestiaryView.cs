using UnityEngine;
using Mismo.Gameplay.Player.Presentation;
using U=Mismo.Gameplay.Player.Presentation.QuietFantasyUI;
using Mismo.Gameplay.Player.World;
using L=Mismo.Gameplay.Player.Localization.GameLanguage;
namespace Mismo.Gameplay.Player.Equipment.Inventory
{
    public sealed class BestiaryView
    {
        int page,last=-1;
        public void Reset()=>last=-1;
        public void Draw(PlayerInventory inventory,InventoryPreview preview,InventoryUIIcons icons=null,System.Action<Rect> drawPreview=null)
        {
            var book=Mismo.Core.ProjectAssets.Load<BestiaryBook>("BestiaryBook");if(book==null||book.pages==null||book.pages.Length==0)return;
            var pages=System.Array.FindAll(book.pages,s=>s!=null&&inventory.HasSeenSpecies(s.id));
            if(pages.Length==0){U.Text(new Rect(200,280,860,150),"Todavía no descubriste ninguna criatura. Explorá el mundo para completar el bestiario.",26,U.Muted,false,TextAnchor.MiddleCenter);return;}
            page=Mathf.Clamp(page,0,pages.Length-1);var species=pages[page];
            if(last!=page)
            {
                // Mark this page attempted even if the asset is absent or invalid.
                // Otherwise OnGUI would allocate and fail again on every event.
                last=page;
                try
                {
                    var source=species.prefabs==null?null:System.Array.Find(species.prefabs,p=>p!=null);
                    preview.Show(source,new Vector3(0,150,0));preview.Zoom(species.bookZoom);
                }
                catch(System.Exception exception)
                {
                    preview.Dispose();
                    Debug.LogWarning("Bestiary preview unavailable for "+species.id+": "+exception.Message);
                }
            }
            PlayerHUD.Fill(new Rect(625,160,1,470),U.Rule);
            var label=new GUIStyle(GUI.skin.label){font=U.Body,fontSize=23,wordWrap=true};label.normal.textColor=U.Ink;
            GUI.Label(new Rect(155,155,430,50),L.Text(species.displayName),label);
            var modelRect=new Rect(155,220,420,335);
            if(preview.HasModel)
            {
                if(drawPreview!=null)drawPreview(modelRect);
                else GUI.DrawTexture(modelRect,preview.Texture,ScaleMode.ScaleToFit,true);
            }
            else U.Text(modelRect,"Vista previa no disponible",20,U.Muted,false,TextAnchor.MiddleCenter);
            GUI.Label(new Rect(670,160,440,150),L.Text(species.description),label);
            int defeated=inventory.SpeciesDefeats(species.id);label.fontSize=20;
            GUI.Label(new Rect(670,325,435,45),L.Format("Derrotados: {0}",defeated),label);
            GUI.Label(new Rect(670,375,435,70),!species.domesticable?L.Text("No domesticable"):L.Format("Reconocimiento al vencer: {0:0.#}%",species.recognitionChance*100),label);
            GUI.Label(new Rect(670,455,435,55),L.Text(species.mountable?"Apta como montura":"Sin montura"),label);
            bool enabled=GUI.enabled;GUI.enabled=enabled&&pages.Length>1;
            if(PageButton(new Rect(485,660,52,45),icons?.previousPage,"←","Anterior",icons)){page=(page+pages.Length-1)%pages.Length;last=-1;}
            U.Text(new Rect(550,662,170,40),(page+1)+" / "+pages.Length,21,U.Muted,false,TextAnchor.MiddleCenter);
            if(PageButton(new Rect(735,660,52,45),icons?.nextPage,"→","Siguiente",icons)){page=(page+1)%pages.Length;last=-1;}
            GUI.enabled=enabled;
        }
        static bool PageButton(Rect rect,Texture2D icon,string fallback,string caption,InventoryUIIcons icons)
        {
            bool clicked=U.Button(rect,icon==null?fallback:"");
            if(icon!=null)
            {
                var previous=GUI.color;var tint=U.Ink;tint.a*=icons!=null?Mathf.Clamp01(icons.navigationIconOpacity):1;
                GUI.color=tint;GUI.DrawTexture(new Rect(rect.center.x-16,rect.center.y-16,32,32),MapIcons.Mask(icon),ScaleMode.ScaleToFit);GUI.color=previous;
            }
            GUI.Label(rect,new GUIContent("",L.Text(caption)),GUIStyle.none);return clicked;
        }
    }
}



