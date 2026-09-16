from pathlib import Path
import subprocess
root=Path(__file__).resolve().parents[2]
unity=Path('C:/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor/Data')
out=root/'output/music'
for name, template, folder, editor in [
    ('Mismo.Audio','Mismo.Audio','Assets/Scripts/Audio',False),
    ('Mismo.Gameplay.Player','Mismo.Gameplay.Player','Assets/Scripts/Gameplay/Player',False),
    ('Mismo.Gameplay.Enemies','Mismo.Gameplay.Enemies','Assets/Scripts/Gameplay/Enemies',False),
    ('Mismo.Menu','Mismo.Menu','Assets/Scripts/Menu',False),
    ('Mismo.Audio.Editor','Mismo.Menu.Editor','Assets/Scripts/Audio/Editor',True),
    ('Mismo.Gameplay.Player.Editor','Mismo.Gameplay.Player.Editor','Assets/Scripts/Gameplay/Player/Editor',True)]:
    source_rsp = root/f'Library/Bee/artifacts/1900b0aP.dag/{template}.rsp'
    if not source_rsp.exists(): source_rsp=root/f'Library/Bee/artifacts/1900b0aEDbg.dag/{template}.rsp'
    lines=source_rsp.read_text(encoding='utf-8-sig').splitlines()
    lines=[s for s in lines if not s.startswith(('-out:','-refout:')) and not (s.startswith('"Assets/') and s.endswith('.cs"'))]
    for i,s in enumerate(lines):
        for dependency in ['Mismo.Audio','Mismo.Gameplay.Player','Mismo.Gameplay.Enemies']:
            s=s.replace(f'Library/Bee/artifacts/1900b0aP.dag/{dependency}.ref.dll',f'output/music/{dependency}.dll')
            s=s.replace(f'Library/Bee/artifacts/1900b0aEDbg.dag/{dependency}.ref.dll',f'output/music/{dependency}.dll')
        lines[i]=s
    sources=[p for p in (root/folder).rglob('*.cs') if editor or 'Editor' not in p.relative_to(root/folder).parts]
    lines+=['"'+p.relative_to(root).as_posix()+'"' for p in sources]
    if editor and not any('output/music/Mismo.Audio.dll' in s for s in lines): lines+=['-r:"output/music/Mismo.Audio.dll"']
    lines+=[f'-out:"output/music/{name}.dll"']
    rsp=out/f'{name}.rsp';rsp.write_text('\n'.join(lines),encoding='utf-8')
    result=subprocess.run([str(unity/'NetCoreRuntime/dotnet.exe'),str(unity/'DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll'),'@'+str(rsp)],cwd=root,text=True,capture_output=True)
    print(name, result.returncode, result.stdout, result.stderr)
    (out/f'{name}.compile.txt').write_text(result.stdout+result.stderr,encoding='utf-8')
    if result.returncode: raise SystemExit(result.returncode)
