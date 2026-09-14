using UnityEngine;
namespace Mismo.Gameplay.Player.Equipment.Inventory
{
    public sealed partial class PlayerInventory
    {
        public int SpeciesDefeats(string id)=>profile?.speciesDefeats?.Find(s=>s.id==id)?.quantity??0;
        public bool HasSeenSpecies(string id)=>profile?.seenSpecies?.Contains(id)==true;
        public bool DiscoverSpecies(string id)
        {
            if(!IsReady||!MaterialCatalog.ValidId(id))return false;if(HasSeenSpecies(id))return true;
            var next=profile.Copy();next.seenSpecies.Add(id);return Commit(next,Localization.GameLanguage.Text("Nueva especie descubierta."),false);
        }
        public string CompanionSpeciesId=>profile?.companionSpeciesId;
        public string CompanionPrefabName=>profile?.companionPrefabName;
        public bool CompanionWaiting=>profile?.companionWaiting??false;
        public bool CompanionSummoned=>IsReady&&!profile.companionDismissed&&!string.IsNullOrEmpty(profile.selectedMountId);
        public string SelectedMountId=>profile?.selectedMountId;
        public OwnedMount[] Mounts=>profile?.mounts?.ConvertAll(m=>m.Copy()).ToArray()??System.Array.Empty<OwnedMount>();
        public bool OwnsMount(string species,string prefab)=>profile?.mounts?.Exists(m=>m.speciesId==species&&m.prefabName==prefab)==true;
        public bool CanCallMount=>CanManage&&GetComponent<World.CompanionPlayer>()?.Riding!=true&&GetComponent<World.GatheringPlayer>()?.Busy!=true;
        public bool SummonMount(string id)
        {
            if(!CanCallMount)return false;var next=profile.Copy();var mount=next.mounts.Find(m=>m.id==id);if(mount==null)return false;
            next.selectedMountId=id;next.companionSpeciesId=mount.speciesId;next.companionPrefabName=mount.prefabName;next.companionIndividualId=mount.id;
            next.companionDismissed=false;next.companionWaiting=false;
            if(!Commit(next,Localization.GameLanguage.Text("Montura invocada."),false))return false;
            var companion=GetComponent<World.CompanionPlayer>()??gameObject.AddComponent<World.CompanionPlayer>();companion.Recall();return true;
        }
        public bool DismissMount()
        {
            if(!CanCallMount||!CompanionSummoned)return false;var next=profile.Copy();next.companionDismissed=true;next.companionWaiting=false;
            if(!Commit(next,Localization.GameLanguage.Text("Montura guardada. Podés invocarla nuevamente."),false))return false;
            GetComponent<World.CompanionPlayer>()?.Recall();return true;
        }
        public bool SetCompanionWaiting(bool waiting)
        {
            if(!CanManage||string.IsNullOrEmpty(CompanionSpeciesId))return false;
            var next=profile.Copy();next.companionWaiting=waiting;return Commit(next,Localization.GameLanguage.Text(waiting?"Tu compañero espera.":"Tu compañero te sigue."),false);
        }
    }
}
