using System;
using System.Collections.Generic;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using L = Mismo.Gameplay.Player.Localization.GameLanguage;

namespace Mismo.Gameplay.Player.Equipment.Inventory
{
    [DefaultExecutionOrder(-100)]
    public sealed class InventoryPanel : MonoBehaviour
    {
        enum Page { Menu, Inventory, Character, Skills, Bestiary, Mounts }
        int mountIndex;string mountPreviewId;Vector2 mountScroll;
        readonly BestiaryView bestiary=new BestiaryView();

        PlayerInventory inventory;
        EquipmentLoadout loadout;
        Health health;
        string selected,message,query="";
        bool selectedMaterial,showChest,confirmDiscard,previewDirty,searchFocused;
        int filter,amount=1,compareSlot;
        string selectedGrid,draggedGrid;
        bool dragRotated;
        Vector2 dragStart;
        Vector2Int dragOffset;
        float messageUntil;
        Vector2 scroll;
        Vector2 skillsScroll;
        int selectedSkillSlot=1;
        WeaponDefinition skillsWeapon;
        bool rotatingPreview;
        readonly Dictionary<Sprite,Texture2D> rotatedIcons=new Dictionary<Sprite,Texture2D>();
        Page page=Page.Inventory;
        GUIStyle label,button,field;
        readonly InventoryPreview preview=new InventoryPreview();
        CursorLockMode previousLock;
        bool previousVisible;
        int closedFrame=-1;
        static InventoryPanel active;
        public static bool AnyOpen=>active!=null&&active.IsOpen;
        public bool IsOpen {get;private set;}
        public bool BlocksGameplay=>IsOpen||closedFrame==Time.frameCount;
        static readonly Color Accent=new Color(.83f,.74f,.53f), Muted=new Color(.61f,.65f,.67f);
        void Awake()
        {
            inventory=GetComponent<PlayerInventory>();loadout=GetComponent<EquipmentLoadout>();health=GetComponent<Health>();
            inventory.Changed+=OnChanged;OnChanged();
        }
        void OnChanged(){message=inventory.Notice;messageUntil=Time.unscaledTime+5;previewDirty=true;}
        void Update()
        {
            if(IsOpen&&(health==null||health.IsDead)){Close();return;}
            if(showChest&&!inventory.AtChest){showChest=false;selected=null;confirmDiscard=false;previewDirty=true;}
            var k=Keyboard.current;
            bool typing=IsOpen&&searchFocused;
            if(k!=null)
            {
                if(k.escapeKey.wasPressedThisFrame&&IsOpen)
                {
                    if(draggedGrid!=null)draggedGrid=null;
                    else if(confirmDiscard)confirmDiscard=false;
                    else if(selected!=null&&page==Page.Inventory){selected=null;previewDirty=true;}
                    else Close();
                }
                else if(!typing&&k.iKey.wasPressedThisFrame){if(IsOpen&&page==Page.Inventory)Close();else OpenPage(Page.Inventory);}
                else if(!typing&&k.bKey.wasPressedThisFrame){if(IsOpen&&page==Page.Menu)Close();else OpenPage(Page.Menu);}
                else if(!typing&&k.pKey.wasPressedThisFrame)OpenPage(Page.Character);
                else if(!typing&&k.kKey.wasPressedThisFrame)OpenPage(Page.Skills);
            }
            if(IsOpen&&draggedGrid!=null&&k!=null&&k.rKey.wasPressedThisFrame)
            {
                var item=inventory.GridItems(showChest).Find(i=>i.key==draggedGrid);
                if(item!=null&&item.canRotate){dragRotated=!dragRotated;dragOffset=Vector2Int.zero;}
            }
            if(IsOpen&&previewDirty)
            {
                previewDirty=false;
                GameObject source=gameObject;
                if(page==Page.Bestiary||page==Page.Mounts){source=null;mountPreviewId=null;}
                if(page==Page.Inventory&&selected!=null)source=selectedMaterial?inventory.Material(selected)?.pickupPrefab:inventory.Definition(selected)?.visualPrefab;
                Vector3 rotation=page==Page.Inventory&&selected!=null?(selectedMaterial?inventory.Material(selected)?.inventoryPreviewRotation??Vector3.zero:inventory.Definition(selected)?.inventoryPreviewRotation??Vector3.zero):Vector3.zero;
                preview.Show(source,rotation);
            }
        }
        void LateUpdate(){if(IsOpen)preview.Render();}
        public bool TryOpen()=>OpenPage(Page.Inventory);
        bool OpenPage(Page target)
        {
            if(target==Page.Bestiary)bestiary.Reset();
            if(GetComponent<World.GatheringPlayer>()?.Busy==true)return false;
            if(WorldMapPanel.BlocksGameplay)return false;
            if(!IsOpen)
            {
                if(!inventory.IsReady||health==null||health.IsDead||loadout.Runner==null||loadout.Runner.IsBusy||
                    loadout.Belt!=null&&loadout.Belt.IsActive||!Mathf.Approximately(Time.timeScale,1))return false;
                previousLock=Cursor.lockState;previousVisible=Cursor.visible;
                Cursor.lockState=CursorLockMode.None;Cursor.visible=true;IsOpen=true;active=this;
            }
            if(target==Page.Skills&&page!=Page.Skills){skillsWeapon=loadout.ActiveDefinition;skillsScroll=Vector2.zero;}
            page=target;confirmDiscard=false;previewDirty=true;return true;
        }
        public bool OpenChest()
        {
            if(!inventory.AtChest||!inventory.CanManage||!OpenPage(Page.Inventory))return false;
            showChest=true;selected=null;draggedGrid=null;previewDirty=true;return true;
        }
        public void Close()
        {
            if(!IsOpen)return;
            IsOpen=false;closedFrame=Time.frameCount;if(active==this)active=null;
            Cursor.lockState=previousLock;Cursor.visible=previousVisible;confirmDiscard=false;showChest=false;draggedGrid=null;
            preview.Dispose();searchFocused=false;rotatingPreview=false;
        }
        void OnDisable()=>Close();
        void OnDestroy(){if(inventory!=null)inventory.Changed-=OnChanged;preview.Dispose();foreach(var icon in rotatedIcons.Values)Destroy(icon);rotatedIcons.Clear();}
        void Text(Rect rect,string value,int size=18,Color? color=null)
        {label.fontSize=size;label.normal.textColor=color??Color.white;GUI.Label(rect,L.Text(value),label);}
        bool Button(Rect rect,string value)
        {
            var old=GUI.backgroundColor;if(old==Color.white)GUI.backgroundColor=new Color(.13f,.16f,.18f);
            bool pressed=GUI.Button(rect,L.Text(value),button);GUI.backgroundColor=old;
            if(pressed){GUI.FocusControl(null);searchFocused=false;}return pressed;
        }
        bool Tab(Rect rect,string value,bool chosen)
        {
            var old=GUI.backgroundColor;GUI.backgroundColor=chosen?new Color(.55f,.49f,.35f):new Color(.13f,.16f,.18f);
            int size=button.fontSize;
            if(rect.width<110)button.fontSize=13;
            bool clicked=Button(rect,value);button.fontSize=size;GUI.backgroundColor=old;return clicked;
        }
        void Line(float x,float y,float width)=>PlayerHUD.Fill(new Rect(x,y,width,1),new Color(.55f,.56f,.53f,.4f));
        void OnGUI()
        {
            if(inventory==null||health==null||health.IsDead)return;
            if(label==null)
            {
                label=new GUIStyle(GUI.skin.label){wordWrap=true,padding=new RectOffset(0,0,0,0)};
                button=new GUIStyle(GUI.skin.button){fontSize=16,wordWrap=true,padding=new RectOffset(4,4,5,5)};
                button.normal.background=Texture2D.whiteTexture;button.normal.textColor=Color.white;
                button.hover.background=Texture2D.whiteTexture;button.hover.textColor=Accent;
                button.active.background=Texture2D.whiteTexture;button.active.textColor=Color.white;
                field=new GUIStyle(GUI.skin.textField){fontSize=20,padding=new RectOffset(12,12,7,7)};
            }
            var matrix=GUI.matrix;int depth=GUI.depth;
            if(IsOpen){GUI.depth=-40;PlayerHUD.Fill(new Rect(0,0,Screen.width,Screen.height),new Color(.015f,.022f,.03f,.86f));}
            float scale=Mathf.Min(Screen.width/1280f,Screen.height/800f);
            GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1280*scale)/2,(Screen.height-800*scale)/2,0),Quaternion.identity,Vector3.one*scale);
            GUI.depth=-40;
            if(!IsOpen)
            {
                Text(new Rect(28,756,650,32),L.Format("[B] MENÚ   [I] MOCHILA   {0} / {1}",inventory.UsedSlots(false),inventory.BackpackCapacity),17,Accent);
                if(Time.unscaledTime<messageUntil){PlayerHUD.Fill(new Rect(265,25,750,65),PlayerHUD.Panel);Text(new Rect(285,37,710,48),message,18,Accent);}
            }
            else
            {

                bool previousEnabled=GUI.enabled;GUI.enabled=previousEnabled&&!confirmDiscard;
                Text(new Rect(38,25,740,44),page==Page.Menu?"VIAJERO":page==Page.Inventory?"PERTENENCIAS":page==Page.Character?"PERSONAJE":page==Page.Bestiary?"BESTIARIO":page==Page.Mounts?"MONTURAS":"HABILIDADES",32,Accent);
                if(Button(new Rect(940,27,120,35),"Menú [B]"))OpenPage(Page.Menu);
                if(Button(new Rect(1075,27,165,35),"Cerrar [ESC]"))Close();
                Line(38,82,1202);

                if(page==Page.Menu)DrawMenu();
                else if(page==Page.Character)DrawProgression(40,0);
                else if(page==Page.Skills)DrawSkills();
                else if(page==Page.Bestiary)bestiary.Draw(inventory,preview);
                else if(page==Page.Mounts)DrawMounts();
                else DrawInventory();
                Line(38,710,1202);
                Text(new Rect(40,726,1180,30),L.Format("MOCHILA  {0} / {1} celdas",inventory.UsedSlots(false),inventory.BackpackCapacity)+
                    (inventory.AtChest?L.Format("     COFRE  {0} / {1}",inventory.UsedSlots(true),inventory.ChestCapacity):"")+L.Format("     ·     NIVEL {0}",inventory.Level),19,Accent);
                Text(new Rect(40,764,1190,30),inventory.Notice,16,inventory.HasSaveProblem?new Color(1,.55f,.3f):Muted);
                GUI.enabled=previousEnabled;
                if(confirmDiscard)DrawDiscardConfirmation();
            }
            GUI.matrix=matrix;GUI.depth=depth;
        }
        void DrawMenu()
        {
            if(Button(new Rect(40,105,300,38),L.LanguageLabel))L.Next();
            Text(new Rect(405,135,500,38),"PREPARÁ TU PRÓXIMA SALIDA",21,Muted);
            if(Button(new Rect(450,225,380,80),"PERSONAJE  [P]"))OpenPage(Page.Character);
            if(Button(new Rect(745,365,360,80),"INVENTARIO  [I]"))OpenPage(Page.Inventory);
            if(Button(new Rect(175,365,360,80),"HABILIDADES  [K]"))OpenPage(Page.Skills);
            if(Button(new Rect(450,505,380,80),"MAPA  [M]"))
            {
                var map=GetComponent<WorldMapPanel>();if(map!=null){Close();map.Open();}
            }
            if(Button(new Rect(300,602,310,38),"BESTIARIO"))OpenPage(Page.Bestiary);
            if(Button(new Rect(630,602,310,38),"MONTURAS"))OpenPage(Page.Mounts);
            Text(new Rect(450,650,500,32),"El mundo continúa mientras consultás el menú.",17,Muted);
        }
        void DrawMounts()
        {
            var mounts=inventory.Mounts;
            if(mounts.Length==0){Text(new Rect(100,260,1050,110),"Todavía no tenés monturas. Algunas criaturas pueden reconocerte al vencerlas.",25,Muted);return;}
            mountIndex=Mathf.Clamp(mountIndex,0,mounts.Length-1);
            mountScroll=GUI.BeginScrollView(new Rect(45,125,490,450),mountScroll,new Rect(0,0,465,mounts.Length*65));
            for(int i=0;i<mounts.Length;i++)
            {
                var sp=World.CreatureSpecies.Find(mounts[i].speciesId);
                if(Tab(new Rect(0,i*65,450,55),(sp!=null?L.Text(sp.displayName):mounts[i].speciesId)+" · "+(i+1),i==mountIndex))mountIndex=i;
            }
            GUI.EndScrollView();
            var mount=mounts[mountIndex];var species=World.CreatureSpecies.Find(mount.speciesId);
            if(mountPreviewId!=mount.id){preview.Show(species?.prefabs==null?null:System.Array.Find(species.prefabs,p=>p!=null&&p.name==mount.prefabName),new Vector3(0,150,0));preview.Zoom(species!=null?species.bookZoom:1);mountPreviewId=mount.id;}
            DrawPreview(new Rect(600,140,540,320));
            Text(new Rect(590,470,610,65),"Tus monturas permanecen en la colección aunque mueras o las guardes.",20,Muted);
            bool selectedActive=inventory.SelectedMountId==mount.id&&inventory.CompanionSummoned;
            Text(new Rect(590,540,600,30),selectedActive?"Acompañante activo":"Guardada en tu colección",20,Accent);
            bool enabled=GUI.enabled;GUI.enabled=enabled&&inventory.CanCallMount&&species!=null;
            if(Button(new Rect(590,585,280,42),selectedActive?"Invocar a mi lado":"Invocar montura"))inventory.SummonMount(mount.id);
            GUI.enabled=enabled&&inventory.CanCallMount&&inventory.CompanionSummoned;
            if(Button(new Rect(890,585,280,42),"Guardar acompañante"))inventory.DismissMount();GUI.enabled=enabled;
            if(!inventory.CanCallMount)Text(new Rect(590,640,600,42),"Desmontá y salí de combate para cambiar de acompañante.",17,Muted);
        }
        void DrawInventory()
        {
            if(Tab(new Rect(40,99,240,36),"Mochila",!showChest)){showChest=false;selected=null;draggedGrid=null;previewDirty=true;}
            bool enabled=GUI.enabled;GUI.enabled=inventory.AtChest;
            if(Tab(new Rect(290,99,240,36),inventory.AtChest?"Cofre personal":"Cofre · En el pueblo",showChest)){showChest=true;selected=null;draggedGrid=null;previewDirty=true;}
            GUI.enabled=enabled;
            string[] categories={"Todos","Armas","Materiales","Consumo","Favoritos"};
            for(int i=0;i<categories.Length;i++)if(Tab(new Rect(40+i*103,149,99,36),categories[i],filter==i)){filter=i;scroll=Vector2.zero;selected=null;previewDirty=true;}
            GUI.SetNextControlName("InventorySearch");query=GUI.TextField(new Rect(40,199,312,38),query,80,field);
            searchFocused=GUI.GetNameOfFocusedControl()=="InventorySearch";
            if(query.Length==0&&Event.current.type==EventType.Repaint)Text(new Rect(54,207,260,27),"Buscar objetos…",19,Muted);
            bool canOrganize=GUI.enabled;GUI.enabled=inventory.CanManage&&(!showChest||inventory.AtChest);
            if(Button(new Rect(364,199,190,38),"Organizar")){inventory.OrganizeGrid(showChest);draggedGrid=null;}
            GUI.enabled=canOrganize;
            Text(new Rect(42,250,515,34),"Arrastrá para mover · R para girar al arrastrar",16,Muted);
            DrawGrid();
            PlayerHUD.Fill(new Rect(592,100,648,585),new Color(.02f,.03f,.035f,.65f));
            if(selected==null)DrawCharacterPreview();else DrawItem();
        }
        bool Matches(GridItem item)
        {
            var material=item.material?inventory.Material(item.id):null;
            string name=item.material?(material!=null?material.displayName:item.id):inventory.Definition(item.id).DisplayName;
            return L.Text(name).IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0&&
                (filter==0||filter==1&&!item.material||filter==2&&item.material&&(material==null||material.category==InventoryItemCategory.Material)||
                filter==3&&material!=null&&material.category==InventoryItemCategory.Consumable||filter==4&&inventory.Favorite(item.id,item.material));
        }
        void DrawGrid()
        {
            int columns=inventory.GridColumns(showChest),rows=inventory.GridRows(showChest);
            float cell=496f/columns;
            var items=inventory.GridItems(showChest);var positions=inventory.GridPositions(showChest);
            var unplaced=items.FindAll(i=>!positions.Exists(p=>p.key==i.key));
            scroll=GUI.BeginScrollView(new Rect(40,291,520,396),scroll,new Rect(0,0,498,Mathf.Max(390,rows*cell+unplaced.Count*49+(unplaced.Count>0?38:0))));
            for(int y=0;y<rows;y++)for(int x=0;x<columns;x++)
            {
                PlayerHUD.Fill(new Rect(x*cell,y*cell,cell-2,cell-2),new Color(.08f,.105f,.115f,.95f));
                PlayerHUD.Fill(new Rect(x*cell+2,y*cell+2,cell-6,cell-6),new Color(.045f,.063f,.073f,.95f));
            }
            foreach(var p in positions)
            {
                var item=items.Find(i=>i.key==p.key);if(item==null)continue;
                var rect=new Rect(p.x*cell+1,p.y*cell+1,(p.rotated?item.height:item.width)*cell-3,(p.rotated?item.width:item.height)*cell-3);
                DrawGridItem(item,rect,p.rotated,Matches(item)?1:.22f);
                var e=Event.current;
                if(e.type==EventType.MouseDown&&e.button==0&&rect.Contains(e.mousePosition)&&!confirmDiscard)
                {
                    selected=item.id;selectedMaterial=item.material;selectedGrid=item.key;amount=1;previewDirty=true;
                    GUI.FocusControl(null);searchFocused=false;
                    if(inventory.CanManage&&(!showChest||inventory.AtChest))
                    {
                        draggedGrid=item.key;dragRotated=p.rotated;dragStart=e.mousePosition;
                        dragOffset=new Vector2Int(Mathf.FloorToInt((e.mousePosition.x-p.x*cell)/cell),Mathf.FloorToInt((e.mousePosition.y-p.y*cell)/cell));
                    }
                    e.Use();
                }
            }
            if(draggedGrid!=null)
            {
                var e=Event.current;var item=items.Find(i=>i.key==draggedGrid);
                if(item==null)draggedGrid=null;
                else
                {
                    int x=Mathf.FloorToInt(e.mousePosition.x/cell)-dragOffset.x,y=Mathf.FloorToInt(e.mousePosition.y/cell)-dragOffset.y;
                    bool valid=inventory.CanMoveGrid(item.key,showChest,x,y,dragRotated);
                    var ghost=new Rect(x*cell+1,y*cell+1,(dragRotated?item.height:item.width)*cell-3,(dragRotated?item.width:item.height)*cell-3);
                    PlayerHUD.Fill(ghost,valid?new Color(.22f,.7f,.45f,.45f):new Color(.9f,.2f,.15f,.45f));
                    if(e.type==EventType.MouseUp&&e.button==0)
                    {
                        if(valid&&(Vector2.Distance(e.mousePosition,dragStart)>3||positions.Find(p=>p.key==item.key)?.rotated!=dragRotated))inventory.MoveGrid(item.key,showChest,x,y,dragRotated);
                        draggedGrid=null;e.Use();
                    }
                }
            }
            if(unplaced.Count>0)
            {
                Text(new Rect(0,rows*cell+5,495,30),"SIN UBICAR · Guardá o descartá para liberar espacio",15,Accent);
                for(int i=0;i<unplaced.Count;i++)
                {
                    var item=unplaced[i];string name=item.material?(inventory.Material(item.id)?.displayName??item.id):inventory.Definition(item.id).DisplayName;
                    if(Button(new Rect(0,rows*cell+38+i*49,492,44),name+" ×"+item.quantity))
                    {selected=item.id;selectedMaterial=item.material;selectedGrid=item.key;previewDirty=true;}
                }
            }
            GUI.EndScrollView();
        }
        Texture2D RotatedIcon(Sprite sprite)
        {
            if(rotatedIcons.TryGetValue(sprite,out var cached))return cached;
            var source=sprite.texture;var crop=sprite.textureRect;
            int width=Mathf.RoundToInt(crop.width),height=Mathf.RoundToInt(crop.height);
            var target=RenderTexture.GetTemporary(source.width,source.height,0,RenderTextureFormat.ARGB32);
            var previous=RenderTexture.active;
            Texture2D readable=null;
            try
            {
                Graphics.Blit(source,target);RenderTexture.active=target;
                readable=new Texture2D(width,height,TextureFormat.RGBA32,false);
                readable.ReadPixels(crop,0,0);readable.Apply();
                var pixels=readable.GetPixels32();var rotated=new Color32[pixels.Length];
                for(int y=0;y<height;y++)for(int x=0;x<width;x++)rotated[x*height+height-1-y]=pixels[y*width+x];
                cached=new Texture2D(height,width,TextureFormat.RGBA32,false){name=sprite.name+" rotated",wrapMode=TextureWrapMode.Clamp};
                cached.SetPixels32(rotated);cached.Apply();rotatedIcons.Add(sprite,cached);return cached;
            }
            finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);if(readable!=null)Destroy(readable);}
        }
        void DrawGridItem(GridItem item,Rect rect,bool rotated,float alpha)
        {
            bool chosen=item.key==selectedGrid&&item.id==selected;
            var old=GUI.color;GUI.color=new Color(1,1,1,alpha);
            PlayerHUD.Fill(rect,chosen?new Color(.42f,.37f,.23f,.96f):new Color(.16f,.21f,.23f,.96f));
            var inset=new Rect(rect.x+2,rect.y+2,rect.width-4,rect.height-4);
            PlayerHUD.Fill(inset,new Color(.07f,.10f,.115f,.94f));
            var icon=item.material?inventory.Material(item.id)?.icon:inventory.Definition(item.id).inventoryIcon;
            if(icon!=null)
            {
                var imageRect=new Rect(rect.x+7,rect.y+7,Mathf.Max(1,rect.width-14),Mathf.Max(1,rect.height-14));
                if(rotated)GUI.DrawTexture(imageRect,RotatedIcon(icon),ScaleMode.ScaleToFit);
                else
                {
                    float aspect=icon.rect.width/icon.rect.height;
                    float width=Mathf.Min(imageRect.width,imageRect.height*aspect),height=width/aspect;
                    var uv=icon.textureRect;uv=new Rect(uv.x/icon.texture.width,uv.y/icon.texture.height,uv.width/icon.texture.width,uv.height/icon.texture.height);
                    GUI.DrawTextureWithTexCoords(new Rect(imageRect.center.x-width/2,imageRect.center.y-height/2,width,height),icon.texture,uv);
                }
            }
            else Text(new Rect(rect.x+5,rect.y+8,rect.width-10,rect.height-10),item.material?(inventory.Material(item.id)?.displayName??item.id):inventory.Definition(item.id).DisplayName,13,Muted);
            if(item.material)Text(new Rect(rect.x+4,rect.yMax-23,rect.width-7,22),"×"+item.quantity,16,Color.white);
            else
            {
                Text(new Rect(rect.x+4,rect.y+3,rect.width-7,22),"T"+inventory.Item(item.id).tier,14,Accent);
                if(inventory.IsEquipped(item.id))Text(new Rect(rect.x+4,rect.yMax-22,rect.width-7,20),"E",14,Accent);
            }
            if(inventory.Favorite(item.id,item.material))Text(new Rect(rect.xMax-22,rect.y+3,20,22),"★",16,Accent);
            GUI.color=old;
        }
        void DrawPreview(Rect rect)
        {
            if(!preview.HasModel)return;
            GUI.DrawTexture(rect,preview.Texture,ScaleMode.ScaleToFit);
            Text(new Rect(rect.x,rect.yMax-22,rect.width,22),"Arrastrá para rotar",14,Muted);
            var e=Event.current;
            if(e.type==EventType.MouseDown&&e.button==0&&rect.Contains(e.mousePosition)&&!confirmDiscard)
            {rotatingPreview=true;e.Use();}
            if(rotatingPreview&&e.type==EventType.MouseDrag)
            {preview.Rotate(e.delta);e.Use();}
            if(e.rawType==EventType.MouseUp)rotatingPreview=false;
            if(e.type==EventType.MouseDown&&!rect.Contains(e.mousePosition))rotatingPreview=false;
        }
        void DrawCharacterPreview()
        {
            Text(new Rect(620,120,470,36),"TU PERSONAJE",25,Accent);
            DrawPreview(new Rect(650,162,520,340));
            Text(new Rect(620,520,570,30),L.Format("Vida {0:0} / {1:0}     Armadura {2:0}",health.Current,health.Maximum,inventory.Armor),20);
            for(int i=0;i<2;i++)
            {
                Text(new Rect(620,555+i*40,405,37),(i+1)+" · "+loadout.GetSlot(i).DisplayName+" · "+(inventory.OffhandId(i)!=null?"Mano secundaria equipada":"Mano secundaria libre"),15,Muted);
                bool was=GUI.enabled;GUI.enabled=was&&inventory.CanManage&&inventory.OffhandId(i)!=null;
                if(Button(new Rect(1035,555+i*40,175,32),"Quitar secundaria"))inventory.TryEquipOffhand(i,null);
                GUI.enabled=was;
            }
            Text(new Rect(620,646,570,30),"Seleccioná un objeto para inspeccionarlo.",17,Accent);
        }
        void DrawItem()
        {
            var material=selectedMaterial?inventory.Material(selected):null;
            var weapon=selectedMaterial?null:inventory.Definition(selected);
            if(material==null&&weapon==null){selected=null;previewDirty=true;return;}
            if(Button(new Rect(620,113,168,32),"← Personaje")){selected=null;previewDirty=true;return;}
            if(Button(new Rect(1010,113,203,32),inventory.Favorite(selected,selectedMaterial)?"★ Favorito":"☆ Favorito"))inventory.ToggleFavorite(selected,selectedMaterial);
            if(preview.HasModel)DrawPreview(new Rect(735,151,370,220));
            else if(material?.icon!=null)GUI.DrawTexture(new Rect(850,185,128,128),material.icon.texture,ScaleMode.ScaleToFit);
            else Text(new Rect(820,210,370,70),selectedMaterial?"MATERIAL":"ARMA",32,Accent);
            Text(new Rect(620,377,586,43),material!=null?material.displayName:weapon.DisplayName,28,Accent);
            string description=material!=null?material.description:weapon.inventoryDescription;
            Text(new Rect(620,425,586,55),string.IsNullOrWhiteSpace(description)?(material!=null?"Recurso para preparar tu próxima salida.":"Seleccioná una ranura para comparar y equipar."):description,17,Muted);
            if(material!=null)
            {
                int count=showChest?inventory.StoredMaterialCount(selected):inventory.MaterialCount(selected);
                Text(new Rect(620,492,570,30),L.Format("Total: {0} · Pila: {1} · Tamaño: {2}×{3}",count,Mathf.Max(1,material.stackSize),material.gridWidth,material.gridHeight),19);
                if(Button(new Rect(620,534,44,34),"−"))amount=Mathf.Max(1,amount-1);
                Text(new Rect(680,538,95,28),amount.ToString(),20);
                if(Button(new Rect(770,534,44,34),"+"))amount=Mathf.Min(count,amount+1);
                if(Button(new Rect(828,534,90,34),"Todo"))amount=count;
                amount=Mathf.Clamp(amount,1,Mathf.Max(1,count));
                if(material.IsConsumable&&!showChest)
                {
                    string reason=inventory.ConsumableBlockReason(selected);
                    bool usable=GUI.enabled;GUI.enabled=usable&&reason==null;
                    if(Button(new Rect(940,534,268,34),"Usar"))inventory.TryUseConsumable(selected);
                    GUI.enabled=usable;
                    Text(new Rect(620,575,580,42),reason??(material.damageBonus>0?L.Format("Arma activa: {0}",inventory.ItemName(inventory.EquippedId(loadout.ActiveSlot))):""),16,Muted);
                }
            }
            else
            {
                var item=inventory.Item(selected);var target=inventory.Item(inventory.EquippedId(compareSlot));
                var bonuses=inventory.Rules.Bonuses(item);var current=inventory.Rules.Bonuses(target);
                Text(new Rect(620,482,585,29),"T"+item.tier+" · "+L.Text(PlayerInventory.VariantName(item.variant))+" · "+weapon.gridWidth+"×"+weapon.gridHeight,19);
                Text(new Rect(620,514,580,45),L.Format("Bonos vs. ranura {0}: daño {1} pp · vel. {2} pp\nVida {3} · Armadura {4}",compareSlot+1,Signed((bonuses.damage-current.damage)*100),Signed((bonuses.speed-current.speed)*100),Signed(bonuses.life-current.life),Signed(bonuses.armor-current.armor)),16,Muted);
                if(Button(new Rect(620,568,180,32),L.Format("Comparar ranura {0}",compareSlot+1)))compareSlot=1-compareSlot;
                bool old=GUI.enabled;GUI.enabled=inventory.CanManage&&!showChest&&!weapon.isShield&&inventory.EquippedId(compareSlot)!=selected;
                if(Button(new Rect(814,568,190,32),L.Format("Equipar en {0}",compareSlot+1)))inventory.TryEquip(compareSlot,selected);
                GUI.enabled=old;
                if(Button(new Rect(1018,568,190,32),"Ver habilidades")){OpenPage(Page.Skills);skillsWeapon=weapon;}
                GUI.enabled=old&&inventory.CanManage&&!showChest&&inventory.CanUseOffhand(compareSlot,selected)&&inventory.OffhandId(compareSlot)!=selected;
                if(Button(new Rect(620,603,285,23),"Mano secundaria del conjunto "+(compareSlot+1)))inventory.TryEquipOffhand(compareSlot,selected);
                GUI.enabled=old&&inventory.CanManage&&inventory.OffhandId(compareSlot)!=null;
                if(Button(new Rect(920,603,285,23),"Dejar mano secundaria libre"))inventory.TryEquipOffhand(compareSlot,null);
                GUI.enabled=old;
            }
            bool was=GUI.enabled;GUI.enabled=inventory.CanManage&&inventory.AtChest&&(!inventory.IsEquipped(selected)||selectedMaterial);
            if(Button(new Rect(620,628,275,36),showChest?"Retirar del cofre":"Guardar en cofre"))
            {if(inventory.Transfer(selected,selectedMaterial,amount,!showChest)){selected=null;previewDirty=true;}}
            GUI.enabled=was&&selected!=null&&!showChest&&inventory.CanDiscard(selected,selectedMaterial);
            if(Button(new Rect(915,628,295,36),"Descartar…"))confirmDiscard=true;
            GUI.enabled=was;
        }
        void DrawDiscardConfirmation()
        {
            PlayerHUD.Fill(new Rect(0,0,1280,800),new Color(0,0,0,.85f));
            PlayerHUD.Fill(new Rect(375,270,530,240),new Color(.065f,.07f,.075f));
            Text(new Rect(402,291,475,44),"¿Descartar definitivamente?",25,Accent);
            Text(new Rect(402,345,475,68),L.Format("Se eliminarán {0} unidad(es). Esta acción no se puede deshacer.",selectedMaterial?amount:1),19);
            if(Button(new Rect(400,445,225,40),"Cancelar"))confirmDiscard=false;
            if(Button(new Rect(655,445,225,40),"Descartar"))
            {if(inventory.Discard(selected,selectedMaterial,amount)){selected=null;previewDirty=true;}confirmDiscard=false;}
            if(Event.current.isMouse)Event.current.Use();
        }
        void DrawSkills()
        {
            for(int slot=0;slot<2;slot++)
            {
                var equipped=loadout.GetSlot(slot);
                if(equipped==null)continue;
                if(Tab(new Rect(70+slot*570,105,550,38),L.Format("Ranura {0} · {1}",slot+1,equipped.DisplayName),skillsWeapon==equipped))
                {skillsWeapon=equipped;skillsScroll=Vector2.zero;}
            }
            if(skillsWeapon!=loadout.GetSlot(0)&&skillsWeapon!=loadout.GetSlot(1))skillsWeapon=null;
            var weapon=skillsWeapon??loadout.ActiveDefinition;
            if(weapon==null)return;
            var family=weapon.family;
            int level=inventory.Mastery(weapon)?.level??1;
            Text(new Rect(70,153,1120,40),weapon.DisplayName+" · Maestría "+level,25,Accent);
            var basic=inventory.SelectedAbility(weapon,AbilitySlot.Basic);
            Text(new Rect(70,193,1120,30),"CLICK · "+(basic?.DisplayName??"Básico")+" · Siempre disponible",17,Muted);
            string[] keys={"Q","E","R"};
            for(int i=0;i<3;i++)
            {
                var equipped=inventory.SelectedAbility(weapon,(AbilitySlot)(i+1));
                if(Tab(new Rect(70+i*380,230,365,46),keys[i]+" · "+(equipped?.DisplayName??"—")+(equipped?.IsPassive==true?" (P)":""),selectedSkillSlot==i+1))selectedSkillSlot=i+1;
            }
            int count=family!=null&&!weapon.overrideFamilyAbilities?family.SkillCount:3;
            float contentHeight=0;
            for(int i=0;i<count;i++)
            {
                var ability=family!=null&&!weapon.overrideFamilyAbilities?family.Skill(i):weapon.GetAbility((AbilitySlot)(i+1));
                if(ability==null)continue;
                label.fontSize=17;
                contentHeight+=Mathf.Max(120,77+label.CalcHeight(new GUIContent(ability.Description),740))+12;
            }
            skillsScroll=GUI.BeginScrollView(new Rect(70,292,1140,352),skillsScroll,new Rect(0,0,1118,contentHeight));
            float row=0;
            for(int i=0;i<count;i++)
            {
                var ability=family!=null&&!weapon.overrideFamilyAbilities?family.Skill(i):weapon.GetAbility((AbilitySlot)(i+1));
                if(ability==null)continue;
                int unlock=family?.UnlockLevel(i)??1;
                bool unlocked=level>=unlock;
                string equippedKey="";
                for(int j=1;j<=3;j++)if(inventory.SelectedAbility(weapon,(AbilitySlot)j)==ability)equippedKey=keys[j-1];
                label.fontSize=17;
                float descriptionHeight=label.CalcHeight(new GUIContent(ability.Description),740);
                float height=Mathf.Max(120,77+descriptionHeight);
                PlayerHUD.Fill(new Rect(0,row,1118,height),new Color(.06f,.07f,.08f,.8f));
                Text(new Rect(20,row+10,740,32),ability.DisplayName+(equippedKey!=""?" · "+equippedKey:""),22,unlocked?Accent:Muted);
                Text(new Rect(20,row+42,740,25),ability.IsPassive?"PASIVA · No requiere pulsar una tecla":L.Format("ACTIVA · Recuperación: {0:0.##} s",ability.cooldown),15,Muted);
                Text(new Rect(20,row+73,740,Mathf.Max(35,descriptionHeight)),ability.Description,17,Muted);
                bool was=GUI.enabled;
                GUI.enabled=was&&unlocked&&loadout.CanChangeEquipment&&family!=null&&!weapon.overrideFamilyAbilities&&equippedKey!=keys[selectedSkillSlot-1];
                if(Button(new Rect(805,row+25,285,43),!unlocked?"Maestría "+unlock:equippedKey==keys[selectedSkillSlot-1]?"Equipada":equippedKey!=""?"Intercambiar con "+keys[selectedSkillSlot-1]:"Equipar en "+keys[selectedSkillSlot-1]))
                    inventory.TrySelectAbility(weapon,(AbilitySlot)selectedSkillSlot,i);
                GUI.enabled=was;
                row+=height+12;
            }
            GUI.EndScrollView();
            Text(new Rect(70,651,1120,50),loadout.InCombat?"Salí de combate para cambiar habilidades.":"Elegí Q, E o R y luego una habilidad. Activas y pasivas comparten los 3 espacios.\nNuevas habilidades en maestría 3, 6 y 9. La selección se guarda por familia.",16,Muted);
        }
        static string Signed(float value)=>value.ToString("+0.#;-0.#;0",L.Culture);
        void DrawProgression(float x,float y)
        {
            var p=inventory.Progression;var rules=inventory.Rules;
            Text(new Rect(x+30,y+133,500,38),L.Format("PERSONAJE · NIVEL {0}",p.level),25,PlayerHUD.Gold);
            Text(new Rect(x+30,y+180,500,30),p.level>=rules.characterMaxLevel?"Nivel máximo":
                L.Format("Experiencia {0} / {1}",p.experience,rules.Needed(p.level)),19);
            Text(new Rect(x+30,y+217,500,34),L.Format("Puntos disponibles: {0}",p.Available),20);
            string[] names={"Vida máxima", "Ataque", "Armadura"};
            int[] points={p.lifePoints,p.attackPoints,p.armorPoints};
            string[] increases={L.Format("+{0} vida",rules.lifePerPoint),L.Format("+{0:0.#}% daño",rules.attackPerPoint*100),L.Format("+{0} armadura",rules.armorPerPoint)};
            for(int i=0;i<3;i++)
            {
                float row=y+274+i*75;
                Text(new Rect(x+30,row,310,29),L.Format("{0} · {1} puntos",L.Text(names[i]),points[i]),19);
                Text(new Rect(x+30,row+29,300,27),L.Format("{0} por punto",increases[i]),16,PlayerHUD.Muted);
                bool old=GUI.enabled;GUI.enabled=loadout.CanChangeEquipment&&p.Available>0;
                if(Button(new Rect(x+351,row,150,46),"Mejorar +"))inventory.TrySpend((CharacterAttribute)i);
                GUI.enabled=old;
            }
            Text(new Rect(x+30,y+514,500,60),L.Format("Vida máxima: {0:0.#} · Armadura: {1:0.#}\nLos bonos de ambas armas están activos.",health.Maximum,inventory.Armor),17,PlayerHUD.Muted);
            float right=x+580;
            Text(new Rect(right,y+133,500,38),"MAESTRÍA DE ARMA",25,PlayerHUD.Gold);
            var weapon=inventory.Definition(selected)??loadout.ActiveDefinition;
            if(weapon==null)return;
            var mastery=inventory.Mastery(weapon);
            Text(new Rect(right,y+180,500,60),L.Format("{0}\nFamilia: {1}",weapon.DisplayName,L.Text(weapon.family!=null?weapon.family.DisplayName:weapon.DisplayName)),18);
            Text(new Rect(right,y+249,500,54),L.Format("Nivel {0} · Puntos: {1}",mastery.level,mastery.Available)+"\n"+
                (mastery.level>=rules.masteryMaxLevel?L.Text("Maestría máxima"):L.Format("Experiencia {0} / {1}",mastery.experience,rules.Needed(mastery.level,true))),19);
            for(int i=0;i<2;i++)
            {
                float row=y+326+i*75;
                float increase=i==0?rules.masteryDamagePerPoint:rules.masterySpeedPerPoint;
                Text(new Rect(right,row,300,29),L.Format("{0} · {1} puntos",L.Text(i==0?"Daño":"Velocidad"),i==0?mastery.damagePoints:mastery.speedPoints),19);
                Text(new Rect(right,row+29,300,27),L.Format("+{0:0.#}% por punto",increase*100),16,PlayerHUD.Muted);
                bool old=GUI.enabled;GUI.enabled=loadout.CanChangeEquipment&&mastery.Available>0;
                if(Button(new Rect(right+334,row,160,46),"Mejorar +"))inventory.TrySpendMastery(weapon,(MasteryAttribute)i);
                GUI.enabled=old;
            }
            Text(new Rect(right,y+493,500,88),"Elegí otra arma en la pestaña Armas para ver su maestría.\nEl progreso se comparte entre ejemplares de la misma familia. El alcance no aumenta.",17,PlayerHUD.Muted);
        }
    }
}
