using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Mismo.Gameplay.Player.Equipment.Inventory;

static class Program
{
    static int count;
    static void Check(bool value, string message)
    { if (!value) throw new Exception(message); count++; Console.WriteLine("PASS " + message); }
    static void Main()
    {
        var folder = Path.Combine(Path.GetTempPath(), "MismoInventoryChecks", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "profile.mismo");
        var store = new ProtectedProfileRepository(path);
        Check(store.Read(_ => true, out _) == ProfileReadResult.Missing, "Missing save initializes cleanly");
        store.Write("first valid profile");
        Check(!Encoding.UTF8.GetString(File.ReadAllBytes(path)).Contains("first valid profile"), "Payload is encrypted");
        Check(store.Read(_ => true, out var payload) == ProfileReadResult.Loaded && payload == "first valid profile", "Authenticated encryption round trip");
        byte[] first = File.ReadAllBytes(path);
        store.Write("first valid profile");
        Check(Convert.ToBase64String(first) != Convert.ToBase64String(File.ReadAllBytes(path)), "Fresh random IV per save");
        store.Write("second valid profile");
        var good = File.ReadAllBytes(path);
        foreach (int offset in new[] { 0, 8, 25, good.Length - 1 })
        {
            var altered = (byte[])good.Clone(); altered[offset] ^= 1; File.WriteAllBytes(path, altered);
            Check(new ProtectedProfileRepository(path).Read(_ => true, out var fallback) == ProfileReadResult.Recovered && fallback == "first valid profile",
                "Tampered header, IV, ciphertext or tag recovers backup at offset " + offset);
        }
        File.WriteAllBytes(path, new byte[3]);
        Check(store.Read(_ => true, out _) == ProfileReadResult.Recovered, "Truncated primary recovers backup");
        store.Write("after recovery");
        Check(new ProtectedProfileRepository(path + ".bak").Read(_ => true, out var backup) == ProfileReadResult.Loaded && backup == "first valid profile",
            "Recovery does not replace the good backup with corrupted data");
        Check(new ProtectedProfileRepository(path).Read(text => text == "first valid profile", out _) == ProfileReadResult.Recovered,
            "Authenticated but semantically invalid primary falls back");
        File.Delete(path);
        Check(store.Read(_ => true, out _) == ProfileReadResult.Recovered, "Missing primary can recover existing backup");
        File.WriteAllText(path, "modified"); File.WriteAllText(path + ".bak", "modified");
        Check(store.Read(_ => true, out _) == ProfileReadResult.Invalid, "Two invalid files are not treated as a new profile");
        bool refused = false;
        try { store.Write("overwrite"); } catch (InvalidOperationException) { refused = true; }
        Check(refused && File.ReadAllText(path) == "modified", "Invalid saves cannot be overwritten silently");

        var definitions = new HashSet<string> { "sword", "bow", "reward" };
        var rewards = new Dictionary<string, string> { { "boss", "reward" } };
        var profile = new InventoryProfile();
        foreach (var id in new[] { "sword", "bow" }) profile.weapons.Add(new OwnedWeapon { instanceId = Guid.NewGuid().ToString("N"), definitionId = id });
        profile.equipped[0] = profile.weapons[0].instanceId; profile.equipped[1] = profile.weapons[1].instanceId;
        Check(profile.IsValid(definitions, rewards), "Valid starting profile");
        var invalid = profile.Copy(); invalid.weapons[0].definitionId = "unlisted";
        Check(!invalid.IsValid(definitions, rewards), "Unknown item definition rejected");
        invalid = profile.Copy(); invalid.weapons[1].instanceId = invalid.weapons[0].instanceId;
        Check(!invalid.IsValid(definitions, rewards), "Duplicate instance identity rejected");
        invalid = profile.Copy(); invalid.equipped[1] = invalid.equipped[0];
        Check(!invalid.IsValid(definitions, rewards), "Same instance cannot occupy both slots");
        invalid = profile.Copy(); invalid.equipped[0] = Guid.NewGuid().ToString("N");
        Check(!invalid.IsValid(definitions, rewards), "Equipment must reference an owned instance");
        invalid = profile.Copy(); invalid.version = 99;
        Check(!invalid.IsValid(definitions, rewards), "Unsupported profile version rejected");
        invalid = profile.Copy(); invalid.activeSlot = -1;
        Check(!invalid.IsValid(definitions, rewards), "Invalid active slot rejected");
        invalid = profile.Copy(); invalid.claimedRewards.Add("boss");
        Check(!invalid.IsValid(definitions, rewards), "Claimed reward must have its item");
        Check(profile.TryClaim("boss", "reward") && !profile.TryClaim("boss", "reward") && profile.weapons.Count == 3,
            "Reward grants exactly one instance");
        Check(profile.TryEquip(0, profile.weapons[2].instanceId) && profile.TryEquip(1, profile.weapons[2].instanceId) && profile.IsValid(definitions, rewards),
            "Moving owned equipment exchanges slots safely");
        invalid = profile.Copy(); invalid.claimedRewards.Add("boss");
        Check(!invalid.IsValid(definitions, rewards), "Duplicate reward claims rejected");
        Check(!profile.TryEquip(0, "missing") && !profile.TryEquip(3, profile.weapons[0].instanceId), "Invalid equip requests do not mutate profile");
        invalid=profile.Copy();invalid.weapons[0].tier=0;
        Check(!invalid.IsValid(definitions,rewards),"Tier zero rejected in version 2");
        invalid=profile.Copy();invalid.weapons[0].variant=(WeaponVariant)9;
        Check(!invalid.IsValid(definitions,rewards),"Unknown modifier rejected");
        invalid=profile.Copy();invalid.progression.lifePoints=1;
        Check(!invalid.IsValid(definitions,rewards),"Cannot spend more character points than earned");
        invalid=profile.Copy();invalid.progression.masteries.Add(new MasteryProgress{familyId="sword",speedPoints=1});
        Check(!invalid.IsValid(definitions,rewards),"Cannot overspend mastery points");
        invalid=profile.Copy();invalid.progression.masteries.Add(new MasteryProgress{familyId="sword"});
        invalid.progression.masteries.Add(new MasteryProgress{familyId="sword"});
        Check(!invalid.IsValid(definitions,rewards),"Duplicate mastery families rejected");
        var old=profile.Copy();old.version=1;old.progression=null;old.weapons[0].tier=0;
        Check(old.IsValid(definitions,rewards),"Version 1 remains readable without new fields");
        var savedId=old.equipped[0];old.UpgradeFromVersionOne();
        Check(old.version==2 && old.progression.level==1 && old.weapons[0].tier==1 && old.equipped[0]==savedId && old.IsValid(definitions,rewards),"Migration preserves ownership and equipment");
        var copy=old.Copy();copy.progression.GetOrCreate("sword").level=3;copy.weapons[0].tier=3;
        Check(old.progression.masteries.Count==0 && old.weapons[0].tier==1,"Transaction copy isolates progression and item rolls");
        var world=profile.Copy();world.regions.Add(new DiscoveredRegion{id="world-v1:7319:-2:3",minimumLevel=25});world.defeatedEnemies.Add("world-v1:7319:-4:8:0");
        Check(world.IsValid(definitions,rewards),"Region minimum and defeated slot are accepted");
        var worldCopy=world.Copy();worldCopy.regions[0].minimumLevel=40;worldCopy.defeatedEnemies.Add("another");
        Check(world.regions[0].minimumLevel==25&&world.defeatedEnemies.Count==1,"World transactions do not mutate committed state");
        invalid=world.Copy();invalid.regions.Add(world.regions[0].Copy());Check(!invalid.IsValid(definitions,rewards),"Duplicate region discovery rejected");
        invalid=world.Copy();invalid.defeatedEnemies.Add(world.defeatedEnemies[0]);Check(!invalid.IsValid(definitions,rewards),"Duplicate defeated slots rejected");
        invalid=world.Copy();invalid.regions[0].minimumLevel=0;Check(!invalid.IsValid(definitions,rewards),"Invalid regional level rejected");
        var legacy=profile.Copy();legacy.regions=null;legacy.defeatedEnemies=null;
        Check(legacy.IsValid(definitions,rewards)&&legacy.Copy().regions.Count==0,"Existing saves without world data remain readable");
        Console.WriteLine("INVENTORY CORE: " + count + " checks passed. Test files: " + folder);
        var session=new Mismo.Gameplay.Player.World.WorldSaveData{id=Guid.NewGuid().ToString("N"),seed=7319,settingsJson="{\"seed\":7319}",x=-320,y=14,z=78,yaw=125,spawnX=64,spawnY=12,spawnZ=64};
        Check(session.IsValid(),"Valid world checkpoint accepted");
        var changed=session.Copy();changed.x=100;
        Check(session.x==-320,"Checkpoint copy preserves committed position");
        changed=session.Copy();changed.id="../profile";Check(!changed.IsValid(),"World ID cannot escape profile folder");
        changed=session.Copy();changed.x=float.NaN;Check(!changed.IsValid(),"Non-finite checkpoint rejected");
        changed=session.Copy();changed.version=99;Check(!changed.IsValid(),"Unknown world generation schema rejected");
        changed=session.Copy();changed.legacy=true;Check(!changed.IsValid(),"Legacy world must use explicit legacy identity");
        int previousSeed=session.seed;
        for(int i=0;i<100;i++){int next=Mismo.Gameplay.Player.World.WorldSaveData.FreshSeed(previousSeed);if(next<=0||next==previousSeed)throw new Exception("Seed repeated");previousSeed=next;}
        Check(true,"Each new game chooses a positive seed different from its predecessor");
        var jsonOptions=new System.Text.Json.JsonSerializerOptions{IncludeFields=true};
        string sessionJson=System.Text.Json.JsonSerializer.Serialize(session,jsonOptions);
        var sessionStore=new ProtectedProfileRepository(Path.Combine(folder,"active-world.mismo"));
        bool ValidateWorld(string text){try{return System.Text.Json.JsonSerializer.Deserialize<Mismo.Gameplay.Player.World.WorldSaveData>(text,jsonOptions)?.IsValid()==true;}catch{return false;}}
        sessionStore.Read(ValidateWorld,out _);sessionStore.Write(sessionJson);
        var reopened=new ProtectedProfileRepository(Path.Combine(folder,"active-world.mismo"));
        Check(reopened.Read(ValidateWorld,out var continuedJson)==ProfileReadResult.Loaded,"Continue reopens persisted world after repository recreation");
        var continued=System.Text.Json.JsonSerializer.Deserialize<Mismo.Gameplay.Player.World.WorldSaveData>(continuedJson,jsonOptions);
        Check(continued.id==session.id&&continued.seed==session.seed&&continued.x==session.x&&continued.yaw==session.yaw&&continued.settingsJson==session.settingsJson,"Continue preserves world identity, settings, position and orientation");
        changed=session.Copy();changed.id=Guid.NewGuid().ToString("N");changed.seed=Mismo.Gameplay.Player.World.WorldSaveData.FreshSeed(session.seed);reopened.Write(System.Text.Json.JsonSerializer.Serialize(changed,jsonOptions));
        Check(session.id!=changed.id&&new ProtectedProfileRepository(Path.Combine(folder,"active-world.mismo.bak")).Read(ValidateWorld,out var previousJson)==ProfileReadResult.Loaded&&previousJson==sessionJson,"New world keeps previous active-world backup");
        var materials = profile.Copy();
        Check(materials.TryAddMaterial(MaterialCatalog.Wood, 2) && materials.TryAddMaterial(MaterialCatalog.Stone, 3), "Materials accumulate in existing profile");
        var materialCopy = materials.Copy();
        Check(materialCopy.TryAddMaterial(MaterialCatalog.Wood, 1) && materials.MaterialCount(MaterialCatalog.Wood)==2, "Material transactions isolate committed balances");
        Check(!materials.TrySpendMaterials(new Dictionary<string,int>{{MaterialCatalog.Wood,2},{MaterialCatalog.Stone,4}}) && materials.MaterialCount(MaterialCatalog.Wood)==2, "Insufficient recipe leaves all balances unchanged");
        Check(materials.TrySpendMaterials(new Dictionary<string,int>{{MaterialCatalog.Wood,2},{MaterialCatalog.Stone,3}}) && materials.materials.Count==0, "Exact recipe spends all ingredients atomically");
        Check(!materials.TryAddMaterial("",1) && !materials.TryAddMaterial(MaterialCatalog.Herb,-1) && !materials.TryAddMaterial(MaterialCatalog.Herb,0), "Empty identities and nonpositive material grants rejected");
        Check(materials.TryAddMaterial(MaterialCatalog.Herb,int.MaxValue) && !materials.TryAddMaterial(MaterialCatalog.Herb,1), "Material overflow rejected without mutation");
        invalid=materials.Copy();invalid.materials.Add(invalid.materials[0].Copy());Check(!invalid.IsValid(definitions,rewards), "Duplicate material stacks rejected");
        invalid=materials.Copy();invalid.materials[0].quantity=-1;Check(!invalid.IsValid(definitions,rewards), "Negative saved quantities rejected");
        invalid=materials.Copy();invalid.materials[0].id="";Check(!invalid.IsValid(definitions,rewards), "Empty saved material identities rejected");
        invalid=profile.Copy();invalid.materials=null;Check(invalid.IsValid(definitions,rewards) && invalid.Copy().MaterialCount(MaterialCatalog.Herb)==0, "Older saves without materials remain valid");
        var materialStore=new ProtectedProfileRepository(Path.Combine(folder,"materials.mismo"));
        bool ValidateMaterials(string text){try{return System.Text.Json.JsonSerializer.Deserialize<InventoryProfile>(text,jsonOptions)?.IsValid(definitions,rewards)==true;}catch{return false;}}
        materialStore.Read(ValidateMaterials,out _);materialStore.Write(System.Text.Json.JsonSerializer.Serialize(materials,jsonOptions));
        var materialReload=new ProtectedProfileRepository(Path.Combine(folder,"materials.mismo"));
        Check(materialReload.Read(ValidateMaterials,out var materialJson)==ProfileReadResult.Loaded && System.Text.Json.JsonSerializer.Deserialize<InventoryProfile>(materialJson,jsonOptions).MaterialCount(MaterialCatalog.Herb)==int.MaxValue, "Protected save reload preserves materials");
        var bag=profile.Copy();bag.TryAddMaterial(MaterialCatalog.Wood,20);
        int StackSize(string id)=>20;
        Check(InventoryStorage.Used(bag,false,StackSize)==bag.weapons.Count+1,"Full stack uses one backpack slot");
        bag.TryAddMaterial(MaterialCatalog.Wood,1);
        Check(InventoryStorage.Used(bag,false,StackSize)==bag.weapons.Count+2,"Stack overflow uses an additional slot");
        Check(InventoryStorage.TransferMaterial(bag,MaterialCatalog.Wood,20,true) && bag.MaterialCount(MaterialCatalog.Wood)==1 && bag.chestMaterials[0].quantity==20,"Deposit moves exact quantity between containers");
        Check(!InventoryStorage.TransferMaterial(bag,MaterialCatalog.Wood,2,true) && bag.MaterialCount(MaterialCatalog.Wood)==1,"Invalid transfer preserves source");
        Check(InventoryStorage.TransferMaterial(bag,MaterialCatalog.Wood,5,false) && bag.MaterialCount(MaterialCatalog.Wood)==6,"Withdrawal moves exact quantity back");
        var stored=bag.Copy();stored.chestMaterials[0].quantity=1;
        Check(bag.chestMaterials[0].quantity==15,"Chest transaction copy isolates balances");
        stored=bag.Copy();stored.weapons[2].inChest=true;
        Check(!stored.TryEquip(0,stored.weapons[2].instanceId),"Stored weapon cannot equip remotely");
        stored=bag.Copy();stored.Find(stored.equipped[0]).inChest=true;
        Check(!stored.IsValid(definitions,rewards),"Stored weapon cannot remain equipped in save");
        var lootRecord=new PendingInventoryLoot{id=Guid.NewGuid().ToString("N"),x=1,y=2,z=3,weapon=new OwnedWeapon{instanceId=Guid.NewGuid().ToString("N"),definitionId="sword"}};
        bag.pendingLoot.Add(lootRecord);
        Check(bag.IsValid(definitions,rewards),"Uncollected loot is valid persistent state");
        stored=bag.Copy();stored.pendingLoot[0].weapon.tier=3;
        Check(bag.pendingLoot[0].weapon.tier==1,"Pending reward transaction copy isolates rolls");
        stored=bag.Copy();stored.pendingLoot.Add(lootRecord.Copy());
        Check(!stored.IsValid(definitions,rewards),"Duplicate pending rewards rejected");
        stored=bag.Copy();stored.pendingLoot[0].weapon.instanceId=stored.weapons[0].instanceId;
        Check(!stored.IsValid(definitions,rewards),"Same weapon cannot exist on ground and in inventory");
        stored=bag.Copy();stored.chestMaterials=null;stored.pendingLoot=null;stored.favoriteMaterials=null;
        Check(stored.IsValid(definitions,rewards)&&stored.Copy().pendingLoot.Count==0,"Old saves without container metadata remain readable");
        var shapes=new List<GridItem>{new GridItem{key="sword",width=1,height=3},new GridItem{key="ore",width=1,height=1}};
        var arranged=InventoryGrid.Arrange(shapes,new List<GridPlacement>(),4,3,false);
        Check(arranged.Count==2,"Variable-size objects receive nonoverlapping positions");
        Check(!InventoryGrid.CanMove(shapes,arranged,new GridPlacement{key="ore",x=0,y=0},4,3),"Grid rejects overlap with long weapon");
        Check(!InventoryGrid.CanMove(shapes,arranged,new GridPlacement{key="sword",x=3,y=1},4,3),"Grid rejects footprint outside lower edge");
        Check(InventoryGrid.CanMove(shapes,arranged,new GridPlacement{key="sword",x=0,y=2,rotated=true},4,3),"Rotation changes footprint to fit a horizontal gap");
        var horizontal=new List<GridItem>{new GridItem{key="blade",width=1,height=3}};
        Check(InventoryGrid.Arrange(horizontal,new List<GridPlacement>(),3,1,false)[0].rotated,"Auto-placement rotates when necessary");
        horizontal[0].canRotate=false;Check(InventoryGrid.Arrange(horizontal,new List<GridPlacement>(),3,1,false).Count==0,"Nonrotatable object cannot bypass geometry");
        var savedLayout=new List<GridPlacement>{new GridPlacement{key="sword",x=3,y=0}};
        var preservedLayout=InventoryGrid.Arrange(shapes,savedLayout,4,3,false);
        Check(preservedLayout.Find(g=>g.key=="sword").x==3,"Acquisition preserves manually placed objects");
        var fragments=new List<GridItem>{new GridItem{key="a"},new GridItem{key="b"},new GridItem{key="c"},new GridItem{key="d"},new GridItem{key="large",width=2,height=2}};
        var fragmented=new List<GridPlacement>{new GridPlacement{key="a",x=1,y=0},new GridPlacement{key="b",x=3,y=0},new GridPlacement{key="c",x=1,y=1},new GridPlacement{key="d",x=3,y=1}};
        Check(InventoryGrid.Arrange(fragments,fragmented,4,2,false).Count==4,"Enough free area does not bypass fragmented space");
        var migration=profile.Copy();migration.version=3;migration.gridPlacements=null;migration.UpgradeToCurrent();
        Check(migration.version==5&&migration.IsValid(definitions,rewards),"Version 3 migrates without losing belongings");
        var gathering=profile.Copy();gathering.worldPlaySeconds=150;gathering.harvestedNodes.Add(new HarvestState{id="node:seed:chunk:slot",readyAt=450});
        Check(gathering.IsValid(definitions,rewards),"Gathering clock and depletion state are valid");
        var gatheringCopy=gathering.Copy();gatheringCopy.harvestedNodes[0].readyAt=900;
        Check(gathering.harvestedNodes[0].readyAt==450,"Gathering transaction copies node state independently");
        gatheringCopy.harvestedNodes.Add(gatheringCopy.harvestedNodes[0].Copy());
        Check(!gatheringCopy.IsValid(definitions,rewards),"Duplicate persistent nodes are rejected");
        gatheringCopy=gathering.Copy();gatheringCopy.worldPlaySeconds=double.NaN;
        Check(!gatheringCopy.IsValid(definitions,rewards),"Invalid world clocks are rejected");
        gatheringCopy=gathering.Copy();gatheringCopy.harvestedNodes[0].readyAt=-1;
        Check(!gatheringCopy.IsValid(definitions,rewards),"Invalid regeneration deadlines are rejected");
        Console.WriteLine("INVENTORY AND WORLD CORE: "+count+" checks passed.");
    }
}
