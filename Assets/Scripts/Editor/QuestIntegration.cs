using System;
using System.IO;
using System.Linq;
using Mismo.Core;
using Mismo.Gameplay.Player.Quests;
using Mismo.Gameplay.Player.Equipment.Inventory;
using Mismo.Gameplay.Player.World;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Explicit, repeatable setup: creates missing quest assets without resetting authored values.</summary>
[InitializeOnLoad]
public static class QuestIntegration
{
    const string Root="Assets/Data/Quests";
    static QuestIntegration(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        const string request="Temp/QuestSetup.request";
        if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists(request))return;
        File.Delete(request);
        try{Setup();File.WriteAllText("Temp/QuestSetup.result","PASS");}
        catch(Exception e){File.WriteAllText("Temp/QuestSetup.result",e.ToString());Debug.LogException(e);}
    }
    static T Create<T>(string path,Action<T> initialize) where T:ScriptableObject
    {
        var asset=AssetDatabase.LoadAssetAtPath<T>(path);if(asset!=null)return asset;
        Directory.CreateDirectory(Path.GetDirectoryName(path));AssetDatabase.Refresh();
        asset=ScriptableObject.CreateInstance<T>();initialize(asset);AssetDatabase.CreateAsset(asset,path);return asset;
    }
    static MaterialDefinition Material(string id)=>ProjectAssets.LoadAll<MaterialDefinition>("Materials").First(m=>m.id==id);
    static QuestNpcDefinition Npc(string id,string name,string role,string greeting)
        =>Create<QuestNpcDefinition>(Root+"/NPCs/"+id+".asset",n=>
        {n.id=id;n.displayName=name;n.occupation=role;n.greeting=greeting;n.offer="Hay un trabajo que podrías hacer por el pueblo.";n.accepted="Gracias. Prepará tu equipo y volvé cuando termines.";n.inProgress="No hace falta que te apures. Revisá lo que falta en tu diario.";n.readyToDeliver="Veo que ya está todo. ¿Cerramos el pedido?";n.completed="Cumpliste tu palabra. Acá está lo acordado.";});
    static QuestDefinition Quest(string id,Action<QuestDefinition> initialize)=>Create<QuestDefinition>(Root+"/Missions/"+id+".asset",q=>{q.id=id;initialize(q);});
    static QuestObjective Collect(string id,string label,MaterialDefinition material,int amount)=>new QuestObjective{id=id,label=label,kind=QuestObjectiveKind.Material,material=material,quantity=amount};
    static QuestObjective Signal(string id,string label,string signal,bool previous=false)=>new QuestObjective{id=id,label=label,kind=QuestObjectiveKind.Signal,signal=signal,requirePrevious=previous,consumeOnDelivery=false};
    [MenuItem("Mismo/Quests/Crear ejemplos y registrar catálogo")]
    public static void Setup()
    {
        var herb=Material("MAT-01");var iron=Material("MAT-06");var crystal=Material("MAT-07");var salve=Material("MAT-05");
        var mara=Npc("mara","Mara","Herborista","Bienvenido. Afuera hay mucho por descubrir; primero aprendamos a cuidar esas heridas.");
        var bruno=Npc("bruno","Bruno","Herrero","El fuego está listo. Lo que falta no se consigue sentado junto a la fragua.");
        var elena=Npc("elena","Elena","Molinera","Si el camino al molino se corta, todo el pueblo lo siente.");
        var iria=Npc("iria","Iria","Guardiana del archivo","Las ruinas todavía tienen algo que decir. Hay que saber escuchar.");
        var tomas=Npc("tomas","Tomás","Vigía","Desde esta torre se ve el camino… y también lo que viene por él.");
        var travel=Create<CraftingRecipe>(Root+"/Recipes/TravelSalve.asset",r=>{r.id="REC-QUEST-01";r.displayName="Ungüentos de viaje";r.description="Prepará dos ungüentos con tres hierbas. Una técnica que te enseñó Mara.";r.requiresLearning=true;r.craftInWorld=true;r.ingredients=new[]{new RecipeIngredient{material=herb,quantity=3}};r.result=salve;r.quantity=2;});
        var sharpening=Create<CraftingRecipe>(Root+"/Recipes/FieldWhetstone.asset",r=>{r.id="REC-QUEST-02";r.displayName="Piedra de afilar de campaña";r.description="El método de Bruno permite preparar una piedra de afilar durante la exploración.";r.requiresLearning=true;r.craftInWorld=true;r.ingredients=new[]{new RecipeIngredient{material=Material("MAT-03"),quantity=2},new RecipeIngredient{material=iron,quantity=1}};r.result=Material("MAT-09");});
        var herbs=Quest("Q-HERBS",q=>{q.title="Hierbas para el botiquín";q.description="Conseguí hierba medicinal y llevásela a Mara. Te enseñará una receta para preparar tus próximas expediciones.";q.location="Alrededores del pueblo";q.npc=mara;q.objectives=new[]{Collect("herbs","Conseguí hierba medicinal",herb,5)};q.coins=20;q.items=new[]{new QuestItemReward{material=salve,quantity=2}};q.recipes=new[]{travel};q.offerText="Buscá cinco hierbas medicinales. Acercate a una planta y usá la interacción de recolección. Si ya tenés hierbas en la mochila, también sirven. Traémelas y te enseño a aprovecharlas mejor.";q.acceptedText="Buscá con calma. La recolección se interrumpe si te movés o te atacan; despejá la zona primero.";q.progressText="Necesito cinco hierbas en tu mochila. Si las usaste para curarte, conseguí algunas más antes de volver.";q.readyText="Están frescas, justo como las necesito. A cambio te doy dos ungüentos y te enseño mi receta de viaje.";q.completedText="Ya sabés preparar ungüentos de viaje. Buscá la nueva receta en tu libro: tres hierbas rinden dos ungüentos. Y acá tenés tu pago.";});
        var craft=Quest("Q-PREPARE",q=>{q.title="Antes de volver a salir";q.description="Mara quiere que pongas en práctica su receta. Fabricá ungüentos desde el libro de recetas.";q.location="Se fabrica a mano";q.npc=mara;q.prerequisites=new[]{herbs};q.objectives=new[]{new QuestObjective{id="craft",label="Fabricá ungüentos de viaje",kind=QuestObjectiveKind.CraftRecipe,recipe=travel,consumeOnDelivery=false}};q.coins=25;q.offerText="Una receta en el papel no alcanza. Juntá tres hierbas y prepará una tanda de ungüentos de viaje desde Recetas. Después contame cómo salió.";q.progressText="Abrí Recetas desde el menú radial. Elegí Ungüentos de viaje y fabricá una tanda; no necesitás una mesa.";q.readyText="Ese aroma me dice que seguiste bien los pasos. Los ungüentos son tuyos; guardalos para el camino.";q.completedText="Ahora podés preparar tus propias provisiones. Descansá cuando haga falta y elegí bien cuándo regresar.";});
        var metal=Quest("Q-IRON",q=>{q.title="El primer encargo";q.description="Bruno necesita hierro para la fragua. Reuní el mineral y aprendé a preparar una piedra de afilar en el campo.";q.location="Yacimientos de hierro";q.npc=bruno;q.objectives=new[]{Collect("iron","Conseguí mineral de hierro",iron,10)};q.coins=40;q.recipes=new[]{sharpening};q.offerText="Traeme diez minerales de hierro. Podés quedarte explorando si la mochila da para más, pero no hace falta arriesgar todo por una veta. Te pagaré y te enseñaré a afilar el arma fuera del taller.";q.progressText="Son diez minerales de hierro. Los necesito en la mochila para poder trabajar con ellos.";q.readyText="Buen mineral. ¿Me lo dejás? La receta y el pago están preparados.";q.completedText="Trato hecho. Ya podés fabricar una piedra de afilar de campaña desde Recetas. Probala antes de una pelea difícil.";});
        var hunt=Quest("Q-BOARS",q=>{q.title="Jabalíes en el camino";q.description="Elena pide ayuda para reducir la amenaza de los jabalíes. Las derrotas cuentan desde que aceptás el encargo.";q.location="Zonas donde habitan jabalíes";q.npc=elena;q.objectives=new[]{new QuestObjective{id="boars",label="Derrotá jabalíes",kind=QuestObjectiveKind.DefeatSpecies,species=AssetDatabase.LoadAssetAtPath<CreatureSpecies>("Assets/Data/Bestiary/boar.asset"),quantity=4,consumeOnDelivery=false}};q.coins=60;q.items=new[]{new QuestItemReward{material=salve,quantity=1}};q.offerText="Necesito que te ocupes de cuatro jabalíes. No te rodees de varios a la vez: observá cómo atacan y buscá una apertura. Te guardaré sesenta monedas y un ungüento.";q.acceptedText="Te anoto el encargo. Contaremos los jabalíes que derrotes a partir de ahora.";q.progressText="Todavía queda trabajo. En el diario podés ver cuántos jabalíes llevás.";q.readyText="Los viajeros ya empiezan a volver al camino. Vení, te debo una recompensa.";q.completedText="Gracias. Con el camino más tranquilo, mañana volveremos a llevar harina al pueblo.";});
        var altar=Quest("Q-ALTAR",q=>{q.title="El altar de los cuatro vientos";q.category=QuestCategory.Misterios;q.description="Encontrá el altar del bosque e investigá sus piedras. Las pistas encontradas quedarán anotadas en tu diario.";q.location="Ruinas del bosque";q.npc=iria;q.offerInJournal=false;q.objectives=new[]{Signal("find","Encontrá el altar","altar.found"),Signal("solve","Orientá los cuatro guardianes","altar.solved",true)};q.clueAfterObjective="find";q.clue="De izquierda a derecha: el primero mira al amanecer; el segundo, al ocaso. El tercero busca la estrella del norte; el último le vuelve la espalda.";q.coins=80;q.items=new[]{new QuestItemReward{material=crystal,quantity=3}};q.offerText="Hay cuatro guardianes de piedra en un altar olvidado. Cada uno mira hacia un punto cardinal. Encontrá su inscripción: no necesito una respuesta apresurada, sino que entiendas qué te pide.";q.progressText="Buscá la inscripción y observá el orden de las piedras. El amanecer y el ocaso pueden orientarte.";q.readyText="Entonces el altar respondió. Esos guardianes llevaban mucho tiempo esperando.";q.completedText="Conservá estos cristales. Quizá algún día encuentres otra estructura que responda a su energía.";});
        var raid=Quest("Q-RAID",q=>{q.title="El pueblo bajo asedio";q.category=QuestCategory.Eventos;q.description="La campana anuncia un ataque. Ayudá a los defensores y regresá con Tomás cuando el asalto termine.";q.location="Entrada del pueblo";q.npc=tomas;q.offerInJournal=false;q.objectives=new[]{Signal("defense","Defendé el pueblo hasta el final del asalto","raid.defended")};q.coins=80;q.items=new[]{new QuestItemReward{material=iron,quantity=3}};q.offerText="¡Sonó la campana! Necesitamos ayuda en la entrada. Mantené a los atacantes lejos del pueblo.";q.progressText="El asalto sigue. Cuidá tu posición y ayudá a los defensores.";q.readyText="Ya no vienen más. Respirá. Cuando estés listo, tengo algo para vos.";q.completedText="El pueblo sigue en pie. Este pago y el hierro son tu parte por ayudarnos a defenderlo.";});
        var anomaly=Quest("Q-ANOMALY",q=>{q.title="Un eco entre las piedras";q.category=QuestCategory.Eventos;q.description="Iria te pide investigar una anomalía y estabilizar su núcleo.";q.location="Origen de la anomalía";q.npc=iria;q.offerInJournal=false;q.objectives=new[]{Signal("find","Investigá la anomalía","anomaly.found"),Signal("close","Estabilizá el núcleo","anomaly.closed",true)};q.coins=70;q.items=new[]{new QuestItemReward{material=crystal,quantity=2}};q.offerText="Algo está alterando el lugar. Acercate con cuidado, observá el núcleo y buscá cómo estabilizarlo.";q.progressText="No intentes resolverlo a ciegas. Primero investigá la anomalía; después ocupate del núcleo.";q.readyText="El ruido desapareció. Contame qué encontraste.";q.completedText="Anotaré lo ocurrido. Llevate estos cristales y el pago: tu observación puede ayudarnos con la próxima anomalía.";});
        var catalog=Create<QuestCatalog>(Root+"/QuestCatalog.asset",c=>{c.quests=new[]{herbs,craft,metal,hunt,altar,raid,anomaly};c.journalIcon=JournalIcon();c.radialSurface=Radial("Surface",-2);c.radialOutline=Radial("Outline",-1);c.radialSelected=Enumerable.Range(0,8).Select(i=>Radial("Selected"+i,i)).ToArray();});
        var runtime=AssetDatabase.LoadAssetAtPath<RuntimeAssetCatalog>(ProjectAssets.CatalogPath);
        var entries=runtime.entries.ToList();
        foreach(var entry in new[]{new RuntimeAssetCatalog.Entry{key="QuestCatalog",assets=new Object[]{catalog}},new RuntimeAssetCatalog.Entry{key="Recipes/QuestTravelSalve",assets=new Object[]{travel}},new RuntimeAssetCatalog.Entry{key="Recipes/QuestFieldWhetstone",assets=new Object[]{sharpening}}})
        {entries.RemoveAll(e=>e.key==entry.key);entries.Add(entry);}
        // Any recipe authored as a quest reward becomes a real book/crafting dependency.
        foreach(var reward in catalog.quests.Where(q=>q!=null).SelectMany(q=>q.recipes??Array.Empty<CraftingRecipe>()).Where(r=>r!=null).Distinct())
            if(!entries.Any(e=>(e.key=="Recipes"||e.key.StartsWith("Recipes/",StringComparison.Ordinal))&&e.assets!=null&&e.assets.Contains(reward)))
                entries.Add(new RuntimeAssetCatalog.Entry{key="Recipes/QuestRewards/"+reward.id,assets=new Object[]{reward}});
        runtime.entries=entries.ToArray();runtime.Invalidate();EditorUtility.SetDirty(runtime);AssetDatabase.SaveAssetIfDirty(runtime);
        CreateAltar(altar);
        foreach(var q in catalog.quests)if(q==null||!q.Validate(out _))throw new InvalidOperationException("Invalid quest asset: "+q?.name);
        ProjectOrganizationChecks.Run();Debug.Log("QUEST_SETUP_OK");
    }
    static Texture2D JournalIcon()
    {
        const string path="Assets/Art/UI/Quests/Journal.png";
        if(!File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));var t=new Texture2D(32,32,TextureFormat.RGBA32,false);var pixels=new Color32[1024];
            for(int y=5;y<28;y++)for(int x=6;x<26;x++)
            {
                bool edge=x==6||x==25||y==5||y==27;bool line=x>=10&&x<=21&&(y==11||y==16||y==21);
                if(edge||line)pixels[y*32+x]=new Color32(237,232,211,255);
            }
            t.SetPixels32(pixels);t.Apply();File.WriteAllBytes(path,t.EncodeToPNG());Object.DestroyImmediate(t);
        }
        return ImportTexture(path);
    }
    static Texture2D ImportTexture(string path)
    {
        AssetDatabase.ImportAsset(path);var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Default;importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    static bool Shape(int x,int y,int sector)
    {
        float dx=x-127.5f,dy=y-127.5f,r=Mathf.Sqrt(dx*dx+dy*dy);
        if(sector<0)return r<40;
        float angle=Mathf.Atan2(dx,dy)*Mathf.Rad2Deg;
        return r>=46&&r<=124&&Mathf.Abs(Mathf.DeltaAngle(angle,sector*45))<=20.5f;
    }
    static bool Inner(int x,int y,int sector,int radius)
    {for(int a=-radius;a<=radius;a++)for(int b=-radius;b<=radius;b++)if(!Shape(x+a,y+b,sector))return false;return true;}
    static Texture2D Radial(string name,int mode)
    {
        string path="Assets/Art/UI/Quests/Radial"+name+".png";
        if(!File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));var pixels=new Color32[256*256];
            for(int y=0;y<256;y++)for(int x=0;x<256;x++)for(int sector=-1;sector<8;sector++)
            {
                if(mode>=0&&sector!=mode||!Shape(x,y,sector))continue;
                bool inner=Inner(x,y,sector,3);Color32 color=new Color32(0,0,0,0);
                if(mode==-2){if(inner)color=new Color32(12,18,21,240);}
                else if(!Inner(x,y,sector,1)||Inner(x,y,sector,2)&&!inner)color=new Color32(8,12,14,255);
                else if(!inner)color=mode>=0?new Color32(239,206,127,255):new Color32(225,230,224,255);
                else if(mode>=0)color=new Color32(45,43,30,245);
                pixels[y*256+x]=color;break;
            }
            var t=new Texture2D(256,256,TextureFormat.RGBA32,false);t.SetPixels32(pixels);t.Apply();File.WriteAllBytes(path,t.EncodeToPNG());Object.DestroyImmediate(t);
        }
        return ImportTexture(path);
    }
    static void CreateAltar(QuestDefinition quest)
    {
        const string path="Assets/Art/Prefabs/Quests/FourWindsAltar.prefab";if(File.Exists(path))return;
        Directory.CreateDirectory(Path.GetDirectoryName(path));AssetDatabase.Refresh();
        var previewScene=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var root=new GameObject("Altar de los cuatro vientos");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,previewScene);
        try
        {
            var altar=root.AddComponent<CompassAltar>();altar.stones=new CompassStone[4];
            var trigger=root.AddComponent<BoxCollider>();trigger.isTrigger=true;trigger.size=new Vector3(12,4,7);
            var discovery=root.AddComponent<QuestSignalTrigger>();discovery.startQuest=quest;
            var giver=root.AddComponent<QuestGiver>();giver.npc=quest.npc;giver.quests=new[]{quest};giver.interactionLabel="Leer las notas de Iria";giver.interactionRange=2;
            for(int i=0;i<4;i++)
            {
                var stone=GameObject.CreatePrimitive(PrimitiveType.Cube);stone.name="Guardián "+(i+1);stone.transform.SetParent(root.transform);stone.transform.localPosition=new Vector3(-4.5f+i*3,.5f,0);stone.transform.localScale=new Vector3(1.2f,1,1.2f);
                var spin=stone.AddComponent<CompassStone>();spin.altar=altar;spin.direction=CardinalDirection.North;spin.interactionLabel="Girar guardián "+(i+1);spin.interactionRange=2;
                var pivot=new GameObject("Orientación");pivot.transform.SetParent(stone.transform,false);spin.pointer=pivot.transform;
                var arrow=GameObject.CreatePrimitive(PrimitiveType.Cube);arrow.name="Marca hacia el norte";arrow.transform.SetParent(pivot.transform,false);arrow.transform.localPosition=new Vector3(0,.6f,.3f);arrow.transform.localScale=new Vector3(.18f,.15f,.7f);Object.DestroyImmediate(arrow.GetComponent<Collider>());
                altar.stones[i]=spin;
            }
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally{Object.DestroyImmediate(root);UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(previewScene);}
    }
    public static void Build()
    {
        if(!Directory.GetCurrentDirectory().Replace('\\','/').Contains("/.validation/"))throw new InvalidOperationException("Build in isolated validation project.");
        Setup();Directory.CreateDirectory("output/quest-build");
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/MainMenu.unity","Assets/Scenes/VoxelRegion_7319.unity"},locationPathName="output/quest-build/Mismo.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
        if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Quest build failed: "+report.summary.result);
        Debug.Log("QUEST_BUILD_OK "+report.summary.totalSize);
    }
}
