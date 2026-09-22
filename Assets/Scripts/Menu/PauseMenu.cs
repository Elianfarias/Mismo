using System.Collections.Generic;
using Mismo.Gameplay.Player;
using Mismo.Gameplay.Player.Equipment.Inventory;
using Mismo.Gameplay.Player.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using U=Mismo.Gameplay.Player.Presentation.QuietFantasyUI;
using L=Mismo.Gameplay.Player.Localization.GameLanguage;

namespace Mismo.Menu
{
    [DefaultExecutionOrder(-10000)]
    public sealed class PauseMenu : MonoBehaviour
    {
        bool opened,options,confirmExit,vsync,uiEditor;
        int tab,mode,resolution,quality,fps;
        InventoryUIIcons icons;
        readonly MenuBackgroundBlur backgroundBlur=new MenuBackgroundBlur();
        readonly List<Vector2Int> resolutions=new List<Vector2Int>();
        static readonly int[] FrameCaps={30,60,90,120,144,240,-1};
        static readonly FullScreenMode[] Modes={FullScreenMode.Windowed,FullScreenMode.FullScreenWindow,FullScreenMode.ExclusiveFullScreen};
        static readonly string[] ModeNames={"Ventana","Sin bordes","Pantalla completa"};
        FullScreenMode previousMode;
        int previousWidth,previousHeight;
        float revertAt;
        Vector2 controlScroll;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register(){SceneManager.sceneLoaded-=OnSceneLoaded;SceneManager.sceneLoaded+=OnSceneLoaded;}
        static void OnSceneLoaded(Scene scene,LoadSceneMode loadMode)
        {
            foreach(var root in scene.GetRootGameObjects())if(root.GetComponentInChildren<PlayerController>(true)!=null)
            {if(Object.FindAnyObjectByType<PauseMenu>()!=null)return;var go=new GameObject("Pause menu");SceneManager.MoveGameObjectToScene(go,scene);go.AddComponent<PauseMenu>();return;}
        }
        void Awake()
        {
            icons=Mismo.Core.ProjectAssets.Load<InventoryUIIcons>("InventoryUIIcons");
            if(PlayerPrefs.HasKey("Mismo.Quality"))QualitySettings.SetQualityLevel(Mathf.Clamp(PlayerPrefs.GetInt("Mismo.Quality"),0,QualitySettings.names.Length-1),true);
            if(PlayerPrefs.HasKey("Mismo.VSync"))QualitySettings.vSyncCount=PlayerPrefs.GetInt("Mismo.VSync");
            if(PlayerPrefs.HasKey("Mismo.FPS"))Application.targetFrameRate=PlayerPrefs.GetInt("Mismo.FPS");
            if(!Application.isEditor&&PlayerPrefs.HasKey("Mismo.ScreenWidth"))Screen.SetResolution(PlayerPrefs.GetInt("Mismo.ScreenWidth"),PlayerPrefs.GetInt("Mismo.ScreenHeight"),(FullScreenMode)PlayerPrefs.GetInt("Mismo.ScreenMode"));
        }
        void Update()
        {
            if(revertAt>0&&Time.unscaledTime>=revertAt)RevertDisplay();
            if(Keyboard.current?.escapeKey.wasPressedThisFrame!=true)return;
            if(opened){if(uiEditor)EndUIEditor();else if(revertAt>0)RevertDisplay();else if(confirmExit)confirmExit=false;else if(options)options=false;else Resume();return;}
            if(!InventoryPanel.AnyOpen&&!WorldMapPanel.BlocksGameplay)Open();
        }
        void LateUpdate()
        {
            backgroundBlur.Update(icons!=null&&icons.menuBackgroundBlur&&
                (opened||InventoryPanel.AnyOpen||WorldMapPanel.AnyOpen),icons!=null?icons.menuBlurRadius:1.5f);
        }
        public void Open()
        {
            if(GameplayPause.IsPaused||InventoryPanel.AnyOpen||WorldMapPanel.BlocksGameplay)return;
            GameplayPause.Pause();opened=true;options=false;confirmExit=false;ReadSettings();GameAudio.Play(GameSound.MenuOpen);
        }
        public void Resume()
        {if(!opened)return;if(uiEditor)EndUIEditor();if(revertAt>0)RevertDisplay();opened=false;GameplayPause.Resume();PlayerPrefs.Save();GameAudio.Play(GameSound.MenuClose);}
        void BeginUIEditor(){uiEditor=true;options=false;PlayerHUD.SetUIEditMode(true);GameAudio.Play(GameSound.MenuOpen);}
        void EndUIEditor(){uiEditor=false;PlayerHUD.SetUIEditMode(false);options=true;PlayerPrefs.Save();GameAudio.Play(GameSound.MenuClose);}
        void OnDisable(){Resume();backgroundBlur.Dispose();}
        void ReadSettings()
        {
            resolutions.Clear();foreach(var value in Screen.resolutions){var size=new Vector2Int(value.width,value.height);if(!resolutions.Contains(size))resolutions.Add(size);}
            var current=new Vector2Int(Screen.width,Screen.height);if(!resolutions.Contains(current))resolutions.Add(current);
            resolution=resolutions.IndexOf(current);mode=Mathf.Max(0,System.Array.IndexOf(Modes,Screen.fullScreenMode));quality=QualitySettings.GetQualityLevel();vsync=QualitySettings.vSyncCount>0;
            fps=System.Array.IndexOf(FrameCaps,Application.targetFrameRate);if(fps<0)fps=6;
        }
        bool Button(Rect rect,string label)=>U.Button(rect,label);
        void Text(float x,float y,float width,string text,int size=21)=>U.Text(new Rect(x,y,width,40),text,size,U.Ink);
        bool IconButton(Rect rect,Texture2D texture,string caption,bool selected=false,bool label=false)
        {
            bool clicked=U.Button(rect,texture==null?caption:"",selected);
            if(texture!=null)
            {
                var previous=GUI.color;var tint=selected?U.Amber:U.Ink;tint.a*=icons!=null?Mathf.Clamp01(icons.navigationIconOpacity):1;GUI.color=tint;
                float x=label?rect.x+18:rect.center.x-16;
                GUI.DrawTexture(new Rect(x,rect.center.y-16,32,32),MapIcons.Mask(texture),ScaleMode.ScaleToFit);GUI.color=previous;
                if(label)U.Text(new Rect(rect.x+65,rect.y,rect.width-80,rect.height),caption,21,U.Ink,false,TextAnchor.MiddleLeft);
            }
            GUI.Label(rect,new GUIContent("",caption),GUIStyle.none);return clicked;
        }
        void OnGUI()
        {
            if(!opened)return;var matrix=GUI.matrix;int depth=GUI.depth;GUI.depth=-100;
            float opacity=icons!=null?Mathf.Clamp01(icons.panelOpacity):1;
            float scale=Mathf.Min(Screen.width/1280f,Screen.height/800f)*.9f;
            GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1280*scale)/2,(Screen.height-800*scale)/2,0),Quaternion.identity,Vector3.one*scale);
            var panel=options?new Rect(155,80,970,640):new Rect(460,250,360,285);
            if(uiEditor)
            {
                FantasyUI.Panel(new Rect(320,20,640,290),opacity);
                Text(390,40,250,"Modificar HUD",24);
                Text(390,70,320,"Arrastrá cada bloque para moverlo",16);
                if(icons!=null)
                {
                    Text(390,102,180,"Fondo de slots",16);Text(820,102,90,Mathf.RoundToInt(icons.hudBackgroundOpacity*100)+" %",16);
                    float background=GUI.HorizontalSlider(new Rect(570,111,240,16),icons.hudBackgroundOpacity,0,1);
                    Text(390,135,180,"Barras",16);Text(820,135,90,Mathf.RoundToInt(icons.hudVitalsOpacity*100)+" %",16);
                    float vitals=GUI.HorizontalSlider(new Rect(570,144,240,16),icons.hudVitalsOpacity,0,1);
                    Text(390,168,180,"Tab",16);Text(820,168,90,Mathf.RoundToInt(icons.hudTabOpacity*100)+" %",16);
                    float tab=GUI.HorizontalSlider(new Rect(570,177,240,16),icons.hudTabOpacity,0,1);
                    if(!Mathf.Approximately(background,icons.hudBackgroundOpacity)){icons.hudBackgroundOpacity=background;PlayerPrefs.SetFloat("Mismo.HUD.BackgroundOpacity",background);}
                    if(!Mathf.Approximately(vitals,icons.hudVitalsOpacity)){icons.hudVitalsOpacity=vitals;PlayerPrefs.SetFloat("Mismo.HUD.VitalsOpacity",vitals);}
                    if(!Mathf.Approximately(tab,icons.hudTabOpacity)){icons.hudTabOpacity=tab;PlayerPrefs.SetFloat("Mismo.HUD.TabOpacity",tab);}
                }
                if(icons!=null)
                {
                    if(Button(new Rect(390,210,135,38),icons.hudSkillsAsColumn?"Skills: columna":"Skills: fila")){icons.hudSkillsAsColumn=!icons.hudSkillsAsColumn;PlayerPrefs.SetInt("Mismo.HUD.SkillsColumn",icons.hudSkillsAsColumn?1:0);}
                    if(Button(new Rect(535,210,135,38),icons.hudConsumablesAsColumn?"Items: columna":"Items: fila")){icons.hudConsumablesAsColumn=!icons.hudConsumablesAsColumn;PlayerPrefs.SetInt("Mismo.HUD.ConsumablesColumn",icons.hudConsumablesAsColumn?1:0);}
                }
                if(Button(new Rect(680,210,130,38),"Guardar"))EndUIEditor();
                if(Button(new Rect(820,210,120,38),"Restablecer")){if(icons!=null){icons.hudBackgroundOpacity=.65f;icons.hudVitalsOpacity=1;icons.hudTabOpacity=1;PlayerPrefs.SetFloat("Mismo.HUD.BackgroundOpacity",.65f);PlayerPrefs.SetFloat("Mismo.HUD.VitalsOpacity",1);PlayerPrefs.SetFloat("Mismo.HUD.TabOpacity",1);}PlayerPrefs.Save();}
                if(icons!=null&&Button(new Rect(390,255,220,30),icons.hudShowActionSeparators?"Pipes: visibles":"Pipes: ocultos")){icons.hudShowActionSeparators=!icons.hudShowActionSeparators;PlayerPrefs.SetInt("Mismo.HUD.ActionSeparators",icons.hudShowActionSeparators?1:0);}
                GUI.matrix=matrix;GUI.depth=depth;return;
            }
            FantasyUI.Panel(panel,opacity);
            U.Border(new Rect(panel.x+28,panel.y+75,panel.width-56,1),U.Rule);Text(panel.x+30,panel.y+23,350,options?"Opciones":"Pausa",27);
            if(U.CloseButton(new Rect(panel.xMax-68,panel.y+18,42,42)))Resume();
            if(revertAt>0)
            {
                Text(panel.x+35,panel.y+115,panel.width-70,"¿Conservar la configuración de pantalla?",23);
                Text(panel.x+35,panel.y+180,panel.width-70,"Se restablece en "+Mathf.CeilToInt(revertAt-Time.unscaledTime)+" s",20);
                if(Button(new Rect(panel.x+35,panel.y+265,240,48),"Conservar"))KeepDisplay();
                if(Button(new Rect(panel.x+300,panel.y+265,240,48),"Revertir"))RevertDisplay();
            }
            else if(confirmExit)
            {
                Text(445,275,385,"¿Volver al menú principal?",23);
                if(Button(new Rect(450,380,380,48),"Volver al menú principal")){Resume();SceneManager.LoadScene("MainMenu");}
                if(Button(new Rect(450,455,380,48),"Cancelar"))confirmExit=false;
            }
            else if(!options)
            {
                if(IconButton(new Rect(panel.x+30,panel.y+92,300,44),icons?.pauseResume,"Continuar",false,true))Resume();
                if(IconButton(new Rect(panel.x+30,panel.y+148,300,44),icons?.pauseOptions,"Opciones",false,true)){ReadSettings();options=true;}
                if(IconButton(new Rect(panel.x+30,panel.y+204,300,44),icons?.pauseMainMenu,"Menú principal",false,true))confirmExit=true;
            }
            else
            {
                string[] tabs={"Sonido","Pantalla","Gráficos","Controles"};Texture2D[] art={icons?.settingsSound,icons?.settingsDisplay,icons?.settingsGraphics,icons?.settingsControls};
                for(int i=0;i<4;i++)if(IconButton(new Rect(185+i*223,180,210,44),art[i],tabs[i],tab==i,true)){tab=i;GameAudio.Play(GameSound.TabChanged);}
                if(tab==0)DrawSound();else if(tab==1)DrawDisplay();else if(tab==2)DrawGraphics();else DrawControls();
                if(IconButton(new Rect(185,645,140,43),icons?.previousPage,"Volver",false,true))options=false;
            }
            GUI.matrix=matrix;GUI.depth=depth;
        }
        void DrawSound()
        {
            string[] names={"Música","Efectos","Interfaz"},keys={"Mismo.Music","Mismo.SFX","Mismo.UI"};
            for(int i=0;i<3;i++){float y=270+i*105,value=PlayerPrefs.GetFloat(keys[i],.75f);Text(215,y,400,names[i]);Text(960,y,110,Mathf.RoundToInt(value*100)+" %");float next=GUI.HorizontalSlider(new Rect(215,y+47,835,22),value,0,1);if(!Mathf.Approximately(value,next)){PlayerPrefs.SetFloat(keys[i],next);VolumeSettings.ApplySaved(AudioRuntime.Mixer);}}
        }
        int Choice(float y,string title,int index,string[] values)
        {
            Text(215,y+6,360,title);if(values.Length==0)return 0;
            if(Button(new Rect(620,y,45,43),"‹"))index=(index+values.Length-1)%values.Length;
            U.Text(new Rect(675,y+4,315,38),values[index],21,U.Ink,false,TextAnchor.MiddleCenter);
            if(Button(new Rect(1000,y,45,43),"›"))index=(index+1)%values.Length;return index;
        }
        void DrawDisplay()
        {
            mode=Choice(280,"Modo de pantalla",mode,ModeNames);resolution=Choice(370,"Resolución",resolution,resolutions.ConvertAll(r=>r.x+" × "+r.y).ToArray());
            if(Button(new Rect(820,540,230,48),"Aplicar")){previousMode=Screen.fullScreenMode;previousWidth=Screen.width;previousHeight=Screen.height;var selected=resolutions[resolution];Screen.SetResolution(selected.x,selected.y,Modes[mode]);revertAt=Time.unscaledTime+15;}
        }
        void KeepDisplay(){revertAt=0;var size=resolutions[resolution];PlayerPrefs.SetInt("Mismo.ScreenWidth",size.x);PlayerPrefs.SetInt("Mismo.ScreenHeight",size.y);PlayerPrefs.SetInt("Mismo.ScreenMode",(int)Modes[mode]);PlayerPrefs.Save();}
        void RevertDisplay(){Screen.SetResolution(previousWidth,previousHeight,previousMode);revertAt=0;ReadSettings();}
        void DrawGraphics()
        {
            quality=Choice(270,"Calidad",quality,QualitySettings.names);vsync=Choice(360,"Sincronización vertical",vsync?1:0,new[]{"Desactivada","Activada"})==1;
            bool enabled=GUI.enabled;GUI.enabled=enabled&&!vsync;fps=Choice(450,"Límite de FPS",fps,new[]{"30","60","90","120","144","240","Sin límite"});GUI.enabled=enabled;
            if(Button(new Rect(820,550,230,48),"Aplicar")){QualitySettings.SetQualityLevel(quality,true);QualitySettings.vSyncCount=vsync?1:0;Application.targetFrameRate=FrameCaps[fps];PlayerPrefs.SetInt("Mismo.Quality",quality);PlayerPrefs.SetInt("Mismo.VSync",vsync?1:0);PlayerPrefs.SetInt("Mismo.FPS",FrameCaps[fps]);PlayerPrefs.Save();}
        }
        void DrawControls()
        {
            var languageCodes=L.Languages;var languageNames=new string[languageCodes.Length];
            for(int i=0;i<languageCodes.Length;i++)languageNames[i]=languageCodes[i]=="es"?"Español":"English";
            int languageIndex=Mathf.Max(0,System.Array.IndexOf(languageCodes,L.Code));int selectedLanguage=Choice(230,"Idioma",languageIndex,languageNames);
            if(selectedLanguage!=languageIndex)L.Set(languageCodes[selectedLanguage]);
            float sensitivity=PlayerPrefs.GetFloat("Mismo.LookSensitivity",1);Text(215,295,550,"Sensibilidad del mouse");Text(960,295,110,sensitivity.ToString("0.0")+"×");
            float next=GUI.HorizontalSlider(new Rect(215,345,835,22),sensitivity,.2f,3);if(!Mathf.Approximately(next,sensitivity))PlayerPrefs.SetFloat("Mismo.LookSensitivity",next);
            bool invert=PlayerPrefs.GetInt("Mismo.InvertLookY",0)==1;bool changed=Choice(390,"Invertir cámara vertical",invert?1:0,new[]{"No","Sí"})==1;if(changed!=invert)PlayerPrefs.SetInt("Mismo.InvertLookY",changed?1:0);
            if(Button(new Rect(215,445,260,43),"Modificar UI"))BeginUIEditor();
            if(icons!=null)
            {
                Text(500,445,180,"Opacidad del fondo",18);Text(960,445,110,Mathf.RoundToInt(icons.hudBackgroundOpacity*100)+" %");
                float nextBackground=GUI.HorizontalSlider(new Rect(500,478,550,18),icons.hudBackgroundOpacity,0,1);
                if(!Mathf.Approximately(nextBackground,icons.hudBackgroundOpacity)){icons.hudBackgroundOpacity=nextBackground;PlayerPrefs.SetFloat("Mismo.HUD.BackgroundOpacity",nextBackground);}
            }
            controlScroll=Mismo.Gameplay.Player.Presentation.QuietFantasyUI.BeginScrollView(new Rect(215,520,840,80),controlScroll,new Rect(0,0,805,300));
            string[] help={"Moverse · WASD     Correr · Shift     Saltar · Espacio","Ataque · Clic izquierdo     Habilidades · Q / E / R","Especial · C     Cambiar arma · Tab     Consumibles · 1–4","Menú radial · Mantener B     Inventario · I","Personaje · P     Habilidades · K     Mapa · M","Pausa / volver · Esc"};
            for(int i=0;i<help.Length;i++)Text(0,i*48,795,help[i],18);GUI.EndScrollView();
        }
    }
}
