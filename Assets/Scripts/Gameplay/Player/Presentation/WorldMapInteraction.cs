using System;
using System.Collections.Generic;
using Mismo.Gameplay.Player.World;
using Mismo.Gameplay.Player.Movement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Mismo.Gameplay.Player.Presentation
{
    public sealed partial class WorldMapPanel
    {
        MapMarkerCatalog catalog;
        bool ownedCatalog,miniInteractive;
        readonly List<MapPin> pins=new List<MapPin>();
        MapPin draft;
        WorldSite? selectedVillage;
        Vector2 press;
        bool dragging,moved;
        int dragButton;
        string error;
        Vector2 paletteScroll;
        Rect Card => new Rect(Mathf.Max(8,Screen.width-312),IsOpen?76:MiniRect.yMax+10,300,250);
        public IReadOnlyList<MapPin> Pins => pins;
        void LoadPins()
        {
            catalog=Resources.Load<MapMarkerCatalog>("MapMarkerCatalog");
            if(catalog==null){catalog=ScriptableObject.CreateInstance<MapMarkerCatalog>();ownedCatalog=true;}
            pins.Clear();if(WorldSession.Current?.mapPins!=null)foreach(var pin in WorldSession.Current.mapPins)if(pin!=null)pins.Add(Clone(pin));
        }
        static MapPin Clone(MapPin p)=>new MapPin{id=p.id,typeId=p.typeId,name=p.name,x=p.x,z=p.z};
        bool Persist(List<MapPin> next)
        {
            if(WorldSession.Current!=null&&!WorldSession.SaveMapPins(next)){error=WorldSession.LastError;return false;}
            pins.Clear();pins.AddRange(next);error=null;return true;
        }
        public bool SavePin(MapPin pin)
        {
            if(pin==null||string.IsNullOrWhiteSpace(pin.id)||string.IsNullOrWhiteSpace(pin.typeId)||string.IsNullOrWhiteSpace(pin.name)||pin.name.Length>48||float.IsNaN(pin.x)||float.IsNaN(pin.z)||Mathf.Abs(pin.x)>900000||Mathf.Abs(pin.z)>900000)return false;
            var next=new List<MapPin>(pins);int index=next.FindIndex(p=>p.id==pin.id);
            if(index<0){if(next.Count>=500){error="Límite de 500 marcadores alcanzado.";return false;}next.Add(Clone(pin));}else next[index]=Clone(pin);
            return Persist(next);
        }
        public bool DeletePin(string id){var next=new List<MapPin>(pins);next.RemoveAll(p=>p.id==id);return Persist(next);}
        public static string VillageName(WorldSite site)
        {
            if(site.cell.x==int.MinValue)return "Pueblo de origen";
            string[] a={"Valle","Puerto","Monte","Villa","Paso","Bosque","Lago","Campo"};
            string[] b={"claro","verde","alto","dorado","sereno","azul","blanco","nuevo"};
            uint h=(uint)ExplorationTerrain.Hash(17,site.cell.x,site.cell.y,711);
            return a[h%8]+b[(h/8)%8];
        }
        public bool TravelTo(WorldSite site)
        {
            if(!exploration.Contains(site.position.x,site.position.z)||site.kind!=WorldSiteKind.Village||GetComponent<Mismo.Gameplay.Combat.Health>()?.IsDead==true||GetComponent<GatheringPlayer>()?.Busy==true)return false;
            var world=UnityEngine.Object.FindAnyObjectByType<ExplorationChunks>();
            if(world==null){error="El mundo todavía no está listo.";return false;}
            var arrival=site.position+(settings.content!=null?settings.content.VillageArrivalOffset:new Vector3(0,0,-55));
            arrival.y=terrain.Height(arrival.x,arrival.z)+.3f;
            // Build the destination's collision neighbourhood before moving the character.
            var cell=ExplorationChunks.Coordinate(arrival);
            for(int z=-1;z<=1;z++)for(int x=-1;x<=1;x++){var id=cell+new Vector2Int(x,z);if(!world.IsAuthored(id))world.CreateChunk(id);}
            Physics.SyncTransforms();
            if(Array.Exists(Physics.OverlapCapsule(arrival+Vector3.up*.35f,arrival+Vector3.up*1.4f,.25f,~(1<<31),QueryTriggerInteraction.Ignore),c=>!c.transform.IsChildOf(transform)))
            {error="La llegada a este pueblo está obstruida.";return false;}
            float yaw=settings.content!=null?settings.content.villageSpawnYaw:0;
            if(WorldSession.Current!=null&&!WorldSession.Checkpoint(arrival,yaw)){error=WorldSession.LastError;return false;}
            GetComponent<TreeClimbing>()?.Release();var equipment=GetComponent<Equipment.EquipmentLoadout>();equipment?.Runner.Cancel();equipment?.Belt?.Cancel();
            var motor=GetComponent<PlayerMotor>();if(motor!=null){motor.ResetPosition(arrival);motor.Face(Quaternion.Euler(0,yaw,0)*Vector3.forward);}else transform.position=arrival;
            world.RefreshResourceNavigation();selectedVillage=null;Recenter();Close();ReleaseMini();return true;
        }
        void ReleaseMini()
        {
            FinishMiniResize();
            if(!miniInteractive)return;miniInteractive=false;closedFrame=Time.frameCount;draft=null;selectedVillage=null;
            Cursor.lockState=previousLock;Cursor.visible=previousVisible;
        }
        void UpdateMiniInteraction()
        {
            bool held=Keyboard.current!=null&&(Keyboard.current.leftAltKey.isPressed||Keyboard.current.rightAltKey.isPressed);
            if(miniInteractive&&Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame){ReleaseMini();return;}
            if(!IsOpen&&!InventoryBlocked()&&held&&!miniInteractive)
            {previousLock=Cursor.lockState;previousVisible=Cursor.visible;miniInteractive=true;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
            if(miniInteractive&&(!held&&!resizingMini&&!dragging&&draft==null&&!selectedVillage.HasValue||InventoryBlocked()&&!GameplayPause.BlocksInput))ReleaseMini();
        }
        bool InventoryBlocked()=>Equipment.Inventory.InventoryPanel.AnyOpen||GameplayPause.BlocksInput||GetComponent<Mismo.Gameplay.Combat.Health>()?.IsDead==true;
        void OnGUI()
        {
            if(mapCamera==null||InventoryBlocked())return;
            HandleMiniResize();
            // Consume the wheel before marker buttons; zoom must also work over an icon.
            if(!IsOpen&&miniInteractive&&!resizingMini&&Event.current.type==EventType.ScrollWheel&&MiniRect.Contains(Event.current.mousePosition))
            {
                SetMiniZoom(miniZoom*Mathf.Exp(Event.current.delta.y*.07f));
                Event.current.Use();
            }
            int oldDepth=GUI.depth;
            if(IsOpen)
            {
                GUI.depth=-200;PlayerHUD.Fill(View,new Color(.015f,.025f,.035f,Mathf.Clamp01(catalog.backgroundOpacity)));
                if(texture!=null)GUI.DrawTexture(View,texture,ScaleMode.StretchToFill);
                if(MapIcons.Button(new Rect(Screen.width-56,12,42,42),MapSymbol.Close,"Cerrar · M"))Close();
                if(MapIcons.Button(new Rect(Screen.width-56,Screen.height-56,42,42),MapSymbol.Center,"Centrar en mí"))Recenter();
                DrawMarkers(View,true);

            }
            else
            {
                GUI.depth=-150;
                float brightness=Mismo.Gameplay.Player.World.DayNightCycle.Current?.MinimapBrightness??1f;
                var previousColor=GUI.color;
                GUI.color=new Color(brightness,brightness,brightness,1);
                if(texture!=null)GUI.DrawTexture(MiniRect,texture,ScaleMode.StretchToFill);
                DrawMarkers(MiniRect,miniInteractive);
                GUI.color=previousColor;
                if(miniInteractive||!followPlayer||miniOrbitOffset.sqrMagnitude>.001f)if(MapIcons.Button(new Rect(MiniRect.xMax-34,MiniRect.yMax-34,32,32),MapSymbol.Center,"Centrar y restablecer orientación"))Recenter();
                float radians=-renderedYaw*Mathf.Deg2Rad;var north=MiniRect.center+new Vector2(Mathf.Sin(radians),-Mathf.Cos(radians))*(MiniRect.width*.43f);
                QuietFantasyUI.Text(new Rect(north.x-10,north.y-10,20,20),"N",14,Color.white,false,TextAnchor.MiddleCenter);

            }
            if(draft!=null)DrawPinEditor();
            if(selectedVillage.HasValue)
            {
                var p=Project(selectedVillage.Value.position,IsOpen?View:MiniRect);
                var rect=new Rect(Mathf.Clamp(p.x+30,4,Screen.width-52),Mathf.Clamp(p.y+22,4,Screen.height-52),44,44);
                if(MapIcons.Button(rect,MapSymbol.Portal,"Viajar"))TravelTo(selectedVillage.Value);
            }
            if(!resizingMini&&(IsOpen||miniInteractive))HandleMapInput(IsOpen?View:MiniRect);
            if(!string.IsNullOrEmpty(error))QuietFantasyUI.Text(new Rect(20,Screen.height-80,Screen.width-90,50),error,17,new Color(1,.65f,.5f));
            GUI.depth=oldDepth;
            if(!string.IsNullOrEmpty(GUI.tooltip))
            {
                var p=Event.current.mousePosition;var r=new Rect(Mathf.Clamp(p.x+12,4,Screen.width-224),Mathf.Clamp(p.y+20,4,Screen.height-34),220,28);
                PlayerHUD.Fill(r,new Color(.03f,.045f,.06f,.96f));QuietFantasyUI.Text(r,GUI.tooltip,15,Color.white,false,TextAnchor.MiddleCenter);
            }
        }
        void DrawMarkers(Rect rect,bool interactive)
        {
            GUI.BeginGroup(rect);var local=new Rect(0,0,rect.width,rect.height);
            foreach(var site in sites)
            {
                if(!VisibleOnMap(site.position))continue;
                var p=Project(site.position,rect)-rect.position;if(!local.Contains(p))continue;
                var r=new Rect(p.x-16,p.y-16,32,32);
                if(interactive&&MapIcons.Button(r,MapSymbol.House,VillageName(site),new Color(1,.91f,.68f),catalog.villageIcon)){selectedVillage=site;draft=null;error=null;}
                else if(!interactive)DrawIcon(r,MapSymbol.House,new Color(1,.91f,.68f),catalog.villageIcon);
                if(IsOpen)DrawName(p,VillageName(site));
            }
            foreach(var pin in pins)
            {
                if(!VisibleOnMap(new Vector3(pin.x,0,pin.z)))continue;
                var p=Project(new Vector3(pin.x,0,pin.z),rect)-rect.position;if(!local.Contains(p))continue;
                var type=catalog.Find(pin.typeId);var symbol=type?.symbol??MapSymbol.Flag;var color=type?.color??new Color(1,.76f,.25f);var r=new Rect(p.x-15,p.y-15,30,30);
                if(interactive&&MapIcons.Button(r,symbol,pin.name,color,type?.icon)){draft=Clone(pin);selectedVillage=null;error=null;}
                else if(!interactive)DrawIcon(r,symbol,color,type?.icon);
                if(IsOpen)DrawName(p,pin.name);
            }
            var player=Project(transform.position,rect)-rect.position;
            if(local.Contains(player)&&VisibleOnMap(transform.position))
            {
                var r=new Rect(player.x-21,player.y-25,42,48);
                if(portrait!=null)GUI.DrawTexture(r,portrait,ScaleMode.ScaleToFit);
                else DrawIcon(new Rect(player.x-12,player.y-12,24,24),MapSymbol.Center,Color.white);
            }            GUI.EndGroup();
        }
        static void DrawIcon(Rect r,MapSymbol symbol,Color color,Texture2D custom=null)
        {var old=GUI.color;GUI.color=color;GUI.DrawTexture(r,custom!=null?MapIcons.Mask(custom):MapIcons.Get(symbol),ScaleMode.ScaleToFit);GUI.color=old;}
        static void DrawName(Vector2 p,string name)
        {
            var r=new Rect(p.x-80,p.y+18,160,22);PlayerHUD.Fill(r,new Color(.035f,.05f,.065f,.86f));QuietFantasyUI.Text(r,name,14,Color.white,false,TextAnchor.MiddleCenter);
        }
        bool resizingMini,resizeFromLeft;
        int resizeControl;
        Vector2 resizeStart;
        float resizeStartWidth;
        void FinishMiniResize()
        {
            if(!resizingMini)return;
            resizingMini=false;
            if(GUIUtility.hotControl==resizeControl)GUIUtility.hotControl=0;
            PlayerPrefs.SetFloat(MiniWidthPreference,miniWidth);PlayerPrefs.Save();
        }
        void HandleMiniResize()
        {
            int id=GUIUtility.GetControlID("MinimapResize".GetHashCode(),FocusType.Passive);
            if(IsOpen||!miniInteractive)return;
            var e=Event.current;var r=MiniRect;
            float edge=8*PlayerHUD.Scale;
            var left=new Rect(r.xMin-edge,r.yMin,edge*2,r.height+edge);
            var bottom=new Rect(r.xMin-edge,r.yMax-edge,r.width+edge,edge*2);
            // Keep the top-right anchor; drag the left or bottom border to resize.
            PlayerHUD.Fill(new Rect(r.xMin,r.yMin,1,r.height),new Color(1,1,1,.35f));
            PlayerHUD.Fill(new Rect(r.xMin,r.yMax-1,r.width,1),new Color(1,1,1,.35f));
            if(!resizingMini&&!dragging&&draft==null&&e.type==EventType.MouseDown&&e.button==0&&(left.Contains(e.mousePosition)||bottom.Contains(e.mousePosition)))
            {
                resizingMini=true;resizeFromLeft=left.Contains(e.mousePosition);resizeControl=id;
                resizeStart=e.mousePosition;resizeStartWidth=r.width/PlayerHUD.Scale;
                GUIUtility.hotControl=id;e.Use();
            }
            else if(resizingMini&&e.type==EventType.MouseDrag)
            {
                var delta=(e.mousePosition-resizeStart)/PlayerHUD.Scale;
                float change=resizeFromLeft?-delta.x:delta.y*330f/280f;
                float max=Mathf.Min(900,(Screen.width/PlayerHUD.Scale)-40,(Screen.height/PlayerHUD.Scale-84)*330f/280f);
                miniWidth=Mathf.Clamp(resizeStartWidth+change,Mathf.Min(220,max),max);e.Use();
            }
            else if(resizingMini&&e.rawType==EventType.MouseUp&&e.button==0)
            {FinishMiniResize();e.Use();}
        }
        void HandleMapInput(Rect rect)
        {
            var e=Event.current;
            if(draft!=null||selectedVillage.HasValue)
            {
                if(e.type==EventType.MouseDown&&!Card.Contains(e.mousePosition)&&!rect.Contains(e.mousePosition)){draft=null;selectedVillage=null;}
                if(draft!=null)return;
            }
            if(dragging&&e.rawType==EventType.MouseUp&&(e.type!=EventType.MouseUp||!rect.Contains(e.mousePosition))){dragging=false;return;}
            if(!dragging&&!rect.Contains(e.mousePosition))return;
            if(!dragging&&IsOpen&&(new Rect(Screen.width-64,0,64,64).Contains(e.mousePosition)||new Rect(Screen.width-64,Screen.height-64,64,64).Contains(e.mousePosition)))return;
            if(!dragging&&!IsOpen&&new Rect(MiniRect.xMax-36,MiniRect.yMax-36,36,36).Contains(e.mousePosition))return;
            if(e.type==EventType.ScrollWheel)
            {
                if(IsOpen)SetZoom(Zoom*Mathf.Exp(e.delta.y*.07f));
                else SetMiniZoom(miniZoom*Mathf.Exp(e.delta.y*.07f));
                e.Use();
            }
            if(e.type==EventType.MouseDown&&(e.button==0||e.button==1))
            {press=e.mousePosition;dragButton=e.button;dragging=true;moved=false;e.Use();}
            else if(e.type==EventType.MouseDrag&&dragging)
            {
                if((e.mousePosition-press).sqrMagnitude>25)moved=true;
                if(moved)
                {
                    if(dragButton==0)
                    {
                        var orbitDelta=new Vector2(e.delta.x*.35f,e.delta.y*.3f);
                        if(IsOpen)Orbit(orbitDelta);else if(miniInteractive)OrbitMini(orbitDelta);
                    }
                    else if(dragButton==1)
                    {
                        var rotation=Quaternion.Euler(0,renderedYaw,0);
                        var delta=rotation*new Vector3(-e.delta.x,0,e.delta.y/Mathf.Sin(renderedPitch*Mathf.Deg2Rad))*(mapCamera.orthographicSize*2/rect.height);
                        Pan(new Vector2(delta.x,delta.z));
                    }
                }
                e.Use();
            }
            else if(e.type==EventType.MouseUp&&e.button==dragButton&&dragging)
            {
                dragging=false;
                if(!moved&&dragButton==0)
                {
                    selectedVillage=null;
                    if(Pick(e.mousePosition,rect,out var p))
                    {
                        var type=catalog.types.Find(t=>t!=null&&t.available);
                        if(type!=null){draft=new MapPin{id=Guid.NewGuid().ToString("N"),typeId=type.id,name="",x=p.x,z=p.z};error=null;}
                        else error="No hay tipos de marcador habilitados en el catálogo.";
                    }
                }
                e.Use();
            }
        }        void DrawPinEditor()
        {
            PlayerHUD.Fill(Card,new Color(.04f,.06f,.08f,.98f));
            var r=Card;GUI.SetNextControlName("MapPinName");draft.name=GUI.TextField(new Rect(r.x+12,r.y+12,r.width-66,32),draft.name??"",48);
            if(MapIcons.Button(new Rect(r.xMax-44,r.y+12,32,32),MapSymbol.Close,"Cancelar")){draft=null;return;}
            var options=catalog.types.FindAll(t=>t!=null&&t.available);
            var viewport=new Rect(r.x+12,r.y+56,r.width-24,126);
            paletteScroll=Mismo.Gameplay.Player.Presentation.QuietFantasyUI.BeginScrollView(viewport,paletteScroll,new Rect(0,0,250,Mathf.Max(126,Mathf.CeilToInt(options.Count/5f)*48)));
            for(int i=0;i<options.Count;i++)
            {
                var type=options[i];var cell=new Rect(i%5*48,i/5*48,42,42);
                if(type.id==draft.typeId)QuietFantasyUI.Border(cell,QuietFantasyUI.Amber,2);
                if(MapIcons.Button(cell,type.symbol,type.label,type.color,type.icon))draft.typeId=type.id;
            }
            GUI.EndScrollView();
            bool enabled=GUI.enabled;GUI.enabled=!string.IsNullOrWhiteSpace(draft.name);
            if(MapIcons.Button(new Rect(r.xMax-54,r.yMax-52,40,40),MapSymbol.Confirm,"Guardar marcador"))
            {draft.name=draft.name.Trim();if(SavePin(draft))draft=null;}
            GUI.enabled=enabled;
            if(draft!=null&&pins.Exists(p=>p.id==draft.id)&&MapIcons.Button(new Rect(r.x+12,r.yMax-52,40,40),MapSymbol.Delete,"Eliminar marcador"))if(DeletePin(draft.id))draft=null;
        }
    }
}





