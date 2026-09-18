using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using Mismo.Gameplay.Player.World;
using Mismo.Gameplay.Player.Presentation;
using L = Mismo.Gameplay.Player.Localization.GameLanguage;

namespace Mismo.Menu
{
    public sealed class MainMenuView : MonoBehaviour
    {
        [SerializeField] string gameplayScene="VoxelRegion_7319";
        [SerializeField] GameObject home, options;
        [SerializeField] Button begin, openOptions, back, quit;
        [SerializeField] Button continueGame;
        [SerializeField] Text status;
        [SerializeField] Slider music, sfx, ui;
        public Slider Music => music;
        public Slider Sfx => sfx;
        public Slider UI => ui;
        public bool OptionsVisible => options.activeSelf;
        bool loading;
        GameObject credits;
        Button openCredits;
        [Header("Créditos de iconos")]
        [TextArea(2,5)] public string flaticonAttribution="Iconos de Flaticon · www.flaticon.com";
        Sprite fantasyFrame;
        readonly System.Collections.Generic.Dictionary<Text,string> originalLabels=new System.Collections.Generic.Dictionary<Text,string>();
        void OnDestroy(){L.Changed-=RefreshLanguage;if(fantasyFrame!=null)Destroy(fantasyFrame);}
        void RefreshLanguage(){foreach(var pair in originalLabels)if(pair.Key!=null)pair.Key.text=L.Text(pair.Value);}
        void OnGUI()
        {
            if(loading)return;
            var old=GUI.matrix;
            float scale=Mathf.Min(Screen.width/1600f,Screen.height/900f);
            GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);
            if(FantasyUI.Button(new Rect(1280,24,270,46),L.LanguageLabel))L.Next();
            GUI.matrix=old;
        }
        void Awake()
        {
            Time.timeScale=1; Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            begin.onClick.AddListener(Begin);
            // Also upgrade saved menu scenes without rebuilding their art or audio settings.
            if(continueGame==null)continueGame=ButtonAt(home.transform,"Continuar",0,true);
            continueGame.onClick.AddListener(Continue);
            begin.GetComponentInChildren<Text>().text="Nueva partida";
            begin.GetComponent<RectTransform>().anchoredPosition=new Vector2(0,-75);
            openOptions.GetComponent<RectTransform>().anchoredPosition=new Vector2(0,-150);
            openCredits=ButtonAt(home.transform,"Créditos",225,false);
            openCredits.onClick.AddListener(ShowCredits);
            quit.GetComponent<RectTransform>().anchoredPosition=new Vector2(0,-300);
            status.rectTransform.anchoredPosition=new Vector2(0,-380);
            CreateCredits();
            continueGame.interactable=WorldSession.CanContinue();
            if(WorldSession.LastError!=null)status.text=WorldSession.LastError;
            openOptions.onClick.AddListener(()=>ShowOptions(true));
            back.onClick.AddListener(()=>ShowOptions(false));
            quit.onClick.AddListener(Application.Quit);
            ShowOptions(false);
        }
        void Start()
        {
            foreach (var control in GetComponentsInChildren<Selectable>(true))
                if (control.GetComponent<UISoundFeedback>() == null) control.gameObject.AddComponent<UISoundFeedback>();
            ApplyFantasyStyle();
            foreach(var text in GetComponentsInChildren<Text>(true))if(text!=status&&!text.text.Contains("%"))originalLabels[text]=text.text;
            L.Changed+=RefreshLanguage;RefreshLanguage();ShowOptions(false);
            if(status!=null)status.text=L.Text(status.text);
        }
        void ApplyFantasyStyle()
        {
            var texture=FantasyUI.FrameTexture;if(texture==null)return;
            fantasyFrame=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(12,12,12,12));
            foreach(var control in GetComponentsInChildren<Button>(true))
            {
                var image=control.targetGraphic as Image;if(image==null)continue;
                image.color=new Color(.10f,.14f,.17f);
                var label=control.GetComponentInChildren<Text>();if(label!=null)label.color=new Color(.94f,.90f,.79f);
                AddFantasyFrame(control.transform,new Color(.72f,.65f,.46f));
            }
            var panel=transform.Find("Menu panel");
            if(panel!=null)
            {
                var edge=panel.Find("Gold edge");if(edge!=null)edge.gameObject.SetActive(false);
                AddFantasyFrame(panel,new Color(.72f,.65f,.46f));
            }
        }
        void AddFantasyFrame(Transform parent,Color tint)
        {
            var go=new GameObject("Fantasy border",typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
            var image=go.GetComponent<Image>();image.sprite=fantasyFrame;image.type=Image.Type.Sliced;image.fillCenter=false;image.color=tint;image.raycastTarget=false;
            var rect=image.rectTransform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
        }
        void Update()
        {
            if(!loading&&credits!=null&&credits.activeSelf&&Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame){ShowOptions(false);return;}
            if(!loading && OptionsVisible && Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame)ShowOptions(false);
        }
        public void ShowOptions(bool show)
        {
            if(loading)return;
            if(credits!=null)credits.SetActive(false);
            if (options.activeSelf != show) GameAudio.Play(show ? GameSound.MenuOpen : GameSound.MenuClose);
            home.SetActive(!show);options.SetActive(show);
            if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(show?music.gameObject:continueGame!=null&&continueGame.interactable?continueGame.gameObject:begin.gameObject);
            if(!show)PlayerPrefs.Save();
        }
        void CreateCredits()
        {
            credits=Box("Créditos",home.transform.parent,new Vector2(42,235),new Vector2(450,420),Color.clear).gameObject;
            Label(credits.transform,"CRÉDITOS",Vector2.zero,new Vector2(450,38),27,Color.white);
            Label(credits.transform,"Lorc y Skoll · Game-icons.net\nCC BY 3.0 · Escala y tinte adaptados\ncreativecommons.org/licenses/by/3.0\n\nCagliostro · Matthew Desmond · SIL OFL 1.1\n\nFantasy UI Borders · Kenney · CC0\n\n"+flaticonAttribution,new Vector2(0,52),new Vector2(450,275),18,new Color(.85f,.87f,.81f));
            var returnButton=ButtonAt(credits.transform,"Volver",340,false);
            returnButton.onClick.AddListener(()=>ShowOptions(false));
            credits.SetActive(false);
        }
        void ShowCredits()
        {
            if(loading)return;
            home.SetActive(false);options.SetActive(false);credits.SetActive(true);GameAudio.Play(GameSound.MenuOpen);
            if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(credits.GetComponentInChildren<Button>().gameObject);
        }
        public void Begin()
        {
            if(loading)return;
            if(!Application.CanStreamedLevelBeLoaded(gameplayScene)){status.text=L.Text("No se encontró la región de juego.");return;}
            if(!WorldSession.NewGame()){status.text=WorldSession.LastError??"No se pudo crear la partida.";return;}
            LoadGame();
        }
        public void Continue()
        {
            if(loading)return;
            if(!Application.CanStreamedLevelBeLoaded(gameplayScene)){status.text=L.Text("No se encontró la región de juego.");return;}
            if(!WorldSession.Continue()){status.text=WorldSession.LastError??"No hay una partida guardada.";return;}
            LoadGame();
        }
        void LoadGame()
        {
            loading=true;begin.interactable=false;openOptions.interactable=false;quit.interactable=false;
            continueGame.interactable=false;
            status.text=L.Text("Preparando tu aventura…");PlayerPrefs.Save();
            SceneManager.LoadSceneAsync(gameplayScene);
        }
        // Built into the scene by the editor tool; all controls remain editable in the hierarchy.
        public void CreateLayout()
        {
            var canvas=gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            var shade=Box("Backdrop",transform,new Vector2(0,0),new Vector2(1600,900),new Color(.035f,.055f,.07f,.42f));
            var rect=shade.rectTransform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var panel=Box("Menu panel",transform,new Vector2(85,115),new Vector2(540,670),new Color(.035f,.055f,.07f,.94f));
            Box("Gold edge",panel.transform,Vector2.zero,new Vector2(4,670),new Color(.86f,.72f,.43f));
            Label(panel.transform,"EXPLORACIÓN · COMBATE · AVENTURA",new Vector2(42,44),new Vector2(450,30),16,new Color(.86f,.72f,.43f));
            Label(panel.transform,"MISMO",new Vector2(38,85),new Vector2(460,90),72,Color.white);
            Label(panel.transform,"Un mundo por descubrir.",new Vector2(42,181),new Vector2(450,35),23,new Color(.7f,.77f,.78f));
            home=Box("Inicio",panel.transform,new Vector2(42,258),new Vector2(450,345),Color.clear).gameObject;
            continueGame=ButtonAt(home.transform,"Continuar",0,true);
            begin=ButtonAt(home.transform,"Nueva partida",75,false);
            openOptions=ButtonAt(home.transform,"Opciones",150,false);
            quit=ButtonAt(home.transform,"Salir",225,false);
            status=Label(home.transform,"VERSIÓN DE PRUEBA",new Vector2(0,279),new Vector2(450,40),15,new Color(.66f,.73f,.73f));
            options=Box("Opciones de sonido",panel.transform,new Vector2(42,245),new Vector2(450,395),Color.clear).gameObject;
            music=SliderAt(options.transform,"Música",0);
            sfx=SliderAt(options.transform,"Efectos",92);
            ui=SliderAt(options.transform,"Interfaz",184);
            back=ButtonAt(options.transform,"Volver",286,false);
            var volume=options.AddComponent<VolumeSettings>();volume.Configure(AudioRuntime.Mixer,music,sfx,ui);
            options.SetActive(false);
            Label(transform,"DOS ARMAS. TU ESTILO.",new Vector2(1040,745),new Vector2(470,45),27,Color.white);
            Label(transform,"Explorá la región y encontrá tu próximo desafío.",new Vector2(900,798),new Vector2(610,35),18,new Color(.8f,.85f,.8f));
        }
        internal static Image Box(string name,Transform parent,Vector2 position,Vector2 size,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
            var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);
            rect.anchoredPosition=new Vector2(position.x,-position.y);rect.sizeDelta=size;
            var image=go.GetComponent<Image>();image.color=color;image.raycastTarget=false;return image;
        }
        internal static Text Label(Transform parent,string text,Vector2 position,Vector2 size,int fontSize,Color color)
        {
            var go=new GameObject(text,typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);
            var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(position.x,-position.y);rect.sizeDelta=size;
            var label=go.GetComponent<Text>();label.text=text;label.font=Mismo.Gameplay.Player.Presentation.QuietFantasyUI.Body;label.fontSize=fontSize;label.color=color;label.raycastTarget=false;
            label.verticalOverflow=VerticalWrapMode.Overflow;return label;
        }
        internal static Button ButtonAt(Transform parent,string title,float y,bool primary)
        {
            var image=Box(title,parent,new Vector2(0,y),new Vector2(450,66),primary?new Color(.83f,.7f,.43f):new Color(.12f,.17f,.19f));image.raycastTarget=true;
            var button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;
            var colors=button.colors;colors.highlightedColor=new Color(1,.91f,.72f);colors.selectedColor=colors.highlightedColor;colors.pressedColor=new Color(.7f,.73f,.7f);button.colors=colors;
            var text=Label(image.transform,title,new Vector2(24,17),new Vector2(400,36),25,primary?new Color(.07f,.09f,.1f):Color.white);
            return button;
        }
        internal static Slider SliderAt(Transform parent,string title,float y)
        {
            Label(parent,title,new Vector2(0,y),new Vector2(310,30),22,Color.white);
            var value=Label(parent,"75 %",new Vector2(355,y),new Vector2(95,30),20,new Color(.86f,.72f,.43f));
            var track=Box(title+" slider",parent,new Vector2(0,y+42),new Vector2(450,22),new Color(.17f,.23f,.25f));track.raycastTarget=true;
            var fill=Box("Fill",track.transform,Vector2.zero,new Vector2(450,22),new Color(.76f,.65f,.41f));
            fill.rectTransform.sizeDelta=Vector2.zero;
            var handle=Box("Handle",track.transform,Vector2.zero,new Vector2(18,32),new Color(1,.91f,.7f));handle.raycastTarget=true;
            handle.rectTransform.pivot=new Vector2(.5f,.5f);handle.rectTransform.sizeDelta=new Vector2(18,10);
            var slider=track.gameObject.AddComponent<Slider>();slider.minValue=0;slider.maxValue=1;slider.value=.75f;slider.fillRect=fill.rectTransform;slider.handleRect=handle.rectTransform;slider.targetGraphic=handle;
            var percent=track.gameObject.AddComponent<VolumePercentLabel>();percent.Configure(slider,value);
            return slider;
        }
    }
}
