using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Selectable))]
public sealed class UISoundFeedback : MonoBehaviour, IPointerEnterHandler, ISelectHandler
{
    Selectable control;
    Button button;
    void Awake() { control = GetComponent<Selectable>(); button = control as Button; }
    void OnEnable() { if (button != null) button.onClick.AddListener(Click); }
    void OnDisable() { if (button != null) button.onClick.RemoveListener(Click); }
    bool Available => control != null && control.IsActive() && control.IsInteractable();
    void Click() { if (Available) GameAudio.Play(GameSound.Click); }
    public void OnPointerEnter(PointerEventData _) { if (Available) GameAudio.Play(GameSound.Hover); }
    public void OnSelect(BaseEventData _) { if (Available) GameAudio.Play(GameSound.Hover); }
}
