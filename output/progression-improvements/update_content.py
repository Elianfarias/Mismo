from pathlib import Path
import json,re,uuid
root=Path(__file__).resolve().parents[2]
replacements={
'M1 es tu ataque básico. Q, E y R son los espacios de habilidades del arma equipada. Sus iconos muestran recarga y disponibilidad.':
'Empezás con el ataque básico M1 y sin habilidades en Q, E o R. El daño real a monstruos aumenta la maestría de esa familia de arma. Ganás 1 punto de habilidad cada 3 niveles de maestría (3, 6, 9…): abrí K, elegí cualquiera, desbloqueala y arrastrala a un espacio. Hacelo fuera de combate.',
'Con la espada inicial, E activa la parada. Usala justo antes de que conecte el golpe del goblin. Un parry acertado te da una oportunidad de contraatacar. Podés ganar este encuentro aunque todavía no te salga.':
'Desde maestría 3, abrí K fuera de combate para elegir una habilidad con tus puntos de habilidad. Si elegís Parada, arrastrala a Q, E o R y usá esa tecla justo antes del impacto. También podés elegir otra habilidad y ganar con M1 y la esquiva C: la parada es opcional.',
'Ahora tenés {q} en Q. Cuesta {focusCost} Focus: acertá disparos básicos para reunirlo y después presioná Q. La habilidad consume ese recurso.':
'El arco también empieza sin habilidades. En maestría 3, 6, 9… ganás puntos de habilidad: abrí K y elegí cuál desbloquear. Equipala en Q, E o R. Si consume Focus, reunilo acertando básicos; el borde de su icono indica cuándo podés usarla.',
'Ahora tenés {q} en Q. Cuesta {focusCost} Focus: acertá disparos básicos para reunirlo y después presioná Q.':
'El arco también empieza sin habilidades. En maestría 3, 6, 9… ganás puntos de habilidad: abrí K y elegí cuál desbloquear. Equipala en Q, E o R. Si consume Focus, reunilo acertando básicos; el borde de su icono indica cuándo podés usarla.',
'Correr, esquivar y algunas acciones gastan estamina. Dejá de gastarla un momento para recuperarla; reservá un poco para defenderte.':
'Correr, esquivar y atacar gastan estamina. Empezás con 80. En Personaje (P) podés invertir puntos para aumentar su capacidad: la barra se alarga. Dejá de gastar estamina un momento para recuperarla.',
'Correr con Shift, esquivar y algunas acciones gastan estamina. Dejá de gastarla un momento para recuperarla y reservá un poco para defenderte.':
'Correr con Shift, esquivar y atacar gastan estamina. Empezás con 80. En Personaje (P) podés aumentar su capacidad y ver crecer la barra. Dejá de gastarla un momento para recuperarla.',
'Dos goblins custodian el último tramo. Alterná espada y arco, esquivá, aprovechá el stagger y usá tus habilidades cuando estén disponibles.':
'Dos goblins custodian el último tramo. Alterná espada y arco, esquivá y aprovechá el stagger. Usá las habilidades que hayas elegido. Al dominarlas con usos efectivos contra monstruos podrás elegir un modificador opcional en K. Usarlas al aire no da progreso.',
'Practicar parry':'Elegir habilidades',
'Espada: E justo antes del impacto.\nDerrotá al goblin para seguir.':'Desde maestría 3, elegí una habilidad en K.\nM1 y C alcanzan para superar este encuentro.',
'Mantené M1 para tensar y soltá para disparar. Acertá para generar Focus y usar Q.':'Mantené M1 para tensar y soltá para disparar. Ganá maestría con el arco y elegí sus habilidades en K.',
'Tu habilidad Q':'Elegir una habilidad del arco',
'La habilidad Q del arco':'Elegir una habilidad del arco',
'Parry con espada':'Elegir tu primera habilidad',
'Acertá disparos con M1 y usá Q.\nDerrotá al goblin para seguir.':'Acertá disparos con M1 para ganar maestría.\nElegí las habilidades del arco en K.',
}
# Both the serialized lessons and their authoring sources keep the same wording.
for path in list((root/'Assets/Data/Tutorial').glob('*.asset'))+[root/'Assets/Scenes/DemoIntroduction.unity']:
    original=path.read_text(encoding='utf-8-sig')
    lines=[]
    for line in original.splitlines():
        match=re.match(r'(\s*(?:- )?(?:title|body|instruction): )(.+)',line)
        if match:
            raw=match[2]
            value=json.loads(re.sub(r'\\x([0-9a-fA-F]{2})',r'\\u00\1',raw)) if raw.startswith('"') else raw
            if value in replacements:line=match[1]+json.dumps(replacements[value],ensure_ascii=False)
        lines.append(line)
    updated='\n'.join(lines)+'\n'
    if path.name in ['Parry.asset','Focus.asset','WorldFocus.asset']:
        updated=updated.replace('    highlight: 7','    highlight: 4').replace('    highlight: 6','    highlight: 4')
    if updated!=original:path.write_text(updated,encoding='utf-8',newline='\n')
