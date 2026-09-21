using System;
using System.Linq;
using Mismo.Gameplay.Player.Equipment.Inventory;
using Mismo.Gameplay.Player.Quests;
using UnityEngine;
using U=Mismo.Gameplay.Player.Presentation.QuietFantasyUI;
using L=Mismo.Gameplay.Player.Localization.GameLanguage;

namespace Mismo.Gameplay.Player.Presentation
{
    /// <summary>The journal reads live inventory counts; only dialogue delivers rewards.</summary>
    public sealed class QuestJournalView
    {
        public QuestDefinition Selected {get;private set;}
        int filter;
        Vector2 listScroll,detailScroll,rewardScroll,dialogueScroll;
        bool dialogue,clueOpen,worldContact;
        QuestGiver giver;
        string reply;
        RecipeUITheme theme;
        public static bool TrackerHidden
        {
            get=>PlayerPrefs.GetInt("Mismo.HUD.Quests.Hidden",0)!=0;
            set{PlayerPrefs.SetInt("Mismo.HUD.Quests.Hidden",value?1:0);PlayerPrefs.Save();}
        }
        const string TrackerPositionKey="Mismo.HUD.Quests.Position";
        int trackerDragControl;
        Vector2 trackerDragOffset;
        readonly System.Collections.Generic.Dictionary<string,GUIStyle> pieces=new System.Collections.Generic.Dictionary<string,GUIStyle>();
        static readonly string[] Filters={"Todas","Pueblo","Misterios","Eventos","Completadas"};
        public bool Back(){if(!dialogue)return false;dialogue=false;giver=null;worldContact=false;return true;}
        public void Select(QuestDefinition quest){Selected=quest;detailScroll=Vector2.zero;rewardScroll=Vector2.zero;clueOpen=false;reply=null;}
        public void Contact(QuestDefinition quest,QuestGiver source=null)
        {Select(quest);giver=source;worldContact=source!=null;dialogue=true;dialogueScroll=Vector2.zero;}
        void Piece(Rect rect,string key)
        {
            if(key=="WindowSurface"){FantasyUI.Surface(rect);return;}
            if(key=="SectionDividerHorizontal"||key=="SectionDividerVertical"){PlayerHUD.Fill(rect,U.Rule*.4f);return;}
            if(theme==null)theme=RecipeUITheme.Load();
            if(theme?.Texture(key)==null){if(key=="WindowSurface")FantasyUI.Panel(rect);else if(key=="WindowFrame")FantasyUI.Frame(rect,U.Ink);return;}
            if(!pieces.TryGetValue(key,out var style))
            {var b=theme.Border(key);style=new GUIStyle{border=new RectOffset((int)b.x,(int)b.z,(int)b.w,(int)b.y)};style.normal.background=theme.Texture(key);pieces[key]=style;}
            GUI.Box(rect,GUIContent.none,style);
        }
        public static void Sprite(Rect rect,Sprite sprite)
        {
            if(sprite==null)return;
            float ratio=sprite.rect.width/sprite.rect.height,w=Mathf.Min(rect.width,rect.height*ratio),h=w/ratio;
            var uv=sprite.textureRect;var t=sprite.texture;
            GUI.DrawTextureWithTexCoords(new Rect(rect.center.x-w/2,rect.center.y-h/2,w,h),t,new Rect(uv.x/t.width,uv.y/t.height,uv.width/t.width,uv.height/t.height));
        }
        static float TextHeight(string text,float width,int size)
        {var style=new GUIStyle(GUI.skin.label){font=U.Body,fontSize=size,wordWrap=true,padding=new RectOffset()};return Mathf.Max(size+8,style.CalcHeight(new GUIContent(L.Text(text??"")),width));}
        static string StateLabel(PlayerInventory p,QuestDefinition q)
        {var s=p.QuestState(q);return s==null?"Pedido disponible":s.completed?"Completada":p.QuestReady(q)?"Para entregar":"En curso";}
        bool ContactAllowed(PlayerInventory p)=>worldContact?giver!=null&&giver.CanTalk(p,Selected):Selected!=null&&p.Quests.journalContacts&&Selected.offerInJournal;
        public void Draw(PlayerInventory p,Action close,Action backToMenu=null)
        {
            var settings=p.Quests;
            if(settings==null){FantasyUI.Panel(U.MenuWindow);U.Text(new Rect(70,100,1100,80),"No hay un catálogo de misiones configurado.");if(U.Button(new Rect(990,690,220,48),"Cerrar"))close();return;}
            var icons=Mismo.Core.ProjectAssets.Load<InventoryUIIcons>("InventoryUIIcons"); U.MenuBackground(icons); U.MenuDivider();
            if(dialogue){DrawDialogue(p);return;}
            U.MenuHeading(icons?.radialQuests??settings.journalIcon,"Misiones");
            DrawVisibilityToggle(new Rect(1030,38,44,47),icons);
            if(backToMenu!=null&&U.NavigationButton(U.MenuBackButton,icons?.menu,"Menú [B]",icons)){backToMenu();return;}
            U.Text(new Rect(785,49,230,30),p.QuestCoins+" monedas",18,U.Amber,false,TextAnchor.MiddleRight);
            if(U.CloseButton(U.MenuCloseButton)){close();return;}
            for(int i=0;i<Filters.Length;i++)if(U.Button(new Rect(58+i*233,104,219,44),Filters[i],filter==i)){filter=i;listScroll=Vector2.zero;}
            var visible=settings.quests.Where(q=>q!=null&&q.Validate(out _)&&(p.QuestState(q)!=null||settings.journalContacts&&q.offerInJournal&&p.CanAcceptQuest(q)))
                .Where(q=>filter==4?p.QuestState(q)?.completed==true:p.QuestState(q)?.completed!=true&&(filter==0||(int)q.category==filter-1)).ToArray();
            if(!visible.Contains(Selected))Select(visible.FirstOrDefault());
            listScroll=U.BeginScrollView(new Rect(58,168,455,384),listScroll,new Rect(0,0,432,Mathf.Max(384,visible.Length*83)));
            for(int i=0;i<visible.Length;i++)
            {
                var q=visible[i];var rect=new Rect(0,i*83,430,80);
                if(Selected==q)Piece(rect,"RowSelected");else if(rect.Contains(Event.current.mousePosition))Piece(rect,"RowHover");
                if(GameAudio.Button(rect,"",GUIStyle.none))Select(q);
                if(q.icon!=null)Sprite(new Rect(7,rect.y+15,42,42),q.icon);else U.DrawIcon(new Rect(10,rect.y+19,30,30),MapIcons.Mask(icons?.radialQuests??settings.journalIcon));
                U.Text(new Rect(62,rect.y+7,352,43),q.title,20,Selected==q?U.Amber:U.Ink);
                U.Text(new Rect(62,rect.y+53,351,23),(p.TrackedQuest==q?"◆ ":"")+StateLabel(p,q),16,U.Muted);
            }
            GUI.EndScrollView();
            Piece(new Rect(532,168,5,384),"SectionDividerVertical");
            if(Selected!=null)DrawDetail(p);else U.Text(new Rect(570,258,605,130),"No hay misiones en esta categoría. Hablá con los habitantes del pueblo que tengan un ! para recibir un pedido.",24,U.Muted);
            Piece(new Rect(58,576,1164,5),"SectionDividerHorizontal");
            U.Text(new Rect(58,591,560,29),"RECOMPENSAS",19,U.Ink,true);
            if(Selected==null)return;
            DrawRewards(Selected);
            var state=p.QuestState(Selected);
            if(state!=null&&!state.completed&&U.Button(new Rect(977,632,245,46),p.TrackedQuest==Selected?"Dejar de seguir":"Seguir misión",p.TrackedQuest==Selected))p.TrackQuest(p.TrackedQuest==Selected?null:Selected);
            if(ContactAllowed(p))
            {if(U.Button(new Rect(977,696,245,46),state==null?"Leer pedido":"Hablar con "+Selected.npc.displayName))Contact(Selected);}
            else U.Text(new Rect(977,690,245,63),"Visitá a "+Selected.npc.displayName,18,U.Muted,false,TextAnchor.MiddleCenter);
        }
        void DrawDetail(PlayerInventory p)
        {
            var q=Selected;var state=p.QuestState(q);float width=630;
            float descriptionHeight=TextHeight(q.description,width,20);
            float titleHeight=TextHeight(q.title,width,29);
            float objectivesHeight=q.objectives.Sum(o=>Mathf.Max(75,TextHeight(o.label,width-133,19)+20));
            bool showClue=!string.IsNullOrEmpty(q.clue)&&state!=null&&(string.IsNullOrEmpty(q.clueAfterObjective)||q.objectives.Any(o=>o.id==q.clueAfterObjective&&p.QuestCount(q,o)>=o.quantity));
            float total=titleHeight+descriptionHeight+objectivesHeight+210+(showClue?clueOpen?TextHeight(q.clue,width-20,20)+64:54:0);
            detailScroll=U.BeginScrollView(new Rect(563,168,660,384),detailScroll,new Rect(0,0,width,Mathf.Max(384,total)));
            U.Text(new Rect(0,0,width,24),q.category+" · "+StateLabel(p,q),16,U.Muted);
            U.Text(new Rect(0,32,width,titleHeight),q.title,29,U.Ink,true);float y=40+titleHeight;
            U.Text(new Rect(0,y,width,28),q.npc.displayName+" · "+q.npc.occupation,18,U.Muted);y+=29;
            U.Text(new Rect(0,y,width,28),q.location,17,U.Muted);y+=43;
            U.Text(new Rect(0,y,width,descriptionHeight),q.description,20,U.Ink);y+=descriptionHeight+18;
            U.Text(new Rect(0,y,width,25),"OBJETIVOS",17,U.Muted);y+=29;
            foreach(var o in q.objectives)
            {
                int count=p.QuestCount(q,o);float h=Mathf.Max(75,TextHeight(o.label,width-133,19)+20);
                U.Text(new Rect(0,y+17,25,30),state!=null&&count>=o.quantity?"✓":"○",22,count>=o.quantity?new Color(.66f,.84f,.71f):U.Muted);
                U.Text(new Rect(33,y+15,width-133,h-10),o.label,19,U.Ink);
                var sprite=o.icon!=null?o.icon:o.material?.icon;
                if(sprite!=null)Sprite(new Rect(width-67,y,54,48),sprite);
                else if(o.material!=null)U.DrawIcon(new Rect(width-67,y,48,48),"crystal-cluster",U.Ink);
                U.Text(new Rect(width-86,y+(sprite!=null||o.material!=null?49:20),82,26),count+" / "+o.quantity,18,U.Amber,false,TextAnchor.MiddleCenter);y+=h;
            }
            if(state!=null&&!state.completed){U.Text(new Rect(33,y,width-40,40),"Volvé con "+q.npc.displayName+" para entregar el pedido.",17,U.Muted);y+=48;}
            if(showClue)
            {
                if(U.Button(new Rect(0,y,width,42),clueOpen?"Ocultar pista":"Inscripción encontrada"))clueOpen=!clueOpen;
                y+=52;if(clueOpen)U.Text(new Rect(10,y,width-20,TextHeight(q.clue,width-20,20)),q.clue,20,U.Amber);
            }
            GUI.EndScrollView();
        }
        void DrawRewards(QuestDefinition q)
        {
            int count=(q.coins>0?1:0)+q.items.Length+q.recipes.Length;
            rewardScroll=U.BeginScrollView(new Rect(58,627,888,124),rewardScroll,new Rect(0,0,856,Mathf.Max(124,((count+1)/2)*69)));
            int index=0;
            Action<Sprite,string,string> row=(sprite,title,sub)=>
            {float x=(index%2)*428,y=(index/2)*69;Sprite(new Rect(x,y+4,48,48),sprite);if(sprite==null)U.DrawIcon(new Rect(x+5,y+8,34,34),pictogram(sub),U.Amber);U.Text(new Rect(x+62,y,350,30),title,19,U.Ink);U.Text(new Rect(x+62,y+32,350,26),sub,16,U.Muted);index++;};
            if(q.coins>0)row(null,q.coins+" monedas","Pago del encargo");
            foreach(var item in q.items)row(item.material.icon,item.quantity+" × "+item.material.displayName,"Objeto");
            foreach(var recipe in q.recipes)row(recipe.result?.icon??recipe.weaponResult?.inventoryIcon,recipe.displayName,"Receta");
            GUI.EndScrollView();
        }
        static string pictogram(string sub)=>sub=="Receta"?"open-book":"locked-chest";
        static string Choose(string specific,string fallback)=>string.IsNullOrWhiteSpace(specific)?fallback:specific;
        void DrawDialogue(PlayerInventory p)
        {
            if(!ContactAllowed(p)){dialogue=false;return;}
            if(Selected==null)
            {
                var contact=giver.npc;
                U.Text(new Rect(78,49,1010,50),contact.displayName+" · "+contact.occupation,30,U.Ink,true);
                U.Text(new Rect(150,220,980,220),contact.greeting+"\n\n"+contact.noRequests,24,U.Ink);
                if(U.Button(new Rect(977,708,245,43),"Despedirse")){Back();p.GetComponent<InventoryPanel>().Close();}return;
            }
            var q=Selected;var npc=q.npc;var state=p.QuestState(q);
            U.Text(new Rect(78,49,1010,50),npc.displayName+" · "+npc.occupation,30,U.Ink,true);
            if(U.CloseButton(U.MenuCloseButton)){bool physical=worldContact;Back();if(physical)p.GetComponent<InventoryPanel>().Close();return;}
            if(npc.portrait!=null)Sprite(new Rect(79,142,166,190),npc.portrait);
            else U.DrawIcon(new Rect(113,185,80,80),p.Quests.journalIcon,U.Amber);
            U.Text(new Rect(275,134,875,61),q.title,27,U.Amber,true);
            string text=reply??(state==null?Choose(q.offerText,npc.offer):state.completed?Choose(q.completedText,npc.completed):p.QuestReady(q)?Choose(q.readyText,npc.readyToDeliver):Choose(q.progressText,npc.inProgress));
            if(state==null)text=npc.greeting+"\n\n"+text;
            float height=TextHeight(text,858,24);
            dialogueScroll=U.BeginScrollView(new Rect(275,212,901,318),dialogueScroll,new Rect(0,0,858,Mathf.Max(318,height)));
            U.Text(new Rect(0,0,858,height),text,24,U.Ink);GUI.EndScrollView();
            Piece(new Rect(58,576,1164,5),"SectionDividerHorizontal");U.Text(new Rect(58,591,500,29),"RECOMPENSAS",19,U.Ink,true);DrawRewards(q);
            if(state==null)
            {
                if(U.Button(new Rect(977,641,245,49),"Aceptar pedido"))
                {if(p.TryAcceptQuest(q)){reply=Choose(q.acceptedText,npc.accepted);dialogueScroll=Vector2.zero;}else reply=p.Notice;}
            }
            else if(!state.completed)
            {
                if(U.Button(new Rect(977,641,245,49),"Entregar pedido"))
                {reply=p.TryDeliverQuest(q)?Choose(q.completedText,npc.completed):p.Notice;dialogueScroll=Vector2.zero;}
            }
            if(U.Button(new Rect(977,708,245,43),worldContact?"Despedirse":"Volver al diario")){bool physical=worldContact;Back();if(physical)p.GetComponent<InventoryPanel>().Close();}
        }
        static void DrawVisibilityToggle(Rect rect,InventoryUIIcons icons)
        {
            var texture=TrackerHidden?icons?.eye:icons?.eyeCrossed;
            string hint=TrackerHidden?"Mostrar seguimiento":"Ocultar seguimiento";
            if(GameAudio.Button(rect,"",GUIStyle.none))TrackerHidden=!TrackerHidden;
            GUI.Label(rect,new GUIContent("",L.Text(hint)),GUIStyle.none);
            float size=Mathf.Min(rect.width-8,icons!=null?icons.navigationIconSize:24);
            var tint=U.Ink;tint.a*=icons!=null?icons.navigationIconOpacity:1;
            U.DrawIcon(new Rect(rect.center.x-size/2,rect.center.y-size/2,size,size),MapIcons.Mask(texture),tint);
        }
        public void DrawTracker(PlayerInventory p)
        {
            bool editing=PlayerHUD.UIEditMode;
            var settings=p.Quests;var q=p.TrackedQuest;
            if(settings==null||WorldMapPanel.AnyOpen)return;
            if(q!=null&&!q.Validate(out _))q=null;
            if(!editing&&(!settings.showTracker||TrackerHidden||q==null))return;
            // Anchor the HUD to the screen edge, independently of the centered journal canvas.
            var previousMatrix=GUI.matrix;
            float scale=Mathf.Max(.1f,Mathf.Min(Screen.width/1280f,Screen.height/800f));
            GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);
            float width=settings.trackerWidth;var offset=settings.trackerOffset;
            if(PlayerPrefs.HasKey(TrackerPositionKey+".x"))offset=new Vector2(PlayerPrefs.GetFloat(TrackerPositionKey+".x"),PlayerPrefs.GetFloat(TrackerPositionKey+".y"));
            string title=q!=null?q.title:"Misiones";
            string step=q!=null?"Volvé con "+q.npc.displayName:"Acá aparecerá la misión que sigas.";
            var objective=q?.objectives.FirstOrDefault(o=>p.QuestCount(q,o)<o.quantity);
            if(objective!=null)step=objective.label+" · "+p.QuestCount(q,objective)+" / "+objective.quantity;
            float titleHeight=TextHeight(title,width-76,21),stepHeight=TextHeight(step,width-80,17);
            var rect=new Rect(offset.x,offset.y,width,titleHeight+stepHeight+40+(editing?26:0));
            var viewport=new Vector2(Screen.width/scale,Screen.height/scale);
            rect.position=ClampTrackerPosition(rect.position,rect.size,viewport);
            if(editing)EditTracker(ref rect,viewport,settings.trackerOffset);
            FantasyUI.Panel(rect,settings.panelOpacity);PlayerHUD.Fill(new Rect(rect.x,rect.y,3,rect.height),U.Amber);
            U.Text(new Rect(rect.x+16,rect.y+9,width-76,titleHeight),title,21,U.Amber);
            var icons=Mismo.Core.ProjectAssets.Load<InventoryUIIcons>("InventoryUIIcons");
            bool enabled=GUI.enabled;GUI.enabled=enabled&&Cursor.lockState!=CursorLockMode.Locked;
            DrawVisibilityToggle(new Rect(rect.xMax-46,rect.y+6,36,32),icons);GUI.enabled=enabled;
            var sprite=objective?.icon!=null?objective.icon:objective?.material?.icon;
            if(sprite!=null)Sprite(new Rect(rect.x+14,rect.y+titleHeight+17,36,36),sprite);
            U.Text(new Rect(rect.x+(sprite!=null?58:16),rect.y+titleHeight+17,width-80,stepHeight),step,17,U.Ink);
            if(editing)
            {
                U.Border(rect,U.Amber,2);
                U.Text(new Rect(rect.x+12,rect.yMax-25,width-24,21),"Arrastrar · Clic derecho: restablecer",14,U.Amber);
            }
            GUI.matrix=previousMatrix;
        }
        static Vector2 ClampTrackerPosition(Vector2 position,Vector2 size,Vector2 viewport)
        {return new Vector2(Mathf.Clamp(position.x,0,Mathf.Max(0,viewport.x-size.x)),Mathf.Clamp(position.y,0,Mathf.Max(0,viewport.y-size.y)));}
        public void EndTrackerEdit()
        {
            if(trackerDragControl==0)return;
            if(GUIUtility.hotControl==trackerDragControl)GUIUtility.hotControl=0;
            trackerDragControl=0;PlayerPrefs.Save();
        }
        void EditTracker(ref Rect rect,Vector2 viewport,Vector2 defaultPosition)
        {
            int control=GUIUtility.GetControlID(TrackerPositionKey.GetHashCode(),FocusType.Passive,rect);
            var e=Event.current;
            switch(e.GetTypeForControl(control))
            {
                case EventType.MouseDown:
                    if(!rect.Contains(e.mousePosition)||new Rect(rect.xMax-46,rect.y+6,36,32).Contains(e.mousePosition))break;
                    if(e.button==1)
                    {
                        PlayerPrefs.DeleteKey(TrackerPositionKey+".x");PlayerPrefs.DeleteKey(TrackerPositionKey+".y");PlayerPrefs.Save();
                        rect.position=ClampTrackerPosition(defaultPosition,rect.size,viewport);e.Use();
                    }
                    else if(e.button==0)
                    {GUIUtility.hotControl=control;trackerDragControl=control;trackerDragOffset=e.mousePosition-rect.position;e.Use();}
                    break;
                case EventType.MouseDrag:
                    if(GUIUtility.hotControl!=control||trackerDragControl!=control)break;
                    rect.position=ClampTrackerPosition(e.mousePosition-trackerDragOffset,rect.size,viewport);
                    PlayerPrefs.SetFloat(TrackerPositionKey+".x",rect.x);PlayerPrefs.SetFloat(TrackerPositionKey+".y",rect.y);e.Use();
                    break;
                case EventType.MouseUp:
                    if(e.button!=0||GUIUtility.hotControl!=control||trackerDragControl!=control)break;
                    GUIUtility.hotControl=0;trackerDragControl=0;PlayerPrefs.Save();e.Use();
                    break;
            }
        }
    }
}
