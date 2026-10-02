from pathlib import Path
import re, subprocess
root=Path(__file__).resolve().parents[2]
out=root/'output/progression-improvements'
unity=Path('C:/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor/Data')
compiled=[]
for name, folder, editor in [
    ('Mismo.Gameplay.Player','Assets/Scripts/Gameplay/Player',False),
    ('Mismo.Gameplay.Enemies','Assets/Scripts/Gameplay/Enemies',False),
    ('Mismo.Gameplay.Player.Editor','Assets/Scripts/Gameplay/Player/Editor',True),
    ('Assembly-CSharp-Editor','Assets/Scripts/Editor',True)]:
    source=root/f'Library/Bee/artifacts/1900b0aP.dag/{name}.rsp'
    if not source.exists():source=root/f'Library/Bee/artifacts/1900b0aEDbg.dag/{name}.rsp'
    lines=source.read_text(encoding='utf-8-sig').splitlines()
    lines=[s for s in lines if not s.startswith(('-out:','-refout:')) and not (s.startswith('"Assets/') and s.endswith('.cs"'))]
    for dependency in compiled:
        lines=[re.sub(r'Library/Bee/artifacts/[^/]+/'+re.escape(dependency)+r'\.ref\.dll',f'output/progression-improvements/{dependency}.dll',s) for s in lines]
    lines+=['"'+p.relative_to(root).as_posix()+'"' for p in (root/folder).rglob('*.cs') if editor or 'Editor' not in p.relative_to(root/folder).parts]
    lines+=[f'-out:"output/progression-improvements/{name}.dll"']
    rsp=out/f'{name}.rsp';rsp.write_text('\n'.join(lines),encoding='utf-8')
    result=subprocess.run([str(unity/'NetCoreRuntime/dotnet.exe'),str(unity/'DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll'),'@'+str(rsp)],cwd=root,text=True,capture_output=True)
    (out/f'{name}.compile.txt').write_text(result.stdout+result.stderr,encoding='utf-8')
    errors=[l for l in result.stdout.splitlines() if 'error ' in l]
    print(name,result.returncode,'\n'.join(errors),flush=True)
    if result.returncode:raise SystemExit(result.returncode)
    compiled.append(name)