for relative in ['Assets/Scripts/Editor/WorldIntroductionBuilder.cs','Assets/Scripts/Gameplay/Player/Editor/DemoIntroductionBuilder.cs']:
    path=root/relative;source=path.read_text(encoding='utf-8-sig')
    for old,new in replacements.items():source=source.replace(json.dumps(old,ensure_ascii=False),json.dumps(new,ensure_ascii=False))
    path.write_text(source,encoding='utf-8')
# Only repertoire skills receive initial, optional modifiers. Basics remain unchanged.
guids=set()
for path in (root/'Assets/Data/WeaponFamilies').glob('*.asset'):
    match=re.search(r'  repertoire:\n((?:  - .*\n)+)',path.read_text(encoding='utf-8-sig'))
    if match:guids.update(re.findall('guid: ([a-f0-9]+)',match[1]))
count=0
for meta in (root/'Assets/Data/Weapons').rglob('*.asset.meta'):
    if not any(g in meta.read_text() for g in guids):continue
    path=meta.with_suffix('');source=path.read_text(encoding='utf-8-sig')
    if '  masteryModifiers:' in source:continue
    passive=re.search(r'^  passive: ([1-9])',source,re.M)!=None
    power=passive or any('class: '+action in source for action in ['MeleeAction','ProjectileAction','PoisonArrowAction','RepeatedStrikeAction','GroundAreaAction','TrapAction','StepAction','TwoTimesAction'])
    modifiers=[]
    if power:modifiers.append(('power','Potencia','Cada rango mejora un 5% el daño de la habilidad o la magnitud de su efecto.','0.05','0'))
    if not passive:modifiers.append(('recovery','Recuperación','Cada rango reduce un 5% la recarga de esta habilidad.','0','0.05'))
    fields='  masteryUsesRequired: 25\n  masteryModifiers:\n'
    for mid,title,description,damage,cooldown in modifiers:
        fields+=f'  - id: {mid}\n    displayName: {json.dumps(title,ensure_ascii=False)}\n    description: {json.dumps(description,ensure_ascii=False)}\n    maxLevel: 3\n    effectiveUsesPerLevel: 10\n    damagePerLevel: {damage}\n    cooldownReductionPerLevel: {cooldown}\n'
    source=source.replace('  displayName:',fields+'  displayName:',1)
    path.write_text(source,encoding='utf-8');count+=1
print('Configured optional modifiers for',count,'skills')
for path in (root/'Assets/Scripts').rglob('*.cs'):
    meta=Path(str(path)+'.meta')
    if not meta.exists() and path.name in ['PlayerInventoryCombatProgress.cs','ProgressionImprovementsChecks.cs']:
        meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n',encoding='utf-8')
