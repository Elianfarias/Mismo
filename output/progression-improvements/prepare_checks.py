from pathlib import Path
import json,shutil,re
root=Path(__file__).resolve().parents[2]
project=root/'.validation/ProgressionImprovements'
for folder in ['Assets/Scripts/Runtime','Assets/Scripts/Editor','Packages','ProjectSettings']:(project/folder).mkdir(parents=True,exist_ok=True)
names=['Mismo.Core','Mismo.Audio','Mismo.Gameplay.Player','Mismo.Gameplay.Enemies','Assembly-CSharp','MoreMountains.Tools','Lofelt.NiceVibrations','Unity.InputSystem','UnityEngine.UI','Unity.TextMeshPro','Unity.Mathematics','Unity.Collections','Unity.Burst','Unity.Profiling.Core']
names+=['UnityEditor.UI','Unity.Postprocessing.Runtime','Unity.Localization','Unity.Addressables','Unity.ResourceManager']
names += [p.stem for p in (root/'Library/ScriptAssemblies').glob('*.dll') if 'Editor' not in p.stem and ('RenderPipeline' in p.stem or 'ShaderLibrary' in p.stem or p.stem in ['Unity.InternalAPIEngineBridge.013','Unity.InternalAPIEngineBridge.004','Unity.InternalAPIEngineBridge.RenderPipelines.Core.Runtime.Shared','Unity.InternalAPIEngineBridge.ObjectDispatcher.Runtime'])]
for name in set(names):
    updated=root/'output/progression-improvements'/f'{name}.dll'
    source=updated if updated.exists() else root/'Library/ScriptAssemblies'/f'{name}.dll'
    if source.exists():shutil.copy2(source,project/'Assets/Scripts/Runtime'/source.name)
for name in ['Unity.Collections.LowLevel.ILSupport.dll','Newtonsoft.Json.dll']:
    candidates=list((root/'Library/PackageCache').rglob(name))
    if candidates:shutil.copy2(candidates[0],project/'Assets/Scripts/Runtime'/name)
shutil.copy2(root/'Assets/Scripts/Editor/ProgressionImprovementsChecks.cs',project/'Assets/Scripts/Editor/ProgressionImprovementsChecks.cs')
shutil.copy2(root/'Assets/Scripts/Editor/ProgressionEvolutionChecks.cs',project/'Assets/Scripts/Editor/ProgressionEvolutionChecks.cs')
shutil.copy2(root/'Assets/Scripts/Editor/ParryRegressionPlayChecks.cs',project/'Assets/Scripts/Editor/ParryRegressionPlayChecks.cs')
shutil.copy2(root/'Assets/Scripts/Editor/ProjectOrganizationChecks.cs',project/'Assets/Scripts/Editor/ProjectOrganizationChecks.cs')
manifest=json.loads((root/'Packages/manifest.json').read_text(encoding='utf-8-sig'))
manifest['dependencies']={k:v for k,v in manifest['dependencies'].items() if k.startswith('com.unity.modules.')}
(project/'Packages/manifest.json').write_text(json.dumps(manifest,indent=2))
shutil.copy2(root/'ProjectSettings/ProjectVersion.txt',project/'ProjectSettings/ProjectVersion.txt')
print(project)
