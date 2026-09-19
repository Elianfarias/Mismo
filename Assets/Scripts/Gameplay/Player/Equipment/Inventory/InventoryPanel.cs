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
    public sealed partial class InventoryPanel : MonoBehaviour
    {
        enum Page { Menu, Inventory, Character, Skills, Bestiary, Mounts, Weapons }
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
            if (Mismo.Gameplay.Player.Presentation.GameplayPause.BlocksInput) {if(IsOpen&&page==Page.Menu)Close();return;}
            if(IsOpen&&(health==null||health.IsDead)){Close();return;}
            if(showChest&&!inventory.AtChest){showChest=false;selected=null;confirmDiscard=false;showItemActions=false;CancelInventoryDrag();previewDirty=true;}
            var k=Keyboard.current;
            bool typing=IsOpen&&searchFocused;
            if(k!=null)
            {
                if(!IsOpen&&!WorldMapPanel.AnyOpen&&health!=null&&!health.IsDead)
                {
                    for(int slot=0;slot<4;slot++)
                        if(k[(Key)((int)Key.Digit1+slot)].wasPressedThisFrame){inventory.TryUseConsumableSlot(slot);break;}
                }
                if(k.escapeKey.wasPressedThisFrame&&IsOpen)
                {
                    if(skillDragIndex>=0)CancelSkillDrag();
                    else if(draggedGrid!=null)CancelInventoryDrag();
                    else if(showItemActions)showItemActions=false;
                    else if(confirmDiscard)confirmDiscard=false;
                    else if(selected!=null&&page==Page.Inventory){selected=null;previewDirty=true;}
                    else Close();
                }
                else if(!typing&&k.iKey.wasPressedThisFrame){if(IsOpen&&page==Page.Inventory)Close();else OpenPage(Page.Inventory);}
                else if(!typing&&k.bKey.wasPressedThisFrame){if(OpenPage(Page.Menu))radialHeld=true;}
                else if(!typing&&k.pKey.wasPressedThisFrame)OpenPage(Page.Character);
                else if(!typing&&k.kKey.wasPressedThisFrame)OpenPage(Page.Skills);
            }
            if(IsOpen&&page==Page.Menu)
            {
                UpdateRadialSelection();
                if(radialHeld&&k!=null&&k.bKey.wasReleasedThisFrame)ConfirmRadialSelection();
            }
            if(IsOpen&&draggedGrid!=null&&k!=null&&k.rKey.wasPressedThisFrame)
            {
                var item=inventory.GridItems(showChest).Find(i=>i.key==draggedGrid);
                if(item!=null&&item.canRotate){dragRotated=!dragRotated;dragOffset=Vector2Int.zero;}
            }
            if(IsOpen&&previewDirty)
            {
                previewDirty=false;
                if(page==Page.Menu){preview.Dispose();return;}
                GameObject source=gameObject;
                if(page==Page.Bestiary||page==Page.Mounts){source=null;mountPreviewId=null;}
                // Keep the character visible while inspecting inventory objects.
                if(page==Page.Weapons)source=CurrentMenuWeapon?.visualPrefab;
                Vector3 rotation=Vector3.zero;
                if(page==Page.Weapons)rotation=CurrentMenuWeapon?.inventoryPreviewRotation??Vector3.zero;
                preview.Show(source,rotation,page==Page.Character||page==Page.Weapons||page==Page.Inventory);
            }
        }
        void LateUpdate(){if(IsOpen&&page!=Page.Menu)preview.Render();}
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
                GameAudio.Play(GameSound.MenuOpen);
            }
            else if (page != target) GameAudio.Play(GameSound.TabChanged);
            if(target==Page.Skills&&page!=Page.Skills){skillsWeapon=loadout.ActiveDefinition;skillsScroll=Vector2.zero;}
            CancelSkillDrag();CancelInventoryDrag();showItemActions=false;
            if(page!=target){System.Array.Clear(pendingAttributes,0,3);ResetMasteryDraft();}
            page=target;confirmDiscard=false;previewDirty=true;searchFocused=false;
            radialHeld=false;
            if(target==Page.Menu)
            {
                radialSelected=-1;radialOpenedFrame=Time.frameCount;
                Mouse.current?.WarpCursorPosition(new Vector2(Screen.width*.5f,Screen.height*.5f));
            }
            return true;
        }
        public bool OpenChest()
        {
            if(!inventory.AtChest||!inventory.CanManage||!OpenPage(Page.Inventory))return false;
            showChest=true;selected=null;draggedGrid=null;previewDirty=true;return true;
        }
        public void Close()
        {
            if(!IsOpen)return;
            GameAudio.Play(GameSound.MenuClose);
            IsOpen=false;System.Array.Clear(pendingAttributes,0,3);ResetMasteryDraft();radialHeld=false;closedFrame=Time.frameCount;if(active==this)active=null;
            Cursor.lockState=previousLock;Cursor.visible=previousVisible;confirmDiscard=false;showChest=false;draggedGrid=null;
            CancelSkillDrag();CancelInventoryDrag();showItemActions=false;preview.Dispose();searchFocused=false;rotatingPreview=false;
        }
        void OnDisable()=>Close();
        void OnDestroy(){DisposeRadialTextures();if(inventory!=null)inventory.Changed-=OnChanged;preview.Dispose();foreach(var icon in rotatedIcons.Values)Destroy(icon);rotatedIcons.Clear();if(inventoryBackdrop!=null)Destroy(inventoryBackdrop);}
        void Text(Rect rect,string value,int size=18,Color? color=null)
        {label.fontSize=size;label.normal.textColor=color??Color.white;GUI.Label(rect,L.Text(value),label);}
        bool Button(Rect rect,string value)
        {
            var old=GUI.backgroundColor;if(old==Color.white)GUI.backgroundColor=new Color(.13f,.16f,.18f);
            bool pressed=GameAudio.Button(rect,L.Text(value),button);GUI.backgroundColor=old;
            FantasyUI.Frame(rect,GUI.enabled?(rect.Contains(Event.current.mousePosition)?PlayerHUD.Gold:Accent):Muted*.55f);
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
            if (Mismo.Gameplay.Player.Presentation.GameplayPause.BlocksInput) return;
            if(inventory==null||health==null||health.IsDead)return;
            if(label==null)
            {
                label=new GUIStyle(GUI.skin.label){font=QuietFantasyUI.Body,wordWrap=true,padding=new RectOffset(0,0,0,0)};
                button=new GUIStyle(GUI.skin.button){fontSize=16,wordWrap=true,padding=new RectOffset(4,4,5,5)};
                button.normal.background=Texture2D.whiteTexture;button.normal.textColor=Color.white;
                button.hover.background=Texture2D.whiteTexture;button.hover.textColor=Accent;
                button.active.background=Texture2D.whiteTexture;button.active.textColor=Color.white;
                FantasyUI.StyleButton(button);
                field=new GUIStyle(GUI.skin.textField){font=QuietFantasyUI.Body,fontSize=20,padding=new RectOffset(12,12,7,7)};
            }
            if(inventoryIcons==null)inventoryIcons=Resources.Load<InventoryUIIcons>("InventoryUIIcons");
            var matrix=GUI.matrix;int depth=GUI.depth;
            if(IsOpen&&page!=Page.Menu){GUI.depth=-40;PlayerHUD.Fill(new Rect(0,0,Screen.width,Screen.height),IsQuietPage?new Color(.015f,.022f,.03f,.25f*PanelOpacity):new Color(.015f,.022f,.03f,.86f*PanelOpacity));}
            const float canvasWidth=1280f;
            float scale=Mathf.Min(Screen.width/canvasWidth,Screen.height/800f);
            if(IsQuietPage)scale*=.9f;
            GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-canvasWidth*scale)/2,(Screen.height-800*scale)/2,0),Quaternion.identity,Vector3.one*scale);
            GUI.depth=-40;
            if(IsOpen&&page==Page.Menu)
            {
                DrawMenu();GUI.matrix=matrix;GUI.depth=depth;return;
            }
            if(IsOpen&&page==Page.Inventory)
            {
                DrawVisualInventory();GUI.matrix=matrix;GUI.depth=depth;return;
            }
            if(IsOpen&&IsQuietPage)
            {
                DrawQuietInterface();GUI.matrix=matrix;GUI.depth=depth;return;
            }
            if(!IsOpen)
            {

                if(Time.unscaledTime<messageUntil){PlayerHUD.Fill(new Rect(265,25,750,65),PlayerHUD.Panel);Text(new Rect(285,37,710,48),message,18,Accent);}
            }
            else
            {
                FantasyUI.Panel(new Rect(16,10,1248,788));
                bool previousEnabled=GUI.enabled;GUI.enabled=previousEnabled&&!confirmDiscard;
                Text(new Rect(38,25,740,44),page==Page.Menu?"VIAJERO":page==Page.Inventory?"PERTENENCIAS":page==Page.Character?"PERSONAJE":page==Page.Bestiary?"BESTIARIO":page==Page.Mounts?"MONTURAS":"HABILIDADES",32,Accent);
                if(Button(new Rect(940,27,120,35),"Menú [B]"))OpenPage(Page.Menu);
                if(Button(new Rect(1075,27,165,35),"Cerrar [ESC]"))Close();
                Line(38,82,1202);

                if(page==Page.Menu)DrawMenu();


                else if(page==Page.Bestiary)bestiary.Draw(inventory,preview);
                else if(page==Page.Mounts)DrawMounts();
                else DrawVisualInventory();
                Line(38,710,1202);
                Text(new Rect(40,726,1180,30),L.Format("MOCHILA  {0} / {1} celdas",inventory.UsedSlots(false),inventory.BackpackCapacity)+
                    (inventory.AtChest?L.Format("     COFRE  {0} / {1}",inventory.UsedSlots(true),inventory.ChestCapacity):"")+L.Format("     ·     NIVEL {0}",inventory.Level),19,Accent);
                Text(new Rect(40,764,1190,30),inventory.Notice,16,inventory.HasSaveProblem?new Color(1,.55f,.3f):Muted);
                GUI.enabled=previousEnabled;
                if(confirmDiscard)DrawDiscardConfirmation();
            }
            GUI.matrix=matrix;GUI.depth=depth;
        }
        void DrawMounts()
        {
            var mounts=inventory.Mounts;
            if(mounts.Length==0)
            {
                var color=GUI.color;GUI.color=NavigationTint(QuietFantasyUI.Muted);
                if(inventoryIcons?.radialMounts!=null)GUI.DrawTexture(new Rect(604,270,72,72),MapIcons.Mask(inventoryIcons.radialMounts),ScaleMode.ScaleToFit);
                else QuietFantasyUI.DrawIcon(new Rect(604,270,72,72),"wingfoot",NavigationTint(QuietFantasyUI.Muted));
                GUI.color=color;
                QuietFantasyUI.Text(new Rect(260,375,760,48),"Todavía no tenés monturas",27,QuietFantasyUI.Ink,false,TextAnchor.MiddleCenter);
                QuietFantasyUI.Text(new Rect(290,432,700,72),"Algunas criaturas pueden reconocerte al vencerlas.",21,QuietFantasyUI.Muted,false,TextAnchor.MiddleCenter);
                return;
            }
            mountIndex=Mathf.Clamp(mountIndex,0,mounts.Length-1);
            mountScroll=Mismo.Gameplay.Player.Presentation.QuietFantasyUI.BeginScrollView(new Rect(65,155,410,510),mountScroll,new Rect(0,0,385,mounts.Length*65));
            for(int i=0;i<mounts.Length;i++)
            {
                var sp=World.CreatureSpecies.Find(mounts[i].speciesId);
                if(QuietFantasyUI.Button(new Rect(0,i*65,370,55),(sp!=null?L.Text(sp.displayName):mounts[i].speciesId)+" · "+(i+1),i==mountIndex)){mountIndex=i;rotatingPreview=false;}
            }
            GUI.EndScrollView();
            var mount=mounts[mountIndex];var species=World.CreatureSpecies.Find(mount.speciesId);
            if(mountPreviewId!=mount.id)
            {
                mountPreviewId=mount.id;
                try{preview.Show(species?.prefabs==null?null:System.Array.Find(species.prefabs,p=>p!=null&&p.name==mount.prefabName),new Vector3(0,150,0));preview.Zoom(species!=null?species.bookZoom:1);}
                catch(Exception exception){preview.Dispose();Debug.LogWarning("Mount preview unavailable for "+mount.id+": "+exception.Message);}
            }
            PlayerHUD.Fill(new Rect(510,155,1,510),QuietFantasyUI.Rule);
            QuietFantasyUI.Text(new Rect(555,151,630,40),species!=null?species.displayName:mount.speciesId,27,QuietFantasyUI.Ink,false,TextAnchor.MiddleCenter);
            if(preview.HasModel)DrawPreview(new Rect(605,205,530,325));
            else QuietFantasyUI.Text(new Rect(605,205,530,325),"Vista previa no disponible",20,QuietFantasyUI.Muted,false,TextAnchor.MiddleCenter);
            bool selectedActive=inventory.SelectedMountId==mount.id&&inventory.CompanionSummoned;
            QuietFantasyUI.Text(new Rect(555,540,630,32),selectedActive?"Acompañante activo":"Guardada en tu colección",20,QuietFantasyUI.Muted,false,TextAnchor.MiddleCenter);
            bool enabled=GUI.enabled;GUI.enabled=enabled&&inventory.CanCallMount&&species!=null;
            if(QuietFantasyUI.Button(new Rect(565,597,285,45),selectedActive?"Invocar a mi lado":"Invocar montura"))inventory.SummonMount(mount.id);
            GUI.enabled=enabled&&inventory.CanCallMount&&inventory.CompanionSummoned;
            if(QuietFantasyUI.Button(new Rect(875,597,285,45),"Guardar acompañante"))inventory.DismissMount();GUI.enabled=enabled;
            if(!inventory.CanCallMount)QuietFantasyUI.Text(new Rect(555,665,630,50),"Desmontá y salí de combate para cambiar de acompañante.",17,QuietFantasyUI.Muted,false,TextAnchor.MiddleCenter);
        }

        bool Matches(GridItem item)
        {
            var material=item.material?inventory.Material(item.id):null;
            string name=item.material?(material!=null?material.displayName:item.id):inventory.Definition(item.id).DisplayName;
            return L.Text(name).IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0&&
                (filter==0||filter==1&&!item.material||filter==2&&item.material&&(material==null||material.category==InventoryItemCategory.Material)||
                filter==3&&material!=null&&material.category==InventoryItemCategory.Consumable||filter==4&&inventory.Favorite(item.id,item.material));
        }
        void DrawGrid(Rect viewport)
        {
            int columns=inventory.GridColumns(showChest),rows=inventory.GridRows(showChest);
            float cell=(viewport.width-24)/columns;
            bool pointerInGrid=viewport.Contains(Event.current.mousePosition);
            var items=inventory.GridItems(showChest);var positions=inventory.GridPositions(showChest);
            var unplaced=items.FindAll(i=>!positions.Exists(p=>p.key==i.key));
            scroll=Mismo.Gameplay.Player.Presentation.QuietFantasyUI.BeginScrollView(viewport,scroll,new Rect(0,0,viewport.width-22,Mathf.Max(viewport.height-6,rows*cell+unplaced.Count*49+(unplaced.Count>0?38:0))));
            for(int y=0;y<rows;y++)for(int x=0;x<columns;x++)
            {
                PlayerHUD.Fill(new Rect(x*cell,y*cell,cell-2,cell-2),OliveTheme?new Color(.43f,.41f,.28f,.65f):new Color(.36f,.39f,.44f,.55f));
                PlayerHUD.Fill(new Rect(x*cell+2,y*cell+2,cell-6,cell-6),OliveTheme?new Color(.20f,.24f,.16f,.94f):new Color(.09f,.13f,.19f,.94f));
            }
            foreach(var p in positions)
            {
                var item=items.Find(i=>i.key==p.key);if(item==null)continue;
                var rect=new Rect(p.x*cell+1,p.y*cell+1,(p.rotated?item.height:item.width)*cell-3,(p.rotated?item.width:item.height)*cell-3);
                DrawGridItem(item,rect,p.rotated,Matches(item)?1:.22f);
                var e=Event.current;
                if(e.type==EventType.MouseDown&&e.button==0&&rect.Contains(e.mousePosition)&&GUI.enabled&&!confirmDiscard)
                {
                    selected=item.id;selectedMaterial=item.material;selectedGrid=item.key;amount=1;
                    GUI.FocusControl(null);searchFocused=false;
                    if(inventory.CanManage&&(!showChest||inventory.AtChest))
                    {
                        draggedGrid=item.key;dragRotated=p.rotated;dragStart=e.mousePosition;dragFromHand=-1;dragFromConsumable=-1;
                        inventoryDragOrigin=GUIUtility.GUIToScreenPoint(e.mousePosition);
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
                    if(pointerInGrid&&dragFromHand<0&&InventoryDragMoved)PlayerHUD.Fill(ghost,valid?new Color(.22f,.7f,.45f,.45f):new Color(.9f,.2f,.15f,.45f));
                    if(e.type==EventType.MouseUp&&e.button==0&&pointerInGrid&&dragFromHand<0&&dragFromConsumable<0&&GUI.enabled)
                    {
                        if(valid&&(Vector2.Distance(e.mousePosition,dragStart)>3||positions.Find(p=>p.key==item.key)?.rotated!=dragRotated))inventory.MoveGrid(item.key,showChest,x,y,dragRotated);
                        CancelInventoryDrag();e.Use();
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
            PlayerHUD.Fill(rect,chosen?new Color(.42f,.37f,.23f,.96f):new Color(.22f,.27f,.34f,.96f));
            var inset=new Rect(rect.x+2,rect.y+2,rect.width-4,rect.height-4);
            PlayerHUD.Fill(inset,OliveTheme?new Color(.24f,.29f,.19f,.96f):new Color(.105f,.15f,.215f,.96f));
            QuietFantasyUI.Border(rect,chosen?PlayerHUD.Gold:new Color(.48f,.52f,.59f,.55f),chosen?2:1);
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
                if(inventory.IsEquipped(item.id))Text(new Rect(rect.x+4,rect.yMax-22,rect.width-7,20),EquippedMarker(item.id),14,Accent);
            }
            if(inventory.Favorite(item.id,item.material))Text(new Rect(rect.xMax-22,rect.y+3,20,22),"★",16,Accent);
            GUI.color=old;
        }
        void DrawPreview(Rect rect)
        {
            if(!preview.HasModel)return;
            GUI.DrawTexture(rect,preview.Texture,ScaleMode.ScaleToFit);
            var e=Event.current;
            if(e.type==EventType.MouseDown&&e.button==0&&rect.Contains(e.mousePosition)&&GUI.enabled&&!confirmDiscard)
            {rotatingPreview=true;e.Use();}
            if(rotatingPreview&&e.type==EventType.MouseDrag)
            {preview.Rotate(new Vector2(-e.delta.x,e.delta.y));e.Use();}
            if(e.rawType==EventType.MouseUp)rotatingPreview=false;
            if(e.type==EventType.MouseDown&&!rect.Contains(e.mousePosition))rotatingPreview=false;
        }


        void DrawDiscardConfirmation()
        {
            PlayerHUD.Fill(new Rect(0,0,1280,800),new Color(0,0,0,.85f));
            FantasyUI.Panel(new Rect(375,270,530,240));
            Text(new Rect(402,291,475,44),"¿Descartar definitivamente?",25,Accent);
            Text(new Rect(402,345,475,68),L.Format("Se eliminarán {0} unidad(es). Esta acción no se puede deshacer.",selectedMaterial?amount:1),19);
            if(Button(new Rect(400,445,225,40),"Cancelar"))confirmDiscard=false;
            if(Button(new Rect(655,445,225,40),"Descartar"))
            {if(inventory.Discard(selected,selectedMaterial,amount)){selected=null;previewDirty=true;}confirmDiscard=false;}
            if(Event.current.isMouse)Event.current.Use();
        }
        static string Signed(float value)=>value.ToString("+0.#;-0.#;0",L.Culture);
    }
}
