using Mismo.Gameplay.Player.Equipment;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Mismo.Gameplay.Player.Presentation
{
    /// <summary>Only renderers: the ghost cannot arm, hit, block a ray, or alter the source prefab.</summary>
    public sealed class GroundThrowIndicator
    {
        readonly GameObject owner;
        GameObject root,ghost;
        GameObject shownPrefab;
        LineRenderer arc,ring,crossA,crossB;
        LineRenderer[] lines;
        readonly Vector3[] arcPoints=new Vector3[GroundThrowTrajectory.Segments+1],ringPoints=new Vector3[49];
        readonly MaterialPropertyBlock tint=new MaterialPropertyBlock();
        static readonly Color Valid=new Color(1,.82f,.48f,.8f),Invalid=new Color(1,.27f,.22f,.85f);
        public GroundThrowIndicator(GameObject owner){this.owner=owner;}
        public void Show(GroundThrowPath path,Material material,TrapAction action,float radius)
        {
            if(root==null)
            {
                root=new GameObject("Ground throw preview"){hideFlags=HideFlags.DontSave};
                SceneManager.MoveGameObjectToScene(root,owner.scene);
                arc=Line("Trajectory",.025f);ring=Line("Landing footprint",.025f);
                crossA=Line("Invalid cross A",.035f);crossB=Line("Invalid cross B",.035f);
                lines=new[]{arc,ring,crossA,crossB};
            }
            if(root.scene!=owner.scene)SceneManager.MoveGameObjectToScene(root,owner.scene);
            root.SetActive(true);
            var color=path.valid?Valid:Invalid;
            foreach(var line in lines)
            {line.sharedMaterial=material;line.startColor=line.endColor=color;}
            arc.enabled=action!=null;
            if(arc.enabled)
            {
                for(int i=0;i<arcPoints.Length;i++)arcPoints[i]=path.Sample(i/(float)GroundThrowTrajectory.Segments);
                arc.positionCount=arcPoints.Length;arc.SetPositions(arcPoints);
            }
            var rotation=path.LandingRotation(path.end-path.start);
            for(int i=0;i<ringPoints.Length;i++)
            {
                float angle=i*Mathf.PI*2/(ringPoints.Length-1);
                var offset=new Vector3(Mathf.Sin(angle)*radius,0,Mathf.Cos(angle)*radius);
                // Area damage uses a horizontal radius; do not shrink it when the center is on a slope.
                var point=path.end+(action!=null?rotation*offset:offset);
                if(GroundThrowTrajectory.Ground(owner,point,Mathf.Max(.4f,radius),Mathf.Max(.6f,radius*2),out var ground)&&ground.normal.y>=.65f)point=ground.point;
                ringPoints[i]=point+path.normal*.04f;
            }
            ring.positionCount=ringPoints.Length;ring.SetPositions(ringPoints);
            crossA.enabled=crossB.enabled=!path.valid;
            var center=path.end+path.normal*.055f;
            Cross(crossA,center,rotation*new Vector3(.28f,0,.28f));Cross(crossB,center,rotation*new Vector3(.28f,0,-.28f));
            if(action!=null)
            {
                if(shownPrefab!=action.prefab||ghost==null)MakeGhost(action,material);
                ghost.SetActive(path.valid);ghost.transform.SetPositionAndRotation(path.end+path.normal*.025f,rotation);
            }
            else if(ghost!=null)ghost.SetActive(false);
        }
        void MakeGhost(TrapAction action,Material material)
        {
            if(ghost!=null)Object.Destroy(ghost);
            shownPrefab=action.prefab;ghost=new GameObject("Trap silhouette");ghost.transform.SetParent(root.transform,false);
            tint.SetColor("_Color",new Color(1,.86f,.6f,.16f));
            foreach(var source in action.prefab.GetComponentsInChildren<MeshFilter>())
            {
                var piece=new GameObject(source.name);piece.layer=owner.layer;piece.transform.SetParent(ghost.transform,false);
                var matrix=action.prefab.transform.worldToLocalMatrix*source.transform.localToWorldMatrix;
                piece.transform.localPosition=matrix.GetColumn(3);piece.transform.localRotation=matrix.rotation;piece.transform.localScale=matrix.lossyScale;
                piece.AddComponent<MeshFilter>().sharedMesh=source.sharedMesh;
                var renderer=piece.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.SetPropertyBlock(tint);
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;
            }
        }
        static void Cross(LineRenderer line,Vector3 center,Vector3 offset)
        {line.positionCount=2;line.SetPosition(0,center-offset);line.SetPosition(1,center+offset);}
        LineRenderer Line(string name,float width)
        {
            var go=new GameObject(name);go.layer=owner.layer;go.transform.SetParent(root.transform,false);
            var line=go.AddComponent<LineRenderer>();line.useWorldSpace=true;line.widthMultiplier=width;
            line.numCornerVertices=2;line.numCapVertices=2;line.shadowCastingMode=ShadowCastingMode.Off;
            line.receiveShadows=false;line.lightProbeUsage=LightProbeUsage.Off;return line;
        }
        public void Hide(){if(root!=null)root.SetActive(false);}
        public void Dispose(){if(root!=null){root.SetActive(false);Object.Destroy(root);}root=null;ghost=null;}
    }
}
