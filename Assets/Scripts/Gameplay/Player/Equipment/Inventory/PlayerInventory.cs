using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Mismo.Gameplay.Player.Equipment.Inventory
{
    [DisallowMultipleComponent]
    public sealed partial class PlayerInventory : MonoBehaviour
    {
        internal static IProfileRepository BuildCheckRepository;
        InventoryProfile profile;
        ItemCatalog catalog;
        IProfileRepository repository;
        EquipmentLoadout loadout;
        readonly HashSet<string> definitions = new HashSet<string>(StringComparer.Ordinal);
        readonly Dictionary<string, string> rewards = new Dictionary<string, string>(StringComparer.Ordinal);
        readonly Dictionary<string, MaterialDefinition> materialDefinitions = new Dictionary<string, MaterialDefinition>(StringComparer.Ordinal);
        public IEnumerable<MaterialDefinition> Materials => materialDefinitions.Values;
        bool writable;
        float baseMaximum;
        public ProgressionRules Rules => ProgressionRules.Current;
        public ProgressionData Progression => profile?.progression.Copy();
        public int Level => profile?.progression.level ?? 1;
        public float Armor {get;private set;}
        public float IncomingDamageMultiplier => Armor>=0 ? Rules.armorScale/(Mathf.Max(.01f,Rules.armorScale)+Armor)
            : 1+(-Armor)/Mathf.Max(.01f,Rules.armorScale);
        public bool IsReady => profile != null;
        public int Count => profile != null ? profile.weapons.Count : 0;
        public string Notice { get; private set; }
        public bool HasSaveProblem { get; private set; }
        public event Action Changed;
        public string SavePath => World.WorldSession.ProfilePath;
        public WeaponDefinition BossReward => catalog != null ? catalog.bossReward : null;
        public int MaterialCount(string id) => profile?.MaterialCount(id) ?? 0;

        public bool TryGrantMaterial(string id, int quantity)
        {
            if (!IsReady || !materialDefinitions.ContainsKey(id)) return false;
            var next = profile.Copy();
            return next.TryAddMaterial(id, quantity) && Fits(next, false) && Commit(next, "+" + quantity + " " + materialDefinitions[id].displayName, false, GameSound.ResourceCollected);
        }

        public void Initialize(ItemCatalog items, IProfileRepository storage = null)
        {
            if (IsReady) return;
            loadout = GetComponent<EquipmentLoadout>();
            loadout.Initialize();
            baseMaximum=GetComponent<Mismo.Gameplay.Combat.Health>()?.Maximum ?? 100;
            catalog = items;
            if (catalog == null || catalog.weapons == null || loadout.GetSlot(0) == null || loadout.GetSlot(1) == null)
            { Notice = "No se pudo cargar el catálogo de armas."; HasSaveProblem = true; return; }
            foreach (var definition in catalog.weapons)
                if (definition == null || string.IsNullOrEmpty(definition.Id) || !definitions.Add(definition.Id))
                { Notice = "El catálogo de armas contiene datos inválidos."; HasSaveProblem = true; return; }
            if (catalog.bossReward != null) rewards.Add(ItemCatalog.BossRewardId, catalog.bossReward.Id);
            foreach (var material in Resources.LoadAll<MaterialDefinition>("Materials"))
            {
                if (!MaterialCatalog.ValidId(material.id) || string.IsNullOrWhiteSpace(material.displayName) || materialDefinitions.ContainsKey(material.id))
                { Notice = "El catálogo de materiales contiene datos inválidos o IDs duplicados."; HasSaveProblem = true; return; }
                materialDefinitions.Add(material.id, material);
            }
            repository = storage ?? BuildCheckRepository ?? new ProtectedProfileRepository(SavePath);
            try
            {
                var result = repository.Read(ValidatePayload, out var payload);
                writable = result != ProfileReadResult.Invalid;
                profile = payload != null ? JsonUtility.FromJson<InventoryProfile>(payload) : CreateStartingProfile();
                if (!profile.IsValid(definitions, rewards)) throw new InvalidDataException("Invalid starting profile.");
                bool migrated=profile.version<5;
                profile.UpgradeToCurrent();profile.UpgradeMountCollection();NormalizeGrid(profile);
                if (result == ProfileReadResult.Invalid)
                { Notice = "No se pudo recuperar el guardado. Tus archivos se conservaron; no se guardarán cambios."; HasSaveProblem = true; }
                else if (result == ProfileReadResult.Recovered)
                { Notice = "Se recuperó la copia de respaldo del inventario."; }
                else Notice = "Inventario guardado automáticamente.";
                if (result == ProfileReadResult.Missing || migrated && writable) repository.Write(JsonUtility.ToJson(profile));
                ApplyEquipment();
                ApplyStats();
                var startingHealth=GetComponent<Mismo.Gameplay.Combat.Health>();
                if(startingHealth!=null && !startingHealth.IsDead)startingHealth.Revive();
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is System.Security.Cryptography.CryptographicException)
            {
                writable = false;
                if(profile==null||!profile.IsValid(definitions,rewards))profile=CreateStartingProfile();
                profile.UpgradeToCurrent();profile.UpgradeMountCollection();NormalizeGrid(profile);
                ApplyEquipment();ApplyStats();
                Notice = "No se pudo acceder al guardado. Tus archivos se conservaron; no se guardarán cambios.";
                HasSaveProblem = true;
                Debug.LogWarning("Inventory initialization: " + e.Message, this);
            }
            if(IsReady && GetComponent<InventoryWorldAccess>()==null)gameObject.AddComponent<InventoryWorldAccess>();
            worldClock=profile.worldPlaySeconds;
            if(IsReady&&GetComponent<World.RegionRespawn>()!=null&&GetComponent<World.GatheringPlayer>()==null)gameObject.AddComponent<World.GatheringPlayer>();
            if(IsReady&&GetComponent<World.RegionRespawn>()!=null&&GetComponent<World.CompanionPlayer>()==null)gameObject.AddComponent<World.CompanionPlayer>();
            Changed?.Invoke();
        }

        InventoryProfile CreateStartingProfile()
        {
            var result = new InventoryProfile();
            for (int slot = 0; slot < 2; slot++)
            {
                var item = new OwnedWeapon { instanceId = Guid.NewGuid().ToString("N"), definitionId = loadout.GetSlot(slot).Id };
                result.weapons.Add(item); result.equipped[slot] = item.instanceId;
            }
            return result;
        }

        bool ValidatePayload(string json)
        {
            try
            {
                var loaded=JsonUtility.FromJson<InventoryProfile>(json);
                if(loaded?.IsValid(definitions,rewards)!=true)return false;
                for(int i=0;i<2;i++)
                {
                    var main=catalog.Find(loaded.Find(loaded.equipped[i]).definitionId);
                    if(main.isShield)return false;
                    var off=loaded.Find(loaded.Offhand(i));
                    if(off!=null&&!Compatible(main,catalog.Find(off.definitionId)))return false;
                }
                if(loaded.version>=2)foreach(var mastery in loaded.progression.masteries)
                {
                    if(!HasFamily(mastery.familyId))return false;
                    if(mastery.equippedAbilities==null||mastery.equippedAbilities.Length==0)continue;
                    var family=FindFamily(mastery.familyId);
                    if(family==null)return false;
                    foreach(var id in mastery.equippedAbilities)
                    {
                        int skill=family.FindSkill(id);
                        if(skill<0||mastery.level<family.UnlockLevel(skill))return false;
                    }
                }
                return true;
            }
            catch (ArgumentException) { return false; }
        }

        public string ItemId(int index) => profile.weapons[index].instanceId;
        public string EquippedId(int slot) => profile.equipped[slot];
        public WeaponDefinition Definition(string instanceId)
        {
            var item = profile?.Find(instanceId);
            return item != null ? catalog.Find(item.definitionId) : null;
        }
        public bool HasClaimed(string rewardId) => profile != null && (profile.claimedRewards.Contains(rewardId)||profile.pendingLoot?.Exists(l=>l.rewardId==rewardId)==true);
        public bool IsWorldEnemyDefeated(string id)=>profile?.defeatedEnemies?.Contains(id)==true;
        public int RegionMinimum(string id)=>profile?.regions?.Find(r=>r.id==id)?.minimumLevel??0;
        public int RegionLevel(string id)=>Mathf.Max(Level,RegionMinimum(id));
        public bool TryDiscoverRegion(string id,int increment)
        {
            if(!IsReady||string.IsNullOrEmpty(id))return false;
            if(RegionMinimum(id)>0)return true;
            var next=profile.Copy();next.EnsureWorldData();
            next.regions.Add(new DiscoveredRegion{id=id,minimumLevel=Mathf.Clamp(Level+Mathf.Clamp(increment,0,100),1,1100)});
            return Commit(next,"Nueva región · Nivel mínimo "+next.regions[next.regions.Count-1].minimumLevel,false,GameSound.RegionDiscovered);
        }
        public OwnedWeapon Item(string instanceId) => profile?.Find(instanceId)?.Copy();
        public string ItemName(string id)
        {
            var item=profile?.Find(id);var definition=Definition(id);
            return definition==null?"":definition.DisplayName+" · T"+item.tier+" · "+VariantName(item.variant);
        }
        public static string VariantName(WeaponVariant variant) => variant==WeaponVariant.Colossus?"Coloso":
            variant==WeaponVariant.Duelist?"Duelista":variant==WeaponVariant.Guardian?"Guardián":"Equilibrada";
        public MasteryProgress Mastery(WeaponDefinition weapon) => weapon==null?null:
            profile?.progression.Find(weapon.MasteryId)?.Copy() ?? new MasteryProgress{familyId=weapon.MasteryId};
        OwnedWeapon EquippedItem(WeaponDefinition weapon)
        {
            if(profile==null||weapon==null)return null;
            if(weapon==loadout.GetSlot(0))return profile.Find(profile.equipped[0]);
            if(weapon==loadout.GetSlot(1))return profile.Find(profile.equipped[1]);
            var active=profile.Find(profile.equipped[profile.activeSlot]);
            if(active.definitionId==weapon.Id)return active;
            var secondary=profile.Find(profile.equipped[1-profile.activeSlot]);
            return secondary.definitionId==weapon.Id?secondary:null;
        }
        public float DamageMultiplier(WeaponDefinition weapon)
        {
            if(!IsReady)return 1;
            var mastery=Mastery(weapon);
            return Mathf.Max(.1f,1+profile.progression.attackPoints*Rules.attackPerPoint+
                HandDamageBonus(weapon)+(mastery?.damagePoints??0)*Rules.masteryDamagePerPoint+WeaponConsumableBonus(weapon))*(weapon!=null&&weapon.dualSwordFamily!=null&&!weapon.dualWield?1.15f:weapon?.styleDamageMultiplier??1);
        }
        public float AttackSpeed(WeaponDefinition weapon)
        {
            if(!IsReady)return 1;
            return Mathf.Clamp(1+HandSpeedBonus(weapon)+
                (Mastery(weapon)?.speedPoints??0)*Rules.masterySpeedPerPoint+(weapon?.styleSpeedBonus??0)+(GetComponent<WeaponSkillEffects>()?.SpeedBonus??0),.5f,Rules.maximumAttackSpeed);
        }
        void ApplyStats()
        {
            if(profile==null)return;
            var first=Rules.Bonuses(profile.Find(profile.equipped[0]));
            var second=Rules.Bonuses(profile.Find(profile.equipped[1]));
            Armor=profile.progression.armorPoints*Rules.armorPerPoint+first.armor+second.armor;
            float offhandLife=0;
            var offhandItem=profile.Find(profile.Offhand(profile.activeSlot));
            if(offhandItem!=null)
            {
                var bonus=Rules.Bonuses(offhandItem);Armor+=bonus.armor;offhandLife+=bonus.life;
                if(catalog.Find(offhandItem.definitionId)?.isShield==true)Armor+=12+2*(offhandItem.tier-1);
            }
            var health=GetComponent<Mismo.Gameplay.Combat.Health>();
            float max=Mathf.Max(1,baseMaximum+profile.progression.lifePoints*Rules.lifePerPoint+first.life+second.life+offhandLife);
            if(health!=null&&!Mathf.Approximately(max,health.Maximum))health.ConfigureMaximum(max);
        }
        public bool TrySpend(CharacterAttribute attribute)
        {
            if(!IsReady||!loadout.CanChangeEquipment||profile.progression.Available<=0)return false;
            var next=profile.Copy();
            switch(attribute)
            {
                case CharacterAttribute.Life:next.progression.lifePoints++;break;
                case CharacterAttribute.Attack:next.progression.attackPoints++;break;
                case CharacterAttribute.Armor:next.progression.armorPoints++;break;
                default:return false;
            }
            return Commit(next,"Atributo guardado.",false);
        }
        public bool TrySpendMastery(WeaponDefinition weapon,MasteryAttribute attribute)
        {
            if(!IsReady||weapon==null||!loadout.CanChangeEquipment)return false;
            var next=profile.Copy();var mastery=next.progression.Find(weapon.MasteryId);
            if(mastery==null||mastery.Available<=0)return false;
            if(attribute==MasteryAttribute.Damage)mastery.damagePoints++;
            else if(attribute==MasteryAttribute.Speed)mastery.speedPoints++;else return false;
            return Commit(next,"Maestría guardada.",false);
        }
        // A kill's experience and rolled item are committed together. Failed writes apply neither.
        public bool TryGrantVictory(int experience,IDictionary<string,int> mastery,OwnedWeapon drop,string worldEnemyId=null, IDictionary<string,int> materialLoot=null, Vector3? lootPosition=null,string speciesId=null,bool recognized=false,string creaturePrefab=null)
        {
            if(!IsReady||experience<0||experience>1000000)return false;
            if(worldEnemyId!=null&&IsWorldEnemyDefeated(worldEnemyId))return true;
            if(mastery!=null)foreach(var pair in mastery)
                if(pair.Value<0||pair.Value>1000000||!HasFamily(pair.Key))return false;
            if(drop!=null && (drop.definitionId==null||!definitions.Contains(drop.definitionId)))return false;
            var next=profile.Copy();int previous=next.progression.level;
            if(!string.IsNullOrEmpty(speciesId))
            {
                if(!MaterialCatalog.ValidId(speciesId))return false;
                var species=next.speciesDefeats.Find(s=>s.id==speciesId);
                if(species==null)next.speciesDefeats.Add(new MaterialStack{id=speciesId,quantity=1});
                else if(species.quantity<int.MaxValue)species.quantity++;
            }
            if(worldEnemyId!=null){next.EnsureWorldData();next.defeatedEnemies.Add(worldEnemyId);}
            bool newMount=false;
            if(recognized)
            {
                var species=World.CreatureSpecies.Find(speciesId);
                if(species!=null&&species.domesticable&&species.mountable&&species.prefabs!=null&&Array.Exists(species.prefabs,p=>p!=null&&p.name==creaturePrefab))
                {
                    next.UpgradeMountCollection();
                    if(next.mounts.Count<512&&!next.mounts.Exists(m=>m.speciesId==speciesId&&m.prefabName==creaturePrefab))
                    {
                        var owned=new OwnedMount{id=string.IsNullOrEmpty(worldEnemyId)?Guid.NewGuid().ToString("N"):worldEnemyId,speciesId=speciesId,prefabName=creaturePrefab};next.mounts.Add(owned);newMount=true;
                        if(string.IsNullOrEmpty(next.selectedMountId)){next.selectedMountId=owned.id;next.companionSpeciesId=speciesId;next.companionPrefabName=creaturePrefab;next.companionIndividualId=owned.id;next.companionWaiting=false;next.companionDismissed=false;}
                    }
                }
            }
            var pending=new PendingInventoryLoot{id=Guid.NewGuid().ToString("N")};
            var position=lootPosition??transform.position;pending.x=position.x;pending.y=position.y;pending.z=position.z;
            if(materialLoot!=null)foreach(var entry in materialLoot)
            {
                if(!materialDefinitions.ContainsKey(entry.Key)||entry.Value<=0)return false;
                var candidate=next.Copy();
                if(candidate.TryAddMaterial(entry.Key,entry.Value)&&HasGridRoom(candidate,false))next=candidate;
                else pending.materials.Add(new MaterialStack{id=entry.Key,quantity=entry.Value});
            }
            Rules.Grant(next.progression,experience,mastery);
            bool added=false;
            if(drop!=null)
            {
                var candidate=next.Copy();candidate.weapons.Add(drop.Copy());
                added=next.weapons.Count<256&&HasGridRoom(candidate,false);
                if(added)next=candidate;else pending.weapon=drop.Copy();
            }
            bool waiting=pending.weapon!=null||pending.materials.Count>0;
            if(waiting)next.pendingLoot.Add(pending);
            string notice="+"+experience+" EXP"+(next.progression.level>previous?" · Nivel "+next.progression.level:"")+
                (added?" · Arma T"+drop.tier+" obtenida":"")+(waiting?" · Mochila llena: botín guardado en el suelo.":"");
            if(newMount)notice+=" · "+Localization.GameLanguage.Text("La criatura te reconoce como su amo.");
            if (!Commit(next,notice,false)) return false;
            if (next.progression.level > previous) GameAudio.Play(GameSound.LevelUp);
            else if (experience > 0) GameAudio.Play(GameSound.ExperienceGained);
            if (drop != null) GameAudio.Play(GameSound.WeaponDrop);
            return true;
        }
        bool HasFamily(string id)
        {if(FindFamily(id)!=null)return true;foreach(var weapon in catalog.weapons)if(weapon.MasteryId==id)return true;return false;}
        public OwnedWeapon RollDrop()
        {
            if(!IsReady||catalog.weapons.Length==0)return null;
            // Unique boss rewards stay exclusive to their existing pickup.
            var eligible=System.Array.FindAll(catalog.weapons,w=>w!=catalog.bossReward);
            if(eligible.Length==0)return null;
            return new OwnedWeapon{instanceId=Guid.NewGuid().ToString("N"),definitionId=eligible[UnityEngine.Random.Range(0,eligible.Length)].Id,
                tier=Rules.RollTier(),variant=(WeaponVariant)UnityEngine.Random.Range(0,4)};
        }

        public bool TryEquip(int slot, string instanceId)
        {
            if (!IsReady || !loadout.CanChangeEquipment || Definition(instanceId)==null || Definition(instanceId).isShield) return false;
            var next = profile.Copy();
            if(!next.TryEquip(slot,instanceId))return false;
            NormalizeHands(next);return Commit(next,"Equipamiento guardado.",true,GameSound.ItemEquipped);
        }

        public bool TryEquipDefinition(int slot, WeaponDefinition definition)
        {
            if (!IsReady || definition == null) return false;
            var item = profile.weapons.Find(value => value.definitionId == definition.Id && !value.inChest);
            return item != null && TryEquip(slot, item.instanceId);
        }

        public bool TrySwap()
        {
            if (!IsReady || !loadout.CanSwap) return false;
            var next = profile.Copy(); next.activeSlot = 1 - next.activeSlot;
            return Commit(next, "Equipamiento guardado.",true,GameSound.ItemEquipped);
        }

        public bool TryClaimReward(string rewardId)
        {
            if (!IsReady || HasClaimed(rewardId) || !rewards.TryGetValue(rewardId, out var definition)) return false;
            var health = GetComponent<Mismo.Gameplay.Combat.Health>();
            if (health == null || health.IsDead) return false;
            var next = profile.Copy();
            if(!next.TryClaim(rewardId,definition))return false;
            var reward=next.weapons[next.weapons.Count-1];reward.tier=2;reward.variant=WeaponVariant.Guardian;
            if(!HasGridRoom(next,false))
            {
                next.weapons.Remove(reward);next.claimedRewards.Remove(rewardId);
                next.pendingLoot.Add(new PendingInventoryLoot{id=Guid.NewGuid().ToString("N"),rewardId=rewardId,weapon=reward,
                    x=transform.position.x,y=transform.position.y,z=transform.position.z});
                return Commit(next,"Mochila llena: la recompensa única quedó guardada como botín en el suelo.",false,GameSound.WeaponDrop);
            }
            return Commit(next, "Obtuviste " + catalog.Find(definition).DisplayName + " T2 Guardián. [I] Inventario", false,GameSound.WeaponDrop);
        }

        bool Commit(InventoryProfile next, string notice, bool updateEquipment = true, GameSound? sound = null)
        {
            next.worldPlaySeconds=System.Math.Max(next.worldPlaySeconds,worldClock);
            NormalizeGrid(next);
            if (!writable || !next.IsValid(definitions, rewards)) return false;
            try { repository.Write(JsonUtility.ToJson(next)); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is System.Security.Cryptography.CryptographicException)
            {
                Notice = "No se pudo guardar el cambio. Volvé a intentarlo; tu equipo anterior sigue intacto.";
                HasSaveProblem = true; Changed?.Invoke();
                Debug.LogWarning("Inventory save: " + e.Message, this);
                GameAudio.Play(GameSound.SaveFailed);
                return false;
            }
            profile = next; Notice = notice; HasSaveProblem = false;
            if (updateEquipment) ApplyEquipment();
            ApplyStats();
            Changed?.Invoke();
            if (sound.HasValue) GameAudio.Play(sound.Value);
            return true;
        }

        void ApplyEquipment() => loadout.ApplyInventoryEquipment(ComposeWeapon(0),ComposeWeapon(1),profile.activeSlot);
    }
}
