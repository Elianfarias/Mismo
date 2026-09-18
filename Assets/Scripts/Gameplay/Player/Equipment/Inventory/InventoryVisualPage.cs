using UnityEngine;
using Mismo.Gameplay.Player.Presentation;
using U = Mismo.Gameplay.Player.Presentation.QuietFantasyUI;
using L = Mismo.Gameplay.Player.Localization.GameLanguage;

namespace Mismo.Gameplay.Player.Equipment.Inventory
{
    public sealed partial class InventoryPanel
    {
        static readonly Rect BagViewport = new Rect(64, 210, 540, 404);
        static readonly Rect ItemActionsRect = new Rect(330, 494, 275, 230);
        readonly Rect[] handTargets = new Rect[4];
        readonly Rect[] consumableTargets = new Rect[4];
        int dragFromConsumable=-1;
        bool showItemActions, compareOffhand;
        Texture2D inventoryBackdrop;
        bool inventoryBackdropOlive;
        bool OliveTheme=>inventoryIcons!=null&&inventoryIcons.useOliveTheme;
        InventoryUIIcons inventoryIcons;
        int dragFromHand = -1;
        Vector2 inventoryDragOrigin;
        string inventoryHint;
        bool InventoryDragMoved => draggedGrid != null &&
            Vector2.Distance(GUIUtility.GUIToScreenPoint(Event.current.mousePosition), inventoryDragOrigin) > 6;

        void CancelInventoryDrag() { draggedGrid = null; dragFromHand = -1; dragFromConsumable=-1; }
        string EquippedMarker(string id)
        {
            for(int i=0;i<2;i++)
                if(inventory.EquippedId(i)==id || inventory.OffhandId(i)==id) return (i+1).ToString();
            return "";
        }
        string HandItem(int hand) => hand%2==0 ? inventory.EquippedId(hand/2) : inventory.OffhandId(hand/2);
        bool CanDropWeapon(string id, int hand)
        {
            if(!inventory.CanManage || showChest || inventory.Item(id)==null || inventory.Item(id).inChest) return false;
            var definition=inventory.Definition(id);
            return definition!=null && (hand%2==0 ? !definition.isShield : inventory.CanUseOffhand(hand/2,id));
        }
        bool EquipAtHand(string id, int hand)
        {
            if(!CanDropWeapon(id,hand)) return false;
            compareSlot=hand/2;compareOffhand=hand%2!=0;
            if(HandItem(hand)==id) return true;
            return hand%2==0 ? inventory.TryEquip(hand/2,id) : inventory.TryEquipOffhand(hand/2,id);
        }

