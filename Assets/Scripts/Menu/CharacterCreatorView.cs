using System;
using System.Collections.Generic;
using Mismo.Gameplay.Player.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Mismo.Menu
{
    public sealed class CharacterCreatorView : MonoBehaviour
    {
        InputField nameInput;
        Text skinLabel, notice;
        Button confirm;
        CanvasGroup controls;
        CharacterCreationStage stage;
        Func<string,string,string> submit;
        Action cancel;
        int selectedIndex;
        string SelectedSkin => PlayerAppearance.Skins[selectedIndex];
        bool submitting, closing;
        readonly List<GameObject> hiddenMenu = new List<GameObject>();
        readonly Color gold = new Color(.88f,.75f,.48f);
        Sprite frameSprite, panelSprite;
        public CharacterCreationStage Stage => stage;
        public bool Ready => stage != null && stage.Ready && !closing && !submitting;

        public static CharacterCreatorView Create(Transform parent, Func<string,string,string> submit, Action cancel)
        {
            var background = MainMenuView.Box("Crear personaje",parent,Vector2.zero,Vector2.zero,Color.clear);
            var rect = background.rectTransform;
            rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            background.raycastTarget=true;
            var view=background.gameObject.AddComponent<CharacterCreatorView>();
            view.submit=submit;view.cancel=cancel;view.Build();return view;
        }
        void Build()
        {
            var frame=FantasyUI.FrameTexture;
            if(frame!=null)frameSprite=Sprite.Create(frame,new Rect(0,0,frame.width,frame.height),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,Vector4.one*FantasyUI.Slice);
            var surface=FantasyUI.PanelTexture;
            panelSprite=Sprite.Create(surface,new Rect(0,0,surface.width,surface.height),new Vector2(.5f,.5f),100);
            // Transparent hit area: the character is part of the real garden, not a render texture.
            var drag=MainMenuView.Box("Girar personaje",transform,Vector2.zero,Vector2.zero,Color.clear);
            drag.rectTransform.anchorMin=new Vector2(.32f,0);drag.rectTransform.anchorMax=Vector2.one;
            drag.rectTransform.offsetMin=drag.rectTransform.offsetMax=Vector2.zero;drag.raycastTarget=true;
            drag.gameObject.AddComponent<CharacterCreatorDragSurface>().Owner=this;
            var content=MainMenuView.Box("Controles",transform,Vector2.zero,Vector2.zero,Color.clear);
            content.rectTransform.anchorMin=Vector2.zero;content.rectTransform.anchorMax=Vector2.one;
            content.rectTransform.offsetMin=content.rectTransform.offsetMax=Vector2.zero;
            controls=content.gameObject.AddComponent<CanvasGroup>();
            MakeButton(content.transform,"Volver",56,42,160,Close);
            var panel=MainMenuView.Box("Tu personaje",content.transform,Vector2.zero,new Vector2(380,478),new Color(.035f,.055f,.058f,.92f));
            panel.rectTransform.anchorMin=panel.rectTransform.anchorMax=Vector2.zero;panel.rectTransform.pivot=Vector2.zero;
            panel.rectTransform.anchoredPosition=new Vector2(56,70);
            var p=panel.transform;
            StyleSurface(panel,.96f);
            Label(p,"TU PERSONAJE",28,26,325,25,16,gold);
            Label(p,"Elegí tu historia",28,63,325,43,31,QuietFantasyUI.Ink);
            Label(p,"NOMBRE",28,131,325,24,16,gold);
            var field=MainMenuView.Box("Nombre",p,new Vector2(28,166),new Vector2(324,53),new Color(.15f,.19f,.19f));field.raycastTarget=true;
            StyleSurface(field,1);
            nameInput=field.gameObject.AddComponent<InputField>();nameInput.targetGraphic=field;
            nameInput.textComponent=Label(field.transform,"",12,10,300,33,23,QuietFantasyUI.Ink);nameInput.textComponent.supportRichText=false;
            nameInput.placeholder=Label(field.transform,"Nombre de tu jugador",12,10,300,33,21,QuietFantasyUI.Muted);
            nameInput.characterLimit=PlayerAppearance.NameLimit;nameInput.lineType=InputField.LineType.SingleLine;
            nameInput.onValidateInput+=(text,index,c)=>char.IsControl(c)||c=='<'||c=='>'?'\0':c;
            nameInput.onValueChanged.AddListener(_=>RefreshValidity());
            Label(p,"APARIENCIA",28,249,324,24,16,gold);
            MakeButton(p,"‹",28,285,50,()=>SelectSkin(-1));
            skinLabel=Label(p,PlayerAppearance.SkinName(SelectedSkin),82,294,216,36,25,QuietFantasyUI.Ink);skinLabel.alignment=TextAnchor.MiddleCenter;
            MakeButton(p,"›",302,285,50,()=>SelectSkin(1));
            notice=Label(p,"",28,349,324,36,16,new Color(1,.81f,.58f));notice.supportRichText=false;
            confirm=MakeButton(p,"Comenzar aventura",28,403,324,Confirm,true);
            var tip=MainMenuView.Box("Ayuda de giro",content.transform,Vector2.zero,new Vector2(650,40),new Color(.03f,.05f,.05f,.65f));
            StyleSurface(tip,.9f);
            tip.rectTransform.anchorMin=tip.rectTransform.anchorMax=new Vector2(.66f,0);tip.rectTransform.pivot=new Vector2(.5f,0);
            tip.rectTransform.anchoredPosition=new Vector2(0,40);
            var hint=Label(tip.transform,"Mantené el clic izquierdo y arrastrá para girar",12,8,626,27,18,new Color(.92f,.9f,.82f));hint.alignment=TextAnchor.MiddleCenter;
        }
        static Text Label(Transform p,string value,float x,float y,float width,float height,int size,Color color)
            =>MainMenuView.Label(p,value,new Vector2(x,y),new Vector2(width,height),size,color);
        void StyleSurface(Image image,float opacity)
        {
            image.sprite=panelSprite;image.type=Image.Type.Simple;image.color=new Color(1,1,1,opacity);
            FantasyPanelClip.Apply(image);
            var border=MainMenuView.Box("Fantasy border",image.transform,Vector2.zero,Vector2.zero,QuietFantasyUI.Ink);
            border.sprite=frameSprite;border.type=Image.Type.Sliced;border.fillCenter=false;
            border.rectTransform.anchorMin=Vector2.zero;border.rectTransform.anchorMax=Vector2.one;
            border.rectTransform.offsetMin=border.rectTransform.offsetMax=Vector2.zero;
        }
        Button MakeButton(Transform p,string title,float x,float y,float width,Action action,bool primary=false)
        {
            var button=MainMenuView.ButtonAt(p,title,y,primary);
            StyleSurface((Image)button.targetGraphic,1);
            var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=PlayerHUD.Gold;
            colors.selectedColor=PlayerHUD.Gold;colors.pressedColor=QuietFantasyUI.Amber;
            colors.disabledColor=new Color(.65f,.65f,.65f,1);button.colors=colors;
            var rect=(RectTransform)button.transform;rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(width,52);
            var label=button.GetComponentInChildren<Text>();label.rectTransform.anchoredPosition=new Vector2(8,-8);
            label.rectTransform.sizeDelta=new Vector2(width-16,36);label.fontSize=22;label.alignment=TextAnchor.MiddleCenter;label.color=QuietFantasyUI.Ink;
            button.onClick.AddListener(()=>{if(!submitting&&!closing)action();});button.gameObject.AddComponent<UISoundFeedback>();return button;
        }
        public void Open()
        {
            if(stage!=null)return;
            gameObject.SetActive(true);transform.SetAsLastSibling();submitting=closing=false;
            try
            {
                stage=new CharacterCreationStage(gameObject.scene);stage.Show(SelectedSkin);stage.Enter();
                foreach(Transform sibling in transform.parent)
                    if(sibling!=transform&&sibling.gameObject.activeSelf){hiddenMenu.Add(sibling.gameObject);sibling.gameObject.SetActive(false);}
                controls.alpha=0;controls.interactable=false;
                if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(null);
                RefreshValidity();
            }
            catch(Exception e)
            {
                stage?.Dispose();stage=null;controls.alpha=1;controls.interactable=true;notice.text=e.Message;confirm.interactable=false;
            }
        }
        void SelectSkin(int direction)
        {
            if(!Ready)return;
            int next=(selectedIndex+direction+PlayerAppearance.Skins.Count)%PlayerAppearance.Skins.Count;
            try
            {
                stage.Show(PlayerAppearance.Skins[next]);selectedIndex=next;
                skinLabel.text=PlayerAppearance.SkinName(SelectedSkin);RefreshValidity();
            }
            catch(Exception e){notice.text=e.Message;confirm.interactable=false;}
        }
        void RefreshValidity()
        {
            if(confirm==null)return;
            bool valid=PlayerAppearance.ValidName(nameInput.text);
            confirm.interactable=valid&&Ready;
            notice.text=valid?"Tu apariencia no cambia tus habilidades.":"Escribí un nombre (hasta 24 caracteres).";
        }
        public void Drag(float horizontalPixels){if(Ready)stage.Rotate(horizontalPixels);}
        void Confirm()
        {
            if(!confirm.interactable||!Ready)return;
            submitting=true;confirm.interactable=false;
            var error=submit(nameInput.text.Trim(),SelectedSkin);
            if(error!=null){submitting=false;RefreshValidity();notice.text=error;return;}
            notice.text="Preparando tu aventura…";
        }
        void Close()
        {
            if(closing||submitting)return;
            if(stage==null){FinishClose();return;}
            closing=true;controls.interactable=false;stage.Return();
        }
        void FinishClose()
        {
            stage?.Dispose();stage=null;
            foreach(var item in hiddenMenu)if(item!=null)item.SetActive(true);hiddenMenu.Clear();
            gameObject.SetActive(false);closing=false;cancel();
        }
        // Also used by the editor's isolated preview check to sample the cinematic deterministically.
        public void Advance(float seconds)
        {
            if(stage==null||submitting)return;
            bool wasMoving=stage.Moving;stage.Advance(seconds);
            controls.alpha=closing?1-Mathf.Clamp01(stage.Progress*2):Mathf.SmoothStep(0,1,Mathf.InverseLerp(.55f,1,stage.Progress));
            if(closing&&!stage.Moving){FinishClose();return;}
            if(wasMoving&&!stage.Moving)
            {
                controls.interactable=true;RefreshValidity();
                nameInput.Select();nameInput.ActivateInputField();
            }
        }
        void Update()
        {
            Advance(Time.unscaledDeltaTime);
            if(!submitting&&Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame)Close();
        }
        void OnDestroy(){stage?.Dispose();stage=null;if(frameSprite!=null)Destroy(frameSprite);if(panelSprite!=null)Destroy(panelSprite);}
    }

    public sealed class CharacterCreatorDragSurface : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public CharacterCreatorView Owner;
        bool dragging;
        public void OnPointerDown(PointerEventData e){dragging=e.button==PointerEventData.InputButton.Left&&Owner!=null&&Owner.Ready;}
        public void OnPointerUp(PointerEventData e){if(e.button==PointerEventData.InputButton.Left)dragging=false;}
        public void OnInitializePotentialDrag(PointerEventData e){e.useDragThreshold=false;}
        public void OnBeginDrag(PointerEventData e){dragging=e.button==PointerEventData.InputButton.Left&&Owner!=null&&Owner.Ready;}
        public void OnDrag(PointerEventData e){if(dragging&&e.button==PointerEventData.InputButton.Left)Owner.Drag(e.delta.x);}
        public void OnEndDrag(PointerEventData e){if(e.button==PointerEventData.InputButton.Left)dragging=false;}
        void OnDisable()=>dragging=false;
        void OnApplicationFocus(bool focused){if(!focused)dragging=false;}
    }
}
