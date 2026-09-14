using UnityEngine;
namespace Mismo.Gameplay.Player.World
{
    public sealed class ResourceImpactPalette:MonoBehaviour
    {
        public Color[] colors={new Color(.45f,.45f,.45f)};
        [Range(0,20)]public int fragmentsPerHit=7;
        public Vector2 fragmentSize=new Vector2(.035f,.085f);
        [Min(.05f)]public float fragmentLifetime=.65f;
    }
    public sealed class ResourceChips:MonoBehaviour
    {
        Vector3 velocity;float life;Material material;
        public static void Emit(GameObject source,Vector3 observer,int count=-1)
        {
            var renderer=source.GetComponentInChildren<Renderer>();if(renderer==null)return;
            var palette=source.GetComponentInChildren<ResourceImpactPalette>();
            if(count<0)count=palette!=null?palette.fragmentsPerHit:7;
            var point=renderer.bounds.ClosestPoint(observer+Vector3.up*.8f);
            for(int i=0;i<count;i++)
            {
                var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);cube.name="Resource fragment";var collider=cube.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
                cube.transform.position=point+Random.insideUnitSphere*.12f;cube.transform.localScale=Vector3.one*(palette!=null?Random.Range(Mathf.Max(.005f,palette.fragmentSize.x),Mathf.Max(.005f,palette.fragmentSize.y)):Random.Range(.035f,.085f));
                var color=palette!=null&&palette.colors!=null&&palette.colors.Length>0?palette.colors[Random.Range(0,palette.colors.Length)]:Color.gray;
                var chip=cube.AddComponent<ResourceChips>();chip.material=new Material(Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard"));chip.material.color=color;cube.GetComponent<Renderer>().sharedMaterial=chip.material;
                chip.velocity=(point-observer).normalized*.6f+Random.insideUnitSphere*1.2f+Vector3.up*2;chip.life=palette!=null?Mathf.Max(.05f,palette.fragmentLifetime):.65f;
            }
        }
        void Update(){life-=Time.deltaTime;if(life<=0){Destroy(gameObject);return;}velocity+=Vector3.down*7*Time.deltaTime;transform.position+=velocity*Time.deltaTime;transform.Rotate(new Vector3(160,95,130)*Time.deltaTime);}
        void OnDestroy(){if(material!=null)Destroy(material);}
    }
}
