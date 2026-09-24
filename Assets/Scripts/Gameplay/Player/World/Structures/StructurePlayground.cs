using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Mismo.Gameplay.Player.Equipment.Inventory;

namespace Mismo.Gameplay.Player.World.Structures
{
    // Used only by the dedicated authoring scenes. Player progress stays entirely in memory.
    public sealed class StructurePlayground : MonoBehaviour
    {
        public PlayerController player;
        public StructureInstance structure;
        public Color exteriorAmbient=new Color(.30f,.35f,.39f);
        NavMeshData navigation;
        NavMeshDataInstance navInstance;
        sealed class Memory:IProfileRepository
        {
            string data;
            public ProfileReadResult Read(Func<string,bool> validate,out string payload){payload=data;return data==null?ProfileReadResult.Missing:validate(data)?ProfileReadResult.Loaded:ProfileReadResult.Invalid;}
            public void Write(string payload)=>data=payload;
        }
        IEnumerator Start()
        {
            if(player==null)yield break;
            var preview=structure.transform.Find("Content preview");if(preview!=null){preview.gameObject.SetActive(false);Destroy(preview.gameObject);}
            var inventory=player.GetComponent<PlayerInventory>()??player.gameObject.AddComponent<PlayerInventory>();
            inventory.Initialize(Mismo.Core.ProjectAssets.Load<ItemCatalog>("ItemCatalog"),new Memory());
            if(player.GetComponent<GatheringPlayer>()==null)player.gameObject.AddComponent<GatheringPlayer>();
            if(player.GetComponent<Presentation.PlayerInteraction>()==null)player.gameObject.AddComponent<Presentation.PlayerInteraction>();
            structure.ConfigureWorld("structure-preview:"+structure.name);
            var settings=Mismo.Core.ProjectAssets.Load<ExplorationWorldSettings>("ExplorationWorldSettings");
            structure.ActivateContent(inventory,settings,"structure-preview",false);
            yield return null;yield return null;Physics.SyncTransforms();
            var sources=new List<NavMeshBuildSource>();
            var ignored=new List<NavMeshBuildMarkup>{new NavMeshBuildMarkup{root=player.transform,ignoreFromBuild=true}};
            NavMeshBuilder.CollectSources(null,~0,NavMeshCollectGeometry.PhysicsColliders,0,ignored,sources);
            navigation=NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0),sources,new Bounds(structure.transform.position+Vector3.up*12,Vector3.one*110),Vector3.zero,Quaternion.identity);
            if(navigation!=null){navInstance=NavMesh.AddNavMeshData(navigation);structure.ActivateContent(inventory,settings,"structure-preview");}
        }
        void OnDestroy(){if(navInstance.valid)navInstance.Remove();if(navigation!=null)Destroy(navigation);}
        void LateUpdate()
        {
            if(player==null)return;var camera=UnityEngine.Camera.main;
            float multiplier=StructureInstance.AmbientAt(camera!=null?camera.transform.position:player.transform.position);
            RenderSettings.ambientSkyColor=exteriorAmbient*multiplier;RenderSettings.ambientEquatorColor=exteriorAmbient*multiplier*.7f;RenderSettings.ambientGroundColor=exteriorAmbient*multiplier*.4f;
            RenderSettings.reflectionIntensity=.3f*multiplier;
            if(player.transform.position.y<-12)player.GetComponent<Movement.PlayerMotor>().ResetPosition(structure.transform.TransformPoint(structure.entrance)+Vector3.up*.3f);
        }
        void OnGUI()
        {
            if(structure==null)return;
            GUI.Box(new Rect(16,16,510,74),structure.title+"\nWASD: caminar · F: abrir, recoger o extraer · Ratón: cámara\nPrueba en memoria: no modifica tu partida.");
            var reward=structure.GetComponentInChildren<StructureReward>(true);
            if(reward!=null&&reward.Consumed)GUI.Box(new Rect(16,98,460,32),"Sala final descubierta");
        }
    }
}
