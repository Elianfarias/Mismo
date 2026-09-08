using UnityEngine;
using UnityEngine.UI;
namespace Mismo.Menu
{
    public sealed class VolumePercentLabel : MonoBehaviour
    {
        [SerializeField] Slider slider;
        [SerializeField] Text label;
        public void Configure(Slider source,Text target){slider=source;label=target;}
        void Update(){label.text=Mathf.RoundToInt(slider.value*100)+" %";}
    }
}
