using System;
using System.Collections.Generic;
using System.Linq;
using Mismo.Core;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Equipment.Inventory;
using Mismo.Gameplay.Player.World;
using UnityEngine;
using L=Mismo.Gameplay.Player.Localization.GameLanguage;
using U=Mismo.Gameplay.Player.Presentation.QuietFantasyUI;

namespace Mismo.Gameplay.Player.Presentation
{
    /// <summary>One recipe screen, shared by the radial and every crafting station.</summary>
    public sealed class RecipeBookView:IDisposable
    {
        public enum Filter { All, Weapons, Consumables, Materials, Upgrades }
        public Filter Category { get; set; }
        public CraftingRecipe Selected { get; private set; }
        public int WeaponSlot { get; set; }
        readonly InventoryPreview preview=new InventoryPreview();
        readonly Dictionary<string,GUIStyle> styles=new Dictionary<string,GUIStyle>();
        readonly Dictionary<MaterialDefinition,string> origins=new Dictionary<MaterialDefinition,string>();
        RecipeUITheme theme;
        GUIStyle controlStyle;
        GUISkin scrollSkin;
        InventoryUIIcons icons;
        CraftingRecipe[] all;
        GameObject previewSource;
        CraftingRecipe previewRecipe;
        Vector3 previewRotation;
        bool previewDirty;
        Vector2 scroll,ingredientScroll;
        string feedback;
        bool rotating;
        public static readonly Filter[] FilterOrder={Filter.All,Filter.Weapons,Filter.Materials,Filter.Consumables,Filter.Upgrades};
        static readonly string[] Filters={"Todos","Armas","Consumibles","Materiales","Mejoras"};
        public static bool Matches(CraftingRecipe recipe,Filter filter)
        {
            if(recipe==null)return false;
            if(filter==Filter.All)return true;
            if(recipe.upgradeWeapon)return filter==Filter.Upgrades;
            if(recipe.weaponResult!=null)return filter==Filter.Weapons;
            bool consumable=recipe.result!=null&&(recipe.result.IsConsumable||recipe.result.category==InventoryItemCategory.Consumable);
            return filter==(consumable?Filter.Consumables:Filter.Materials);
        }
        public static CraftingRecipe[] Available(CraftingStation station,CraftingRecipe[] known)
            =>(station!=null?station.recipes:known)?.Where(r=>r!=null).Distinct().OrderBy(r=>r.id,StringComparer.Ordinal).ToArray()??Array.Empty<CraftingRecipe>();
        GUIStyle Style(string name)
        {
            if(styles.TryGetValue(name,out var s))return s;
            var border=theme!=null?theme.Border(name):Vector4.zero;
            s=new GUIStyle{border=new RectOffset((int)border.x,(int)border.z,(int)border.w,(int)border.y)};
            s.normal.background=theme!=null?theme.Texture(name):null;
            styles[name]=s;return s;
        }
        void Piece(Rect rect,string key)
        {
            if(key=="WindowSurface"){FantasyUI.Surface(rect);return;}
            if(key=="SectionDividerHorizontal"||key=="SectionDividerVertical"){PlayerHUD.Fill(rect,U.Rule*.4f);return;}
            if(theme!=null&&theme.Texture(key)!=null)GUI.Box(rect,GUIContent.none,Style(key));
        }
        Texture2D Icon(string key)=>theme!=null?theme.Texture(key):null;
        static void DrawIcon(Rect rect,Texture2D texture,Color? color=null)
        {
            if(texture==null)return;
            var old=GUI.color;GUI.color=color??U.Ink;GUI.DrawTexture(rect,MapIcons.Mask(texture),ScaleMode.ScaleToFit);GUI.color=old;
        }
        bool Control(Rect rect,string text,Texture2D icon=null,bool selected=false)
        {
            bool clicked=U.Button(rect,text,selected);
            if(icon!=null)
            {
                float size=icons!=null?icons.navigationIconSize:24;
                var tint=GUI.enabled?U.Ink:U.Muted;tint.a*=icons!=null?icons.navigationIconOpacity:1;
                DrawIcon(new Rect(rect.x+12,rect.center.y-size/2,size,size),icon,tint);
            }
            return clicked;
        }
        public void Select(CraftingRecipe recipe){Selected=recipe;feedback=null;ingredientScroll=Vector2.zero;}
        public void Render()
        {
            // Camera.Render must run outside OnGUI to avoid nested URP rendering.
            if(previewDirty){previewDirty=false;preview.Show(previewSource,previewRotation);}
            else preview.Render();
        }
        public void Dispose(){preview.Dispose();previewSource=null;previewRecipe=null;previewDirty=false;rotating=false;if(scrollSkin!=null)UnityEngine.Object.Destroy(scrollSkin);scrollSkin=null;}
        Vector2 BeginScroll(Rect viewport,Vector2 position,Rect content)
        {
            var previous=GUI.skin;
            if(scrollSkin==null)
            {
                scrollSkin=UnityEngine.Object.Instantiate(previous);scrollSkin.hideFlags=HideFlags.HideAndDontSave;
                var track=new GUIStyle(Style("ScrollTrack")){name="verticalscrollbar",fixedWidth=14,stretchHeight=true};
                var thumb=new GUIStyle(Style("ScrollThumb")){name="verticalscrollbarthumb",fixedWidth=14};
                thumb.hover.background=thumb.normal.background;thumb.active.background=thumb.normal.background;
                scrollSkin.verticalScrollbar=track;scrollSkin.verticalScrollbarThumb=thumb;
                scrollSkin.verticalScrollbarUpButton=new GUIStyle{name="verticalscrollbarupbutton",fixedHeight=0};
                scrollSkin.verticalScrollbarDownButton=new GUIStyle{name="verticalscrollbardownbutton",fixedHeight=0};
                scrollSkin.customStyles=scrollSkin.customStyles.Where(s=>s!=null&&!s.name.StartsWith("verticalscrollbar",StringComparison.OrdinalIgnoreCase)).ToArray();
            }
            GUI.skin=scrollSkin;
            try{return GUI.BeginScrollView(viewport,position,content);}
            finally{GUI.skin=previous;}
        }
        static WeaponDefinition Weapon(PlayerInventory inventory,CraftingRecipe recipe,int slot)
            =>recipe?.upgradeWeapon==true?inventory.Definition(inventory.EquippedId(slot)):recipe?.weaponResult;
        static Sprite ItemIcon(PlayerInventory inventory,CraftingRecipe recipe,int slot)
            =>Weapon(inventory,recipe,slot)?.inventoryIcon??recipe?.result?.icon;
        static void Sprite(Rect rect,Sprite sprite)
        {
            if(sprite==null)return;
            float ratio=sprite.rect.width/sprite.rect.height,w=Mathf.Min(rect.width,rect.height*ratio),h=w/ratio;
            var uv=sprite.textureRect;var t=sprite.texture;
            GUI.DrawTextureWithTexCoords(new Rect(rect.center.x-w/2,rect.center.y-h/2,w,h),t,new Rect(uv.x/t.width,uv.y/t.height,uv.width/t.width,uv.height/t.height));
        }
        string Origin(MaterialDefinition material)
        {
            if(origins.TryGetValue(material,out string text))return text;
            var node=ProjectAssets.LoadAll<ResourceNodeDefinition>("Gathering").FirstOrDefault(n=>n.rewards?.entries?.Any(e=>e!=null&&e.material==material)==true);
            if(node!=null)text=node.displayName;
            else if(all.Any(r=>r!=null&&r.result==material))text="Fabricación";
            else if(ProjectAssets.Load<GatheringSettings>("GatheringSettings")?.monsterLoot?.entries?.Any(e=>e!=null&&e.material==material)==true)text="Criaturas";
            else text="Exploración";
            origins[material]=text;return text;
        }
        public void Draw(PlayerInventory inventory,CraftingStation station,Action close,Action backToMenu=null)
        {
            if(theme==null){theme=RecipeUITheme.Load();styles.Clear();controlStyle=null;}
            if(icons==null)icons=ProjectAssets.Load<InventoryUIIcons>("InventoryUIIcons");
            if(all==null)all=ProjectAssets.LoadAll<CraftingRecipe>("Recipes");
            var recipes=Available(station,all);var visible=recipes.Where(r=>inventory.KnowsRecipe(r)&&Matches(r,Category)).ToArray();
            if(!visible.Contains(Selected))Select(visible.FirstOrDefault());
            U.MenuBackground(icons);
            U.MenuDivider();
            U.MenuHeading(icons?.radialRecipes??Icon("IconRecipes"),"Recetas"); if(backToMenu!=null&&U.NavigationButton(U.MenuBackButton,icons?.menu,"Menú [B]",icons)){backToMenu();return;}
            if(U.CloseButton(U.MenuCloseButton)){close();return;}
            Texture2D[] filters={icons?.all,icons?.weapons,icons?.consumables,icons?.materials,icons?.upgrades};
            for(int i=0;i<Filters.Length;i++)if(U.NavigationButton(new Rect(58+i*108,104,104,44),filters[(int)FilterOrder[i]],Filters[(int)FilterOrder[i]],icons,Category==FilterOrder[i]))
            {Category=FilterOrder[i];scroll=Vector2.zero;Select(null);return;}
            DrawList(inventory,visible);
            Piece(new Rect(624,168,5,379),"SectionDividerVertical");
            if(Selected!=null)DrawDetail(inventory,station);
            else U.Text(new Rect(660,280,510,70),"No hay recetas en esta categoría.",23,U.Muted,false,TextAnchor.MiddleCenter);
            Piece(new Rect(58,568,1164,5),"SectionDividerHorizontal");
            U.Text(new Rect(58,587,240,25),"MATERIALES",19,U.Ink,true);
            U.Text(new Rect(316,589,320,25),"Tienes / Necesitas",16,U.Muted);
            if(Selected!=null)
            {
                DrawIngredients(inventory);
                string reason=inventory.CraftBlockReason(Selected,inventory.EquippedId(WeaponSlot),station,station==null);
                // In the field, station-only recipes remain readable in the book.
                bool showCraft=station!=null||Selected.craftInWorld&&!Selected.upgradeWeapon;
                if(showCraft)
                {
                    bool enabled=GUI.enabled;GUI.enabled=enabled&&reason==null;
                    if(Control(new Rect(963,636,258,53),Selected.upgradeWeapon?"Mejorar":"Fabricar ×"+Selected.quantity,Icon("IconCraft")))
                    {
                        bool ok=station!=null?inventory.TryCraft(Selected,inventory.EquippedId(WeaponSlot),station):inventory.TryCraftInWorld(Selected);
                        feedback=ok?inventory.Notice:inventory.CraftBlockReason(Selected,inventory.EquippedId(WeaponSlot),station,station==null)??"No se pudo guardar. Intentá de nuevo.";
                    }
                    GUI.enabled=enabled;
                }
                else DrawIcon(new Rect(1073,645,32,32),Icon("IconLock"),U.Muted);
                U.Text(new Rect(947,701,290,57),feedback??reason??"",16,reason==null?U.Ink:U.Muted,false,TextAnchor.UpperCenter);
            }
        }
        void DrawList(PlayerInventory inventory,CraftingRecipe[] recipes)
        {
            var viewport=new Rect(58,168,548,379);
            scroll=BeginScroll(viewport,scroll,new Rect(0,0,526,Mathf.Max(viewport.height,recipes.Length*67)));
            for(int i=0;i<recipes.Length;i++)
            {
                var recipe=recipes[i];var rect=new Rect(0,i*67,524,65);
                bool chosen=Selected==recipe;
                if(chosen)Piece(rect,"RowSelected");else if(rect.Contains(Event.current.mousePosition))Piece(rect,"RowHover");
                if(GameAudio.Button(rect,"",GUIStyle.none))Select(recipe);
                var itemIcon=ItemIcon(inventory,recipe,WeaponSlot);
                if(itemIcon!=null)Sprite(new Rect(14,rect.y+9,44,44),itemIcon);
                else DrawIcon(new Rect(20,rect.y+15,32,32),recipe.upgradeWeapon?Icon("IconCraft"):recipe.weaponResult!=null?icons?.weapons:recipe.result?.IsConsumable==true?icons?.consumables:icons?.materials);
                U.Text(new Rect(75,rect.y+7,435,27),recipe.displayName,20,chosen?U.Amber:U.Ink);
                bool hand=recipe.craftInWorld&&!recipe.upgradeWeapon;
                DrawIcon(new Rect(75,rect.y+38,18,18),Icon(hand?"IconHandcraft":"IconStation"),U.Muted);
                U.Text(new Rect(103,rect.y+36,396,24),hand?"A mano":"Mesa de crafteo",16,U.Muted);
                if(!chosen)Piece(new Rect(74,rect.y+63,447,3),"RowSeparator");
            }
            GUI.EndScrollView();
        }
        void DrawDetail(PlayerInventory inventory,CraftingStation station)
        {
            U.Text(new Rect(650,165,564,42),Selected.displayName,27,U.Ink,true,TextAnchor.MiddleCenter);
            var weapon=Weapon(inventory,Selected,WeaponSlot);
            var source=weapon!=null?weapon.visualPrefab:Selected.result?.pickupPrefab;
            if(Event.current.type==EventType.Repaint&&(source!=previewSource||Selected!=previewRecipe))
            {
                previewRotation=weapon!=null?weapon.inventoryPreviewRotation:Selected.result?.inventoryPreviewRotation??Vector3.zero;
                previewSource=source;previewRecipe=Selected;previewDirty=true;
            }
            var rect=new Rect(690,217,480,225);
            if(preview.HasModel)GUI.DrawTexture(rect,preview.Texture,ScaleMode.ScaleToFit,true);
            else
            {
                var sprite=ItemIcon(inventory,Selected,WeaponSlot);
                if(sprite!=null)Sprite(new Rect(812,230,220,200),sprite);
                else DrawIcon(new Rect(842,247,160,160),Selected.weaponResult!=null?icons?.weapons:Selected.upgradeWeapon?Icon("IconCraft"):Selected.result?.IsConsumable==true?icons?.consumables:icons?.materials);
            }
            var e=Event.current;
            if(e.type==EventType.MouseDown&&e.button==0&&rect.Contains(e.mousePosition)){rotating=true;e.Use();}
            if(e.type==EventType.MouseDrag&&rotating){preview.Rotate(e.delta);e.Use();}
            if(e.type==EventType.MouseUp)rotating=false;
            string description=Selected.description;
            if(Selected.upgradeWeapon)
            {
                var current=inventory.Item(inventory.EquippedId(WeaponSlot));
                if(current!=null&&current.tier==Selected.fromTier)
                {
                    var after=current.Copy();after.tier=Selected.toTier;
                    var a=inventory.Rules.Bonuses(current);var b=inventory.Rules.Bonuses(after);
                    description=L.Format("Daño {0:+0.#;-0.#;0}% · Vel. {1:+0.#;-0.#;0}% · Vida {2:+0.#;-0.#;0} · Armadura {3:+0.#;-0.#;0}",(b.damage-a.damage)*100,(b.speed-a.speed)*100,b.life-a.life,b.armor-a.armor);
                }
            }
            U.Text(new Rect(655,447,553,52),description,18,U.Muted,false,TextAnchor.UpperCenter);
            if(Selected.upgradeWeapon)
            {
                if(Control(new Rect(663,509,540,42),"T"+Selected.fromTier+" → T"+Selected.toTier+" · "+inventory.ItemName(inventory.EquippedId(WeaponSlot))+" · "+(WeaponSlot+1),Icon("IconCraft")))WeaponSlot=1-WeaponSlot;
            }
            else
            {
                bool hand=Selected.craftInWorld;
                DrawIcon(new Rect(787,517,24,24),Icon(hand?"IconHandcraft":"IconStation"));
                U.Text(new Rect(821,515,367,31),hand?"Se fabrica a mano":"Requiere mesa de crafteo",18,U.Ink);
            }
        }
        void DrawIngredients(PlayerInventory inventory)
        {
            // Combine repeated entries so counts match the transaction's aggregated cost.
            var items=(Selected.ingredients??Array.Empty<RecipeIngredient>()).Where(i=>i?.material!=null)
                .GroupBy(i=>i.material).Select(g=>new{material=g.Key,amount=g.Sum(i=>(long)i.quantity)}).ToArray();
            ingredientScroll=BeginScroll(new Rect(58,618,874,132),ingredientScroll,new Rect(0,0,847,Mathf.Max(132,((items.Length+1)/2)*83)));
            for(int i=0;i<items.Length;i++)
            {
                var item=items[i];float x=(i%2)*423,y=(i/2)*83;
                if(item.material.icon!=null)Sprite(new Rect(x+3,y+3,57,57),item.material.icon);
                else DrawIcon(new Rect(x+10,y+10,43,43),icons?.materials);
                U.Text(new Rect(x+72,y,330,25),item.material.displayName,19,U.Ink);
                int count=inventory.MaterialCount(item.material.id);
                U.Text(new Rect(x+72,y+27,325,23),count+" / "+item.amount,19,count>=item.amount?new Color(.66f,.84f,.71f):new Color(.94f,.66f,.58f));
                DrawIcon(new Rect(x+72,y+55,16,16),Icon("IconLocation"),U.Muted);
                U.Text(new Rect(x+95,y+52,306,28),Origin(item.material),15,U.Muted);
            }
            GUI.EndScrollView();
        }
    }
}
