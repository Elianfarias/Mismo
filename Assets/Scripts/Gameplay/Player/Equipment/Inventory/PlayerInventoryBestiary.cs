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
        public bool SetCompanionWaiting(bool waiting)
        {
            if(!CanManage||string.IsNullOrEmpty(CompanionSpeciesId))return false;
            var next=profile.Copy();next.companionWaiting=waiting;return Commit(next,Localization.GameLanguage.Text(waiting?"Tu compañero espera.":"Tu compañero te sigue."),false);
        }
    }
}