        void DrawVisualInventory()
        {
            if(inventoryIcons==null)inventoryIcons=Resources.Load<InventoryUIIcons>("InventoryUIIcons");
            var e=Event.current;
            if(showItemActions && e.type==EventType.MouseDown && !ItemActionsRect.Contains(e.mousePosition))
            { showItemActions=false;e.Use(); }
            // Rebuild only when the inspector's live theme switch changes.
            if(inventoryBackdrop==null||inventoryBackdropOlive!=OliveTheme)
            {
                if(inventoryBackdrop!=null)Destroy(inventoryBackdrop);
                inventoryBackdropOlive=OliveTheme;
                inventoryBackdrop=new Texture2D(128,80,TextureFormat.RGBA32,false)
                    {name="Inventory slate gradient",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
                var colors=new Color[128*80];
                for(int y=0;y<80;y++)for(int x=0;x<128;x++)
                {
                    float u=x/127f,v=y/79f;
                    float glow=Mathf.Exp(-((u-.742f)*(u-.742f)/.10f+(v-.68f)*(v-.68f)/.23f));
                    colors[y*128+x]=OliveTheme?
                        Color.Lerp(Color.Lerp(new Color(.27f,.23f,.16f),new Color(.20f,.28f,.19f),v),new Color(.34f,.39f,.25f),glow*.60f):
                        Color.Lerp(new Color(.063f,.090f,.133f),new Color(.157f,.208f,.278f),.28f+.72f*glow);
                }
                inventoryBackdrop.SetPixels(colors);inventoryBackdrop.Apply(false,true);
            }
            GUI.DrawTexture(new Rect(30,24,1220,752),inventoryBackdrop,ScaleMode.StretchToFill);
            if(inventoryIcons?.background!=null)DrawInventoryTexture(new Rect(30,24,1220,752),inventoryIcons.background,inventoryIcons.backgroundBorder,inventoryIcons.backgroundTint);
            bool enabled=GUI.enabled;
            GUI.enabled=enabled&&!confirmDiscard&&!showItemActions;
            DrawBagGlyph(new Rect(66,45,30,34),U.Ink);
            U.Text(new Rect(113,49,290,34),inventory.UsedSlots(showChest)+" / "+(showChest?inventory.ChestCapacity:inventory.BackpackCapacity),23,U.Ink);
            if(InventoryIconButton(new Rect(1090,38,52,47),inventoryIcons?.menu,"Menú [B]")) OpenPage(Page.Menu);
            if(InventoryIconButton(new Rect(1162,38,52,47),inventoryIcons?.close,"Cerrar [ESC]")) Close();
            if(!IsOpen||page!=Page.Inventory){GUI.enabled=enabled;return;}
            U.Border(new Rect(64,94,1150,1),new Color(.78f,.70f,.51f,.35f));
            if(inventory.AtChest)
            {
                if(U.Button(new Rect(410,45,85,36),"◀",!showChest)){showChest=false;selected=null;CancelInventoryDrag();}
                if(U.Button(new Rect(503,45,115,36),"Cofre",showChest)){showChest=true;selected=null;CancelInventoryDrag();}
            }
            string[] categories={"Todos","Armas","Materiales","Consumo","Favoritos"};
            Texture2D[] categoryIcons={inventoryIcons?.all,inventoryIcons?.weapons,inventoryIcons?.materials,inventoryIcons?.consumables,inventoryIcons?.favorites};
            for(int i=0;i<categories.Length;i++)
                if(InventoryIconButton(new Rect(64+i*108,108,104,45),categoryIcons[i],categories[i],filter==i))
                {filter=i;scroll=Vector2.zero;CancelInventoryDrag();}
            field.font=U.Body;field.fontSize=19;
            GUI.SetNextControlName("InventorySearch");
            query=GUI.TextField(new Rect(64,162,410,34),query,80,field);
            searchFocused=GUI.GetNameOfFocusedControl()=="InventorySearch";
            if(query.Length==0 && e.type==EventType.Repaint)U.Text(new Rect(76,168,380,25),"Buscar objetos…",18,U.Muted);
            bool was=GUI.enabled;GUI.enabled=was&&inventory.CanManage;
            if(InventoryIconButton(new Rect(538,158,64,42),inventoryIcons?.organize,"Organizar")){inventory.OrganizeGrid(showChest);CancelInventoryDrag();}
            GUI.enabled=was;
            inventoryHint=null;
            DrawGrid(BagViewport);
            DrawPreview(new Rect(820,105,216,290));
            DrawConsumableSlots();
            DrawSpecialSlot();
            DrawEquipmentHands();
            DrawCompactSelection();
            HandleEquipmentDrop();
            U.Border(new Rect(64,736,1150,1),new Color(.78f,.70f,.51f,.35f));
            string status=inventory.HasSaveProblem?inventory.Notice:!string.IsNullOrEmpty(GUI.tooltip)?GUI.tooltip:inventoryHint??
                (Time.unscaledTime<messageUntil?message:"Arrastrá al equipo · R para girar · Clic en objeto y mano para equipar");
            U.Text(new Rect(64,746,1150,28),status,17,inventory.HasSaveProblem?new Color(1,.65f,.35f):U.Ink);
            GUI.enabled=enabled;
            if(showItemActions&&!confirmDiscard)DrawCompactActions();
            if(confirmDiscard)DrawDiscardConfirmation();
        }

        bool InventoryIconButton(Rect rect,Texture2D icon,string caption,bool chosen=false)
        {
            if(icon==null)return U.Button(rect,caption,chosen);
            bool clicked=U.Button(rect,"",chosen);
            var color=GUI.color;GUI.color=chosen?U.Amber:U.Ink;
            float size=Mathf.Min(34,rect.height-8);
            GUI.DrawTexture(new Rect(rect.center.x-size/2,rect.center.y-size/2,size,size),MapIcons.Mask(icon),ScaleMode.ScaleToFit);
            GUI.color=color;
            GUI.Label(rect,new GUIContent("",L.Text(caption)),GUIStyle.none);
            return clicked;
        }

        void DrawBagGlyph(Rect rect,Color color)
        {
            if(inventoryIcons?.backpack!=null)
            {
                var previous=GUI.color;GUI.color=color;
                GUI.DrawTexture(rect,MapIcons.Mask(inventoryIcons.backpack),ScaleMode.ScaleToFit);
                GUI.color=previous;
                GUI.Label(rect,new GUIContent("",L.Text("Mochila")),GUIStyle.none);
                return;
            }
            U.Border(new Rect(rect.x+7,rect.y,rect.width-14,10),color,2);
            U.Border(new Rect(rect.x,rect.y+8,rect.width,rect.height-8),color,2);
            U.Border(new Rect(rect.x+6,rect.y+21,rect.width-12,8),color,1);
        }

        static void DrawInventoryTexture(Rect rect,Texture2D texture,RectOffset border,Color tint)
        {
            if(Event.current.type!=EventType.Repaint)return;
            var style=new GUIStyle {border=border??new RectOffset()};style.normal.background=texture;
            var previous=GUI.color;GUI.color=tint;
            style.Draw(rect,GUIContent.none,false,false,false,false);GUI.color=previous;
        }
        void DrawEquipmentSlotBackground(Rect rect,bool valid,bool hovering,bool weapon=false)
        {
            var artwork=weapon?inventoryIcons?.weaponSlotBackground:inventoryIcons?.consumableSlotBackground;
            var uv=weapon?inventoryIcons?.weaponSlotUV??new Rect(0,0,1,1):inventoryIcons?.consumableSlotUV??new Rect(0,0,1,1);
            var oliveArtwork=weapon?inventoryIcons?.oliveWeaponSlotBackground:inventoryIcons?.oliveConsumableSlotBackground;
            if(OliveTheme&&oliveArtwork!=null)
            {artwork=oliveArtwork;uv=weapon?inventoryIcons.oliveWeaponSlotUV:inventoryIcons.oliveConsumableSlotUV;}
            if(artwork!=null)
            {
                var previous=GUI.color;
                GUI.color=inventoryIcons.slotTint*(valid?(hovering?new Color(1,.90f,.65f):new Color(1,.96f,.83f)):Color.white);
                GUI.DrawTextureWithTexCoords(rect,artwork,uv);
                GUI.color=previous;return;
            }
            if(inventoryIcons?.slotBackground!=null)
                DrawInventoryTexture(rect,inventoryIcons.slotBackground,inventoryIcons.slotBorder,inventoryIcons.slotTint);
            else PlayerHUD.Fill(rect,new Color(.075f,.110f,.165f,.94f));
            if(valid&&hovering)PlayerHUD.Fill(rect,new Color(.65f,.60f,.30f,.25f));
            if(valid||inventoryIcons?.slotBackground==null)
                U.Border(rect,valid?Accent:new Color(.48f,.52f,.59f,.45f),valid?2:1);
        }
        static void DrawInventorySprite(Rect rect,Sprite icon)
        {
            if(icon==null)return;
            float aspect=icon.rect.width/icon.rect.height;
            float w=Mathf.Min(rect.width,rect.height*aspect),h=w/aspect;
            var uv=icon.textureRect;var texture=icon.texture;
            GUI.DrawTextureWithTexCoords(new Rect(rect.center.x-w/2,rect.center.y-h/2,w,h),texture,
                new Rect(uv.x/texture.width,uv.y/texture.height,uv.width/texture.width,uv.height/texture.height));
        }
        void DrawConsumableSlots()
        {
            var e=Event.current;
            var dragged=draggedGrid!=null?inventory.GridItems(showChest).Find(item=>item.key==draggedGrid):null;
            string candidate=dragged!=null?(dragged.material?dragged.id:null):selectedMaterial?selected:null;
            bool valid=!showChest&&candidate!=null&&inventory.CanAssignConsumable(candidate);
            for(int slot=0;slot<4;slot++)
            {
                var rect=new Rect(748+slot*96,537,72,72);consumableTargets[slot]=rect;
                string id=inventory.ConsumableSlot(slot);var material=inventory.Material(id);
                bool hovering=rect.Contains(e.mousePosition);
                DrawEquipmentSlotBackground(rect,valid,hovering);
                if(material!=null)
                {
                    var old=GUI.color;if(inventory.MaterialCount(id)==0)GUI.color=new Color(1,1,1,.3f);
                    DrawInventorySprite(new Rect(rect.x+10,rect.y+10,52,52),material.icon);GUI.color=old;
                }
                U.Text(new Rect(rect.x,rect.yMax+8,rect.width,24),(slot+1).ToString(),18,U.Ink,false,TextAnchor.UpperCenter);
                if(hovering)
                {
                    inventoryHint=valid?L.Format("Asignar a {0}",slot+1):material!=null?
                        L.Text(material.displayName)+" · "+inventory.MaterialCount(id)+" · "+L.Text("Clic derecho para desasignar"):
                        L.Text("Arrastrá un consumible a esta ranura.");
                    if(e.type==EventType.MouseDown&&GUI.enabled)
                    {
                        if(e.button==1){inventory.AssignConsumable(slot,null);CancelInventoryDrag();e.Use();}
                        else if(e.button==0)
                        {
                            if(valid&&candidate!=id){inventory.AssignConsumable(slot,candidate);CancelInventoryDrag();}
                            else if(material!=null)
                            {
                                selected=id;selectedMaterial=true;amount=1;
                                var grid=inventory.GridItems(false).Find(item=>item.material&&item.id==id);
                                if(grid!=null&&!showChest&&inventory.CanManage)
                                {draggedGrid=grid.key;dragFromHand=-1;dragFromConsumable=slot;inventoryDragOrigin=GUIUtility.GUIToScreenPoint(e.mousePosition);}
                            }
                            GUI.FocusControl(null);searchFocused=false;e.Use();
                        }
                    }
                }
            }
        }
        void DrawSpecialSlot()
        {
            var rect=new Rect(884,393,88,88);
            var special=loadout.BeltComponent?.Definition;
            DrawEquipmentSlotBackground(rect,false,false);
            if(special?.inventoryIcon!=null)DrawInventorySprite(new Rect(rect.x+10,rect.y+10,68,68),special.inventoryIcon);
            else if(special!=null&&inventoryIcons?.special!=null)
            {
                var old=GUI.color;GUI.color=U.Ink;
                GUI.DrawTexture(new Rect(rect.x+16,rect.y+16,56,56),MapIcons.Mask(inventoryIcons.special),ScaleMode.ScaleToFit);GUI.color=old;
            }
            if(rect.Contains(Event.current.mousePosition))inventoryHint=special!=null?L.Text(special.DisplayName):L.Text("Objeto especial");
        }

        void DrawEquipmentHands()
        {
            var e=Event.current;
            for(int set=0;set<2;set++)
            {
                float x=620+set*418;
                for(int side=0;side<2;side++)
                {
                    int hand=set*2+side;
                    var rect=new Rect(x+side*104,184,96,172);handTargets[hand]=rect;
                    string id=HandItem(hand);
                    bool hovering=rect.Contains(e.mousePosition);
                    var candidate=draggedGrid!=null?inventory.GridItems(showChest).Find(item=>item.key==draggedGrid):null;
                    string candidateId=candidate!=null&&!candidate.material?candidate.id:!selectedMaterial?selected:null;
                    bool valid=candidateId!=null&&CanDropWeapon(candidateId,hand);
                    DrawEquipmentSlotBackground(rect,valid,hovering,true);
                    if(id!=null)DrawWeaponThumbnail(new Rect(rect.x+18,rect.y+22,rect.width-36,rect.height-44),id);
                    else U.DrawIcon(new Rect(rect.center.x-19,rect.center.y-25,38,38),"checked-shield",new Color(.74f,.73f,.59f,.35f));
                    if(hovering)
                    {
                        compareSlot=set;compareOffhand=side!=0;
                        if(candidateId!=null)
                            inventoryHint=valid?L.Format("Soltar en conjunto {0} · {1}",set+1,L.Text(side==0?"Principal":"Secundaria")):
                                "Esta mano no admite el objeto seleccionado.";
                        if(e.type==EventType.MouseDown&&e.button==0&&GUI.enabled)
                        {
                            if(id!=null && (selected==null||selected==id||!valid))
                            {
                                selected=id;selectedGrid=id;selectedMaterial=false;amount=1;
                                if(inventory.CanManage&&!showChest)
                                {draggedGrid=id;dragFromHand=hand;dragRotated=false;inventoryDragOrigin=GUIUtility.GUIToScreenPoint(e.mousePosition);}
                            }
                            else if(valid)EquipAtHand(candidateId,hand);
                            e.Use();
                        }
                    }
                }
                U.Text(new Rect(x,368,200,32),L.Format("Conjunto {0}",set+1),18,U.Ink,false,TextAnchor.UpperCenter);
            }
        }

        void DrawWeaponThumbnail(Rect rect,string id)
        {
            var weapon=inventory.Definition(id);var icon=weapon?.inventoryIcon;
            if(icon==null){U.DrawIcon(rect,weapon!=null&&weapon.isShield?"checked-shield":weapon!=null&&weapon.isBow?"target-arrows":"broadsword");return;}
            float aspect=icon.rect.width/icon.rect.height;
            float w=Mathf.Min(rect.width,rect.height*aspect),h=w/aspect;
            var uv=icon.textureRect;var texture=icon.texture;
            GUI.DrawTextureWithTexCoords(new Rect(rect.center.x-w/2,rect.center.y-h/2,w,h),texture,
                new Rect(uv.x/texture.width,uv.y/texture.height,uv.width/texture.width,uv.height/texture.height));
        }

        void HandleEquipmentDrop()
        {
            if(draggedGrid==null||!GUI.enabled)return;
            var e=Event.current;var item=inventory.GridItems(showChest).Find(value=>value.key==draggedGrid);
            if(item==null){CancelInventoryDrag();return;}
            if(InventoryDragMoved)
            {
                var ghost=new Rect(e.mousePosition.x+15,e.mousePosition.y-55,65,100);
                if(!item.material)DrawWeaponThumbnail(ghost,item.id);
                else DrawInventorySprite(ghost,inventory.Material(item.id)?.icon);
                if(e.type==EventType.MouseUp&&e.button==0)
                {
                    bool handled=false;
                    for(int slot=0;slot<4;slot++)if(consumableTargets[slot].Contains(e.mousePosition))
                    {handled=item.material&&!showChest&&inventory.AssignConsumable(slot,item.id);break;}
                    for(int hand=0;hand<4;hand++)if(handTargets[hand].Contains(e.mousePosition))
                    {handled=!item.material&&EquipAtHand(item.id,hand);break;}
                    if(!handled&&dragFromHand>=0&&!showChest&&BagViewport.Contains(e.mousePosition))
                    {
                        if(dragFromHand%2==1)handled=inventory.TryEquipOffhand(dragFromHand/2,null);
                        else {message="Arrastrá otra arma a la mano principal para reemplazarla.";messageUntil=Time.unscaledTime+4;handled=true;}
                    }
                    if(!handled&&dragFromConsumable>=0&&!showChest&&BagViewport.Contains(e.mousePosition))
                        handled=inventory.AssignConsumable(dragFromConsumable,null);
                    if(!handled){message="El objeto permanece en su lugar.";messageUntil=Time.unscaledTime+3;}
                    CancelInventoryDrag();e.Use();
                }
            }
            if(e.rawType==EventType.MouseUp)CancelInventoryDrag();
        }

        void DrawCompactSelection()
        {
            if(selected==null){U.Text(new Rect(64,633,540,48),"Seleccioná un objeto para inspeccionarlo.",18,U.Muted);return;}
            var material=selectedMaterial?inventory.Material(selected):null;
            var weapon=selectedMaterial?null:inventory.Definition(selected);
            if(material==null&&weapon==null){selected=null;showItemActions=false;return;}
            U.Text(new Rect(64,628,421,31),material!=null?material.displayName:weapon.DisplayName,23,U.Ink);
            if(U.Button(new Rect(494,625,45,35),inventory.Favorite(selected,selectedMaterial)?"★":"☆"))inventory.ToggleFavorite(selected,selectedMaterial);
            if(U.Button(new Rect(551,625,45,35),"…",showItemActions)){showItemActions=!showItemActions;CancelInventoryDrag();}
            if(material!=null)
            {
                int count=showChest?inventory.StoredMaterialCount(selected):inventory.MaterialCount(selected);
                amount=Mathf.Clamp(amount,1,Mathf.Max(1,count));
                U.Text(new Rect(64,664,240,30),L.Format("Cantidad: {0}",count),18,U.Muted);
                if(material.IsConsumable&&!showChest)
                {
                    string reason=inventory.ConsumableBlockReason(selected);bool was=GUI.enabled;GUI.enabled=was&&reason==null;
                    if(U.Button(new Rect(430,668,168,37),"Usar"))inventory.TryUseConsumable(selected);
                    GUI.enabled=was;
                    if(reason!=null)U.Text(new Rect(64,695,350,35),reason,16,U.Muted);
                }
            }
            else
            {
                var item=inventory.Item(selected);
                var current=inventory.Item(compareOffhand?inventory.OffhandId(compareSlot):inventory.EquippedId(compareSlot));
                var bonus=inventory.Rules.Bonuses(item);var baseline=inventory.Rules.Bonuses(current);
                U.Text(new Rect(64,663,540,27),"T"+item.tier+" · "+L.Text(PlayerInventory.VariantName(item.variant))+" · "+EquippedMarker(selected),17,U.Muted);
                U.Text(new Rect(64,693,540,35),L.Format("{0} · Daño {1} pp · Velocidad {2} pp",L.Text(compareOffhand?"Secundaria":"Principal")+" "+(compareSlot+1),Signed((bonus.damage-baseline.damage)*100),Signed((bonus.speed-baseline.speed)*100)),17,U.Ink);
            }
        }

        void DrawCompactActions()
        {
            if(selected==null){showItemActions=false;return;}
            PlayerHUD.Fill(ItemActionsRect,OliveTheme?new Color(.22f,.27f,.18f):new Color(.105f,.145f,.205f));U.Border(ItemActionsRect,Accent);
            float x=ItemActionsRect.x+12,y=ItemActionsRect.y+10;
            if(selectedMaterial)
            {
                int count=showChest?inventory.StoredMaterialCount(selected):inventory.MaterialCount(selected);
                if(U.Button(new Rect(x,y,40,30),"−"))amount=Mathf.Max(1,amount-1);
                U.Text(new Rect(x+45,y+2,72,28),amount.ToString(),19);
                if(U.Button(new Rect(x+111,y,40,30),"+"))amount=Mathf.Min(count,amount+1);
                if(U.Button(new Rect(x+160,y,85,30),"Todo"))amount=count;
                y+=35;
            }
            bool enabled=GUI.enabled;
            for(int set=0;set<2;set++)if(!selectedMaterial&&inventory.OffhandId(set)==selected)
            {
                GUI.enabled=enabled&&inventory.CanManage;
                if(U.Button(new Rect(x,y,249,36),"Quitar secundaria")){inventory.TryEquipOffhand(set,null);showItemActions=false;}
                GUI.enabled=enabled;y+=39;
            }
            if(inventory.AtChest)
            {
                GUI.enabled=enabled&&inventory.CanManage&&(selectedMaterial||!inventory.IsEquipped(selected));
                if(U.Button(new Rect(x,y,249,38),showChest?"Retirar del cofre":"Guardar en cofre"))
                {if(inventory.Transfer(selected,selectedMaterial,amount,!showChest)){selected=null;showItemActions=false;}}
                GUI.enabled=enabled;y+=41;
            }
            GUI.enabled=enabled&&selected!=null&&!showChest&&inventory.CanDiscard(selected,selectedMaterial);
            if(U.Button(new Rect(x,y,249,38),"Descartar…")){confirmDiscard=true;showItemActions=false;}
            GUI.enabled=enabled;
            if(U.Button(new Rect(x,ItemActionsRect.yMax-43,249,33),"Cerrar"))showItemActions=false;
        }
    }
}
