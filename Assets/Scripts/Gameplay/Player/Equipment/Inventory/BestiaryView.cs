using UnityEngine;
using Mismo.Gameplay.Player.World;
using L=Mismo.Gameplay.Player.Localization.GameLanguage;
namespace Mismo.Gameplay.Player.Equipment.Inventory
{
    public sealed class BestiaryView
    {
        int page,last=-1;
        public void Reset()=>last=-1;
        public void Draw(PlayerInventory inventory,InventoryPreview preview)
        {
            var book=Resources.Load<BestiaryBook>("BestiaryBook");if(book==null||book.pages==null||book.pages.Length==0)return;
            var pages=System.Array.FindAll(book.pages,s=>s!=null&&inventory.HasSeenSpecies(s.id));
            if(pages.Length==0){Presentation.PlayerHUD.Fill(new Rect(120,130,1030,510),book.paper);var empty=new GUIStyle(GUI.skin.label){font=Mismo.Gameplay.Player.Presentation.QuietFantasyUI.Body,fontSize=26,wordWrap=true};empty.normal.textColor=book.ink;GUI.Label(new Rect(200,280,860,150),L.Text("Todavía no descubriste ninguna criatura. Explorá el mundo para completar el bestiario."),empty);return;}
            page=Mathf.Clamp(page,0,pages.Length-1);var species=pages[page];
            if(last!=page||!preview.HasModel){preview.Show(species.prefabs!=null&&species.prefabs.Length>0?species.prefabs[0]:null,new Vector3(0,150,0));preview.Zoom(species.bookZoom);last=page;}
            Presentation.PlayerHUD.Fill(new Rect(110,120,1050,530),new Color(.22f,.13f,.07f));
            Presentation.PlayerHUD.Fill(new Rect(120,130,505,510),book.paper);Presentation.PlayerHUD.Fill(new Rect(635,130,515,510),book.paper);
            var label=new GUIStyle(GUI.skin.label){font=Mismo.Gameplay.Player.Presentation.QuietFantasyUI.Body,fontSize=23,wordWrap=true};label.normal.textColor=book.ink;
            GUI.Label(new Rect(155,155,430,50),L.Text(species.displayName),label);
            if(preview.HasModel)GUI.DrawTexture(new Rect(155,220,420,335),preview.Texture,ScaleMode.ScaleToFit,true);
            GUI.Label(new Rect(670,160,440,150),L.Text(species.description),label);
            int defeated=inventory.SpeciesDefeats(species.id);label.fontSize=20;
            GUI.Label(new Rect(670,325,435,45),L.Format("Derrotados: {0}",defeated),label);
            GUI.Label(new Rect(670,375,435,70),!species.domesticable?L.Text("No domesticable"):L.Format("Reconocimiento al vencer: {0:0.#}%",species.recognitionChance*100),label);
            GUI.Label(new Rect(670,455,435,55),L.Text(species.mountable?"Apta como montura":"Sin montura"),label);
            GUI.Label(new Rect(670,520,435,100),species.domesticable?L.Text("Al vencerla, puede reconocerte como su amo y convertirse en tu montura."):"",label);
            var button=new GUIStyle(GUI.skin.button){font=Mismo.Gameplay.Player.Presentation.QuietFantasyUI.Body,fontSize=20};
            if(GameAudio.Button(new Rect(125,655,200,38),L.Text("← Anterior"),button)){page=(page+pages.Length-1)%pages.Length;last=-1;}
            label.normal.textColor=book.paper;GUI.Label(new Rect(550,655,200,38),(page+1)+" / "+pages.Length,label);
            if(GameAudio.Button(new Rect(945,655,200,38),L.Text("Siguiente →"),button)){page=(page+1)%pages.Length;last=-1;}
        }
    }
}



