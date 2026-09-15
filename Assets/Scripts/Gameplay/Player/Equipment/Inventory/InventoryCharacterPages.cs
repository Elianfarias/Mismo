using UnityEngine;
using Mismo.Gameplay.Player.Presentation;
using U = Mismo.Gameplay.Player.Presentation.QuietFantasyUI;
using L = Mismo.Gameplay.Player.Localization.GameLanguage;

namespace Mismo.Gameplay.Player.Equipment.Inventory
{
    public sealed partial class InventoryPanel
    {
        int selectedAttribute,selectedMastery,selectedAbilityIndex;
        int skillDragIndex=-1,skillDragControl;
        bool skillDragging;
        Vector2 skillDragOrigin,detailScroll;
        WeaponDefinition skillDragWeapon;
        bool showUICredits;
        void OnApplicationFocus(bool focused){if(!focused)CancelSkillDrag();}
        bool IsQuietPage=>page==Page.Character||page==Page.Weapons||page==Page.Skills;
        WeaponDefinition CurrentMenuWeapon=>skillsWeapon==loadout.GetSlot(0)||skillsWeapon==loadout.GetSlot(1)?skillsWeapon??loadout.ActiveDefinition:loadout.ActiveDefinition;
        void CancelSkillDrag()
        {
            if(skillDragControl!=0&&GUIUtility.hotControl==skillDragControl)GUIUtility.hotControl=0;
            skillDragIndex=-1;skillDragging=false;skillDragWeapon=null;skillDragControl=0;
        }
        void QuietLine(float x,float y,float width)=>PlayerHUD.Fill(new Rect(x,y,width,1),U.Rule);
        void DrawQuietInterface()
        {
            U.Text(new Rect(55,27,330,30),"MISMO / VIAJERO",16,U.Muted,true);
            if(U.Button(new Rect(801,20,140,35),"Créditos de UI"))showUICredits=!showUICredits;
            if(U.Button(new Rect(974,20,110,35),"Menú [B]"))OpenPage(Page.Menu);
            if(U.Button(new Rect(1094,20,135,35),"Cerrar [ESC]"))Close();
            if(U.Button(new Rect(367,70,172,49),"Personaje",page==Page.Character,true))OpenPage(Page.Character);
            if(U.Button(new Rect(554,70,172,49),"Armas",page==Page.Weapons,true))OpenPage(Page.Weapons);
            if(U.Button(new Rect(741,70,172,49),"Habilidades",page==Page.Skills,true))OpenPage(Page.Skills);
            QuietLine(55,122,1170);
            if(!showUICredits)
            {
                if(page==Page.Character)DrawCharacterSheet();
                else if(page==Page.Weapons)DrawWeaponSheet();
                else if(page==Page.Skills)DrawSkillCollection();
            }
            U.Text(new Rect(55,761,1168,30),inventory.Notice,16,inventory.HasSaveProblem?new Color(1,.6f,.3f):U.Muted);
            if(showUICredits)
            {
                CancelSkillDrag();
                PlayerHUD.Fill(new Rect(30,133,1220,617),new Color(.035f,.043f,.038f,.99f));
                U.Text(new Rect(95,208,1080,55),"CRÉDITOS DE LA INTERFAZ",30,null,true);
                U.Text(new Rect(95,288,1080,360),"Iconos: Lorc y Skoll · Game-icons.net\nCC BY 3.0 · creativecommons.org/licenses/by/3.0\nAdaptación: escala y tinte marfil.\n\nCinzel: The Cinzel Project Authors · SIL OFL 1.1\nSource Sans 3: Adobe · SIL OFL 1.1\n\nFantasy UI Borders: Kenney · CC0\nLas licencias y fuentes originales se incluyen con el juego.",23,U.Ink);
                if(U.Button(new Rect(95,681,240,44),"Volver"))showUICredits=false;
            }
        }
        void DrawCharacterSheet()
        {
            var p=inventory.Progression;var rules=inventory.Rules;
            U.Text(new Rect(65,165,320,45),"ATRIBUTOS",28,null,true);
            U.Text(new Rect(65,208,320,32),L.Format("Puntos disponibles: {0}",p.Available),20,U.Muted);
            string[] names={"Vida","Ataque","Armadura"},icons={"heart-inside","broadsword","checked-shield"};
            string[] bonus={L.Format("+{0:0.#}",rules.lifePerPoint),L.Format("+{0:0.#}%",rules.attackPerPoint*100),L.Format("+{0:0.#}",rules.armorPerPoint)};
            int[] spent={p.lifePoints,p.attackPoints,p.armorPoints};
            for(int i=0;i<3;i++)
            {
                float y=270+i*111;
                var row=new Rect(57,y-7,342,100);
                if(selectedAttribute==i)PlayerHUD.Fill(row,new Color(.66f,.57f,.35f,.09f));
                U.DrawIcon(new Rect(68,y+15,39,39),icons[i]);
                if(U.Button(new Rect(118,y+8,156,42),names[i],selectedAttribute==i))selectedAttribute=i;
                U.Text(new Rect(279,y+13,78,35),bonus[i],24,U.Ink);
                U.Text(new Rect(121,y+55,240,30),L.Format("Puntos invertidos: {0}",spent[i]),17,U.Muted);
                QuietLine(67,y+97,322);
            }
            string[] descriptions={L.Format("+{0:0.#} de vida máxima",rules.lifePerPoint),L.Format("+{0:0.#}% al daño del arma",rules.attackPerPoint*100),L.Format("+{0:0.#} de armadura",rules.armorPerPoint)};
            U.Text(new Rect(66,618,329,49),descriptions[selectedAttribute],20,U.Amber);
            bool was=GUI.enabled;GUI.enabled=was&&loadout.CanChangeEquipment&&p.Available>0;
            if(U.Button(new Rect(65,677,270,43),"Mejorar · 1 punto"))inventory.TrySpend((CharacterAttribute)selectedAttribute);
            GUI.enabled=was;
            PlayerHUD.Fill(new Rect(422,180,1,525),U.Rule);
            U.Text(new Rect(456,165,369,38),L.Format("Nivel {0}",p.level),24,U.Ink,true,TextAnchor.MiddleCenter);
            U.Text(new Rect(456,207,369,29),p.level>=rules.characterMaxLevel?"Nivel máximo":L.Format("{0} / {1} EXP",p.experience,rules.Needed(p.level)),17,U.Muted,false,TextAnchor.MiddleCenter);
            DrawPreview(new Rect(445,252,391,459));
            PlayerHUD.Fill(new Rect(855,180,1,525),U.Rule);
            U.Text(new Rect(894,165,300,45),"TOTALES",28,null,true);
            DrawTotal(894,263,"heart-inside","Vida máxima",health.Maximum.ToString("0.#",L.Culture));
            DrawTotal(894,403,"broadsword","Ataque total",L.Format("{0:0.#}%",inventory.DamageMultiplier(loadout.ActiveDefinition)*100));
            DrawTotal(894,543,"checked-shield","Armadura total",inventory.Armor.ToString("0.#",L.Culture));
            U.Text(new Rect(894,678,309,67),"Incluye atributos y equipo.\nAtaque: 100% = daño base del arma activa.",17,U.Muted);
            if(!loadout.CanChangeEquipment)U.Text(new Rect(65,728,720,29),"Salí de combate para mejorar atributos.",17,U.Amber);
        }
        void DrawTotal(float x,float y,string icon,string name,string value)
        {
            U.DrawIcon(new Rect(x,y+4,39,39),icon);
            U.Text(new Rect(x+58,y,250,31),name,21,U.Muted);
            U.Text(new Rect(x+58,y+35,250,65),value,43,null,true);
            QuietLine(x,y+113,307);
        }
        void DrawWeaponTabs()
        {
            for(int i=0;i<2;i++)
            {
                var weapon=loadout.GetSlot(i);if(weapon==null)continue;
                if(U.Button(new Rect(65+i*287,146,268,44),weapon.DisplayName,CurrentMenuWeapon==weapon))
                {skillsWeapon=weapon;selectedAbilityIndex=0;skillsScroll=Vector2.zero;detailScroll=Vector2.zero;previewDirty=true;CancelSkillDrag();}
            }
        }
        void DrawWeaponSheet()
        {
            DrawWeaponTabs();var weapon=CurrentMenuWeapon;if(weapon==null)return;
            var mastery=inventory.Mastery(weapon);if(mastery==null)return;
            var rules=inventory.Rules;
            U.Text(new Rect(65,234,460,45),weapon.DisplayName,29,null,true);
            U.Text(new Rect(65,283,460,48),weapon.family!=null?weapon.family.DisplayName:weapon.DisplayName,20,U.Muted);
            DrawPreview(new Rect(80,329,455,340));
            if(U.Button(new Rect(115,689,330,40),"Ver equipo en inventario"))OpenPage(Page.Inventory);
            PlayerHUD.Fill(new Rect(595,228,1,482),U.Rule);
            U.Text(new Rect(649,235,540,45),"MAESTRÍA DEL ARMA",29,null,true);
            U.Text(new Rect(649,293,510,37),L.Format("Nivel {0} · Puntos disponibles: {1}",mastery.level,mastery.Available),23);
            PlayerHUD.Fill(new Rect(650,345,529,3),U.Rule);
            float progress=mastery.level>=rules.masteryMaxLevel?1:(float)mastery.experience/Mathf.Max(1,rules.Needed(mastery.level,true));
            PlayerHUD.Fill(new Rect(650,345,529*Mathf.Clamp01(progress),3),U.Amber);
            U.Text(new Rect(650,360,520,30),mastery.level>=rules.masteryMaxLevel?"Maestría máxima":L.Format("{0} / {1} EXP",mastery.experience,rules.Needed(mastery.level,true)),18,U.Muted);
            for(int i=0;i<2;i++)
            {
                float y=414+i*91;
                U.DrawIcon(new Rect(650,y+6,38,38),i==0?"broadsword":"wingfoot");
                if(U.Button(new Rect(705,y,182,44),i==0?"Daño":"Velocidad",selectedMastery==i))selectedMastery=i;
                U.Text(new Rect(924,y+7,255,32),L.Format("+{0:0.#}% por punto",100*(i==0?rules.masteryDamagePerPoint:rules.masterySpeedPerPoint)),22,U.Ink);
                U.Text(new Rect(705,y+45,450,27),L.Format("Puntos invertidos: {0}",i==0?mastery.damagePoints:mastery.speedPoints),17,U.Muted);
                QuietLine(650,y+78,529);
            }
            bool was=GUI.enabled;GUI.enabled=was&&loadout.CanChangeEquipment&&mastery.Available>0;
            if(U.Button(new Rect(650,613,345,45),"Mejorar · 1 punto de maestría"))inventory.TrySpendMastery(weapon,(MasteryAttribute)selectedMastery);
            GUI.enabled=was;
            if(U.Button(new Rect(650,684,250,43),"Ver habilidades")){OpenPage(Page.Skills);skillsWeapon=weapon;}
            U.Text(new Rect(950,680,252,63),"Progreso compartido por familia de arma.",17,U.Muted);
            if(!loadout.CanChangeEquipment)U.Text(new Rect(650,734,550,26),"Salí de combate para mejorar el arma.",17,U.Amber);
        }
        AbilityDefinition MenuAbility(WeaponDefinition weapon,int index)=>weapon.family!=null&&!weapon.overrideFamilyAbilities?weapon.family.Skill(index):weapon.GetAbility((AbilitySlot)(index+1));
        int MenuUnlock(WeaponDefinition weapon,int index)=>weapon.family!=null&&!weapon.overrideFamilyAbilities?weapon.family.UnlockLevel(index):1;
        bool CanDragSkill(WeaponDefinition weapon,int index)=>weapon!=null&&weapon.family!=null&&!weapon.overrideFamilyAbilities&&loadout.CanChangeEquipment&&MenuAbility(weapon,index)!=null&&(inventory.Mastery(weapon)?.level??1)>=MenuUnlock(weapon,index);
        void StartSkillDrag(WeaponDefinition weapon,int index,int control)
        {
            if(!CanDragSkill(weapon,index))return;
            skillDragIndex=index;skillDragWeapon=weapon;skillDragControl=control;
            skillDragOrigin=GUIUtility.GUIToScreenPoint(Event.current.mousePosition);skillDragging=false;GUIUtility.hotControl=control;
        }
        void DrawSkillCollection()
        {
            DrawWeaponTabs();var weapon=CurrentMenuWeapon;if(weapon==null)return;
            int count=weapon.family!=null&&!weapon.overrideFamilyAbilities?weapon.family.SkillCount:3;
            selectedAbilityIndex=Mathf.Clamp(selectedAbilityIndex,0,Mathf.Max(0,count-1));
            int control=GUIUtility.GetControlID("MismoSkillDrag".GetHashCode(),FocusType.Passive);
            if(skillDragIndex>=0&&(!CanDragSkill(skillDragWeapon,skillDragIndex)||weapon!=skillDragWeapon))CancelSkillDrag();
            var e=Event.current;
            if(skillDragIndex>=0&&e.type==EventType.MouseDrag)
            {if(Vector2.Distance(GUIUtility.GUIToScreenPoint(e.mousePosition),skillDragOrigin)>6)skillDragging=true;e.Use();}
            int level=inventory.Mastery(weapon)?.level??1;
            U.Text(new Rect(65,207,650,29),L.Format("Maestría {0} · Colección de habilidades",level),18,U.Muted);
            var viewport=new Rect(65,254,683,321);
            skillsScroll=GUI.BeginScrollView(viewport,skillsScroll,new Rect(0,0,657,Mathf.Max(315,Mathf.CeilToInt(count/3f)*157)));
            for(int i=0;i<count;i++)
            {
                var ability=MenuAbility(weapon,i);if(ability==null)continue;
                int unlock=MenuUnlock(weapon,i);bool unlocked=level>=unlock;
                var tile=new Rect(8+i%3*216,i/3*157+3,112,110);
                bool selected=selectedAbilityIndex==i,hover=tile.Contains(e.mousePosition);
                PlayerHUD.Fill(tile,U.Surface);U.Border(tile,selected?U.Amber:hover?U.Ink:U.Rule);
                float alpha=unlocked?1:.28f;if(skillDragging&&skillDragIndex==i)alpha=.25f;
                U.DrawIcon(new Rect(tile.x+15,tile.y+15,82,80),U.AbilityIcon(ability),new Color(U.Ink.r,U.Ink.g,U.Ink.b,alpha));
                if(!unlocked)U.DrawIcon(new Rect(tile.xMax-27,tile.yMax-27,22,22),"locked-chest",U.Muted);
                if(ability.IsPassive)U.Text(new Rect(tile.x+5,tile.y+3,95,22),"PASIVA",12,U.Muted);
                U.Text(new Rect(tile.x-3,tile.yMax+6,193,36),unlocked?ability.DisplayName:L.Format("Nivel {0} · {1}",unlock,ability.DisplayName),17,unlocked?U.Ink:U.Muted);
                if(e.type==EventType.MouseDown&&e.button==0&&tile.Contains(e.mousePosition))
                {selectedAbilityIndex=i;detailScroll=Vector2.zero;StartSkillDrag(weapon,i,control);e.Use();}
            }
            GUI.EndScrollView();
            PlayerHUD.Fill(new Rect(774,254,1,315),U.Rule);
            var chosen=MenuAbility(weapon,selectedAbilityIndex);
            if(chosen!=null)
            {
                U.DrawIcon(new Rect(816,253,43,43),U.AbilityIcon(chosen),U.Amber);
                U.Text(new Rect(878,252,330,60),chosen.DisplayName,26,null,true);
                U.Text(new Rect(815,320,385,34),(chosen.IsPassive?"Pasiva":"Activa")+" · "+weapon.DisplayName,19,U.Muted);
                var style=new GUIStyle(GUI.skin.label){font=U.Body,fontSize=21,wordWrap=true};
                float height=Mathf.Max(101,style.CalcHeight(new GUIContent(chosen.Description),359));
                detailScroll=GUI.BeginScrollView(new Rect(815,368,390,112),detailScroll,new Rect(0,0,364,height));
                U.Text(new Rect(0,0,359,height),chosen.Description,21);GUI.EndScrollView();
                U.Text(new Rect(815,500,386,31),chosen.IsPassive?"Efecto activo mientras está equipada":L.Format("Recarga {0:0.#} s",chosen.cooldown),20,U.Muted);
                if(level<MenuUnlock(weapon,selectedAbilityIndex))U.Text(new Rect(815,538,390,32),L.Format("Requiere maestría {0}",MenuUnlock(weapon,selectedAbilityIndex)),19,U.Amber);
            }
            QuietLine(65,596,1140);
            U.Text(new Rect(65,611,430,30),"HABILIDADES EQUIPADAS",21,null,true);
            U.Text(new Rect(633,612,571,30),loadout.CanChangeEquipment?"Arrastrá una habilidad a Q, E o R":"Salí de combate para cambiar habilidades.",19,loadout.CanChangeEquipment?U.Muted:U.Amber,false,TextAnchor.UpperRight);
            for(int i=0;i<3;i++)
            {
                var slot=(AbilitySlot)(i+1);var equipped=inventory.SelectedAbility(weapon,slot);
                var target=new Rect(229+i*337,658,81,81);
                bool hover=target.Contains(e.mousePosition),valid=skillDragging&&CanDragSkill(weapon,skillDragIndex);
                U.Text(new Rect(target.x-56,target.y+18,43,42),new[]{"Q","E","R"}[i],25,U.Ink,true,TextAnchor.MiddleCenter);
                PlayerHUD.Fill(target,valid&&hover?new Color(.63f,.50f,.25f,.25f):U.Surface);
                U.Border(target,valid?U.Amber:U.Rule,valid&&hover?2:1);
                U.DrawIcon(new Rect(target.x+10,target.y+10,61,61),U.AbilityIcon(equipped),valid&&hover?U.Muted:U.Ink);
                U.Text(new Rect(target.x+96,target.y+15,182,58),valid&&hover?"Soltar para equipar":equipped?.DisplayName??"Vacío",18,valid&&hover?U.Amber:U.Ink);
                if(e.type==EventType.MouseDown&&e.button==0&&hover&&equipped!=null)
                {
                    int index=weapon.family?.FindSkill(equipped.Id)??-1;
                    if(index>=0){selectedAbilityIndex=index;StartSkillDrag(weapon,index,control);}e.Use();
                }
                if(skillDragging&&e.rawType==EventType.MouseUp&&e.button==0&&hover)
                {
                    if(valid)inventory.TrySelectAbility(weapon,slot,skillDragIndex);
                    CancelSkillDrag();e.Use();
                }
            }
            if(skillDragging)
            {
                var ghost=new Rect(e.mousePosition.x-31,e.mousePosition.y-31,62,62);
                PlayerHUD.Fill(ghost,new Color(.1f,.12f,.1f,.96f));U.Border(ghost,U.Amber);
                U.DrawIcon(new Rect(ghost.x+5,ghost.y+5,52,52),U.AbilityIcon(MenuAbility(skillDragWeapon,skillDragIndex)),U.Ink);
            }
            if(skillDragIndex>=0&&e.rawType==EventType.MouseUp){CancelSkillDrag();if(e.type!=EventType.Used)e.Use();}
        }
    }
}
