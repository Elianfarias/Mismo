using System;
using System.IO;
using System.Linq;
using Mismo.Core;
using Mismo.Gameplay.Player.World;
using UnityEngine;
namespace Mismo.Gameplay.Player.Quests
{
    /// <summary>Opt-in executable check. Never runs in normal gameplay or changes a player save.</summary>
    public static class QuestBuildCheck
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Check()
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-mismo-quest-check");if(index<0||index+1>=args.Length)return;
            string output=Path.GetFullPath(args[index+1]);
            try
            {
                var catalog=QuestCatalog.Load();
                if(catalog==null||catalog.quests.Length!=7||catalog.radialSelected.Length!=8||catalog.radialSelected.Any(t=>t==null)||catalog.radialSurface==null||catalog.radialOutline==null||catalog.journalIcon==null)throw new Exception("Quest catalog or radial dependencies missing from build");
                if(catalog.journalKey!=UnityEngine.InputSystem.Key.J||catalog.interactKey!=UnityEngine.InputSystem.Key.T)throw new Exception("Unexpected default quest controls");
                if(catalog.journalContacts||catalog.villageNpcs==null||catalog.villageNpcs.residents.Length!=5)throw new Exception("Physical NPC contacts not configured");
                foreach(var resident in catalog.villageNpcs.residents)
                {
                    if(resident.prefab==null||resident.prefab.GetComponent<QuestGiver>()?.npc==null)throw new Exception("NPC prefab or dialogue absent from build");
                    var skin=resident.prefab.GetComponentInChildren<SkinnedMeshRenderer>();
                    if(skin==null||skin.sharedMesh==null||skin.bones.Length<10||skin.sharedMaterials.Any(m=>m==null||m.shader==null))throw new Exception("Voxel NPC rig/material dependencies absent from build");
                }
                var recipes=ProjectAssets.LoadAll<CraftingRecipe>("Recipes");
                foreach(var q in catalog.quests)
                {
                    if(!q.Validate(out var error))throw new Exception(error);
                    if(string.IsNullOrEmpty(q.offerText)||string.IsNullOrEmpty(q.completedText)||string.IsNullOrEmpty(q.npc.greeting))throw new Exception("NPC text missing: "+q.id);
                    foreach(var o in q.objectives)if(o.kind==QuestObjectiveKind.Material&&o.material.icon==null)throw new Exception("Material icon missing: "+q.id);
                    foreach(var r in q.recipes)if(!recipes.Contains(r))throw new Exception("Reward recipe absent from book: "+r.id);
                }
                File.WriteAllText(output,"PASS: 7 quests, 5 voxelized NPC prefabs with rigs/materials, physical quest contacts, material icons, 8 radial sectors, learned recipes and J/T controls load in Windows player.");Debug.Log("QUEST_PLAYER_OK");Application.Quit(0);
            }
            catch(Exception e){File.WriteAllText(output,"FAIL: "+e);Debug.LogException(e);Application.Quit(1);}
        }
    }
}
