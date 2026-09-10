using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Mismo.Gameplay.Player.Equipment.Inventory
{
    [DefaultExecutionOrder(-100)]
    public sealed class InventoryPanel : MonoBehaviour
    {
        PlayerInventory inventory;
        EquipmentLoadout loadout;
        Health health;
        string selected, message;
        float messageUntil;
        Vector2 scroll;
        bool showProgression;
        GUIStyle label, button;
        CursorLockMode previousLock;
        bool previousVisible;
        int closedFrame = -1;
        static InventoryPanel active;
        public static bool AnyOpen => active != null && active.IsOpen;
        public bool IsOpen { get; private set; }
        public bool BlocksGameplay => IsOpen || closedFrame == Time.frameCount;

        void Awake()
        {
            inventory = GetComponent<PlayerInventory>(); loadout = GetComponent<EquipmentLoadout>(); health = GetComponent<Health>();
            inventory.Changed += OnChanged;
            OnChanged();
        }
        void OnChanged() { message = inventory.Notice; messageUntil = Time.unscaledTime + 5f; }
        void Update()
        {
            if (IsOpen && (health == null || health.IsDead)) Close();
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.iKey.wasPressedThisFrame) { if (IsOpen) Close(); else TryOpen(); }
            else if (IsOpen && keyboard.escapeKey.wasPressedThisFrame) Close();
        }

        public bool TryOpen()
        {
            if(WorldMapPanel.BlocksGameplay)return false;
            if(WorldMapPanel.BlocksGameplay)return false;
            if (IsOpen) return true;
            if (!inventory.IsReady || !loadout.CanSwap || !Mathf.Approximately(Time.timeScale, 1f))
            { message = "Terminá tu acción para abrir el inventario."; messageUntil = Time.unscaledTime + 3f; return false; }
            previousLock = Cursor.lockState; previousVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            if (selected == null) selected = inventory.EquippedId(loadout.ActiveSlot);
            IsOpen = true;
            active = this;
            return true;
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false; closedFrame = Time.frameCount;
            if (active == this) active = null;
            Cursor.lockState = previousLock; Cursor.visible = previousVisible;
        }
        void OnDisable() => Close();
        void OnDestroy() { if (inventory != null) inventory.Changed -= OnChanged; }

        void Text(Rect rect, string value, int size = 18, Color? color = null)
        {
            label.fontSize = size; label.normal.textColor = color ?? Color.white;
            GUI.Label(rect, value, label);
        }
        bool Button(Rect rect, string value) => GUI.Button(rect, value, button);

        void OnGUI()
        {
            if (inventory == null || health == null || health.IsDead) return;
            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label) { wordWrap = true, padding = new RectOffset(0, 0, 0, 0) };
                button = new GUIStyle(GUI.skin.button) { fontSize = 18, wordWrap = true, padding = new RectOffset(12, 12, 8, 8) };
            }
            Matrix4x4 previous = GUI.matrix;
            float scale = PlayerHUD.Scale, width = Screen.width / scale, height = Screen.height / scale;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            int depth = GUI.depth; GUI.depth = -30;
            if (!IsOpen)
            {
                Text(new Rect(25, height - 48, 480, 32), "[I] INVENTARIO · NIVEL "+inventory.Level, 17, PlayerHUD.Gold);
                if (Time.unscaledTime < messageUntil)
                {
                    PlayerHUD.Fill(new Rect(width / 2 - 360, 32, 720, 72), PlayerHUD.Panel);
                    Text(new Rect(width / 2 - 342, 44, 684, 55), message, 18, inventory.HasSaveProblem ? new Color(1, .65f, .4f) : PlayerHUD.Gold);
                }
            }
            else
            {
                PlayerHUD.Fill(new Rect(0, 0, width, height), new Color(.015f, .025f, .04f, .82f));
                float x = (width - 1120) / 2, y = (height - 690) / 2;
                PlayerHUD.Fill(new Rect(x, y, 1120, 690), PlayerHUD.Panel);
                PlayerHUD.Fill(new Rect(x, y, 1120, 3), PlayerHUD.Gold);
                Text(new Rect(x + 30, y + 25, 700, 42), "INVENTARIO Y PROGRESIÓN", 30, PlayerHUD.Gold);
                if(Button(new Rect(x+30,y+76,180,36),"Armas"))showProgression=false;
                if(Button(new Rect(x+225,y+76,240,36),"Niveles y maestría"))showProgression=true;
                if (Button(new Rect(x + 890, y + 25, 200, 46), "Cerrar  [I / ESC]")) Close();
                if(showProgression)DrawProgression(x,y);
                else
                {
                Text(new Rect(x + 30, y + 124, 380, 30), "ARMAS OBTENIDAS  ·  " + inventory.Count, 16, PlayerHUD.Gold);
                scroll = GUI.BeginScrollView(new Rect(x + 30, y + 163, 390, 406), scroll, new Rect(0, 0, 364, Mathf.Max(400, inventory.Count * 86)));
                for (int i = 0; i < inventory.Count; i++)
                {
                    string id = inventory.ItemId(i);
                    var definition = inventory.Definition(id);
                    bool equipped = inventory.EquippedId(0) == id || inventory.EquippedId(1) == id;
                    Color oldColor = GUI.backgroundColor;
                    GUI.backgroundColor = id == selected ? PlayerHUD.Gold : new Color(.35f, .43f, .48f);
                    if (Button(new Rect(0, i * 86, 358, 74), inventory.ItemName(id) + (equipped ? "\nEQUIPADA" : "\nDISPONIBLE"))) selected = id;
                    GUI.backgroundColor = oldColor;
                }
                GUI.EndScrollView();
                var weapon = inventory.Definition(selected);
                if (weapon != null)
                {
                    float dx = x + 458;
                    Text(new Rect(dx, y + 124, 625, 42), weapon.DisplayName, 27);
                    Text(new Rect(dx, y + 170, 625, 42), string.IsNullOrEmpty(weapon.inventoryDescription)
                        ? (weapon.isBow ? "Arco · Preparación, distancia y disparos cargados." : "Espada · Combos, parry y ataques de proximidad.")
                        : weapon.inventoryDescription, 17, PlayerHUD.Muted);
                    var item=inventory.Item(selected);var bonuses=inventory.Rules.Bonuses(item);
                    Text(new Rect(dx,y+217,625,43),"T"+item.tier+" "+PlayerInventory.VariantName(item.variant)+
                        " · Daño "+Signed(bonuses.damage*100)+"% · Vel. "+Signed(bonuses.speed*100)+
                        "% · Vida "+Signed(bonuses.life)+" · Arm. "+Signed(bonuses.armor),15,PlayerHUD.Gold);
                    string[] keys = { "CLICK", "Q", "E", "R" };
                    for (int i = 0; i < 4; i++)
                    {
                        var ability = weapon.GetAbility((AbilitySlot)i);
                        if (ability == null) continue;
                        Text(new Rect(dx, y + 272 + i * 39, 625, 32), keys[i] + "   " + ability.displayName + "   ·   " + ability.cooldown.ToString("0.##") + " s", 18);
                    }
                    for (int slot = 0; slot < 2; slot++)
                    {
                        float sy = y + 429 + slot * 73;
                        Text(new Rect(dx, sy, 344, 26), "RANURA " + (slot + 1) + (loadout.ActiveSlot == slot ? " · ACTIVA" : " · SECUNDARIA"), 14, PlayerHUD.Gold);
                        Text(new Rect(dx, sy + 27, 344, 38), inventory.Definition(inventory.EquippedId(slot)).DisplayName, 19);
                        bool same = inventory.EquippedId(slot) == selected;
                        bool enabled = GUI.enabled;
                        GUI.enabled = !same && loadout.CanChangeEquipment;
                        if (Button(new Rect(dx + 354, sy + 4, 270, 53), same ? "Equipada" : "Equipar en ranura " + (slot + 1))) inventory.TryEquip(slot, selected);
                        GUI.enabled = enabled;
                    }
                }
                }
                Text(new Rect(x + 30, y + 601, 1060, 33), loadout.InCombat
                    ? "EN COMBATE · Podés consultar tus armas; equipalas cuando termine el combate."
                    : "TAB alterna las armas durante el juego. El inventario no pausa el mundo.", 17, PlayerHUD.Muted);
                Text(new Rect(x + 30, y + 640, 1060, 43), inventory.Notice, 16, inventory.HasSaveProblem ? new Color(1, .65f, .4f) : PlayerHUD.Gold);
            }
            GUI.depth = depth; GUI.matrix = previous;
        }
        static string Signed(float value)=>value.ToString("+0.#;-0.#;0");
        void DrawProgression(float x,float y)
        {
            var p=inventory.Progression;var rules=inventory.Rules;
            Text(new Rect(x+30,y+133,500,38),"PERSONAJE · NIVEL "+p.level,25,PlayerHUD.Gold);
            Text(new Rect(x+30,y+180,500,30),p.level>=rules.characterMaxLevel?"Nivel máximo":
                "Experiencia "+p.experience+" / "+rules.Needed(p.level),19);
            Text(new Rect(x+30,y+217,500,34),"Puntos disponibles: "+p.Available,20);
            string[] names={"Vida máxima", "Ataque", "Armadura"};
            int[] points={p.lifePoints,p.attackPoints,p.armorPoints};
            string[] increases={"+"+rules.lifePerPoint+" vida", "+"+(rules.attackPerPoint*100).ToString("0.#")+"% daño", "+"+rules.armorPerPoint+" armadura"};
            for(int i=0;i<3;i++)
            {
                float row=y+274+i*75;
                Text(new Rect(x+30,row,310,29),names[i]+" · "+points[i]+" puntos",19);
                Text(new Rect(x+30,row+29,300,27),increases[i]+" por punto",16,PlayerHUD.Muted);
                bool old=GUI.enabled;GUI.enabled=loadout.CanChangeEquipment&&p.Available>0;
                if(Button(new Rect(x+351,row,150,46),"Mejorar +"))inventory.TrySpend((CharacterAttribute)i);
                GUI.enabled=old;
            }
            Text(new Rect(x+30,y+514,500,60),"Vida máxima: "+health.Maximum.ToString("0.#")+
                " · Armadura: "+inventory.Armor.ToString("0.#")+"\nLos bonos de ambas armas están activos.",17,PlayerHUD.Muted);
            float right=x+580;
            Text(new Rect(right,y+133,500,38),"MAESTRÍA DE ARMA",25,PlayerHUD.Gold);
            var weapon=inventory.Definition(selected)??loadout.ActiveDefinition;
            if(weapon==null)return;
            var mastery=inventory.Mastery(weapon);
            Text(new Rect(right,y+180,500,60),weapon.DisplayName+"\nFamilia: "+(weapon.family!=null?weapon.family.DisplayName:weapon.DisplayName),18);
            Text(new Rect(right,y+249,500,54),"Nivel "+mastery.level+" · Puntos: "+mastery.Available+"\n"+
                (mastery.level>=rules.masteryMaxLevel?"Maestría máxima":"Experiencia "+mastery.experience+" / "+rules.Needed(mastery.level,true)),19);
            for(int i=0;i<2;i++)
            {
                float row=y+326+i*75;
                float increase=i==0?rules.masteryDamagePerPoint:rules.masterySpeedPerPoint;
                Text(new Rect(right,row,300,29),(i==0?"Daño · "+mastery.damagePoints:"Velocidad · "+mastery.speedPoints)+" puntos",19);
                Text(new Rect(right,row+29,300,27),"+"+(increase*100).ToString("0.#")+"% por punto",16,PlayerHUD.Muted);
                bool old=GUI.enabled;GUI.enabled=loadout.CanChangeEquipment&&mastery.Available>0;
                if(Button(new Rect(right+334,row,160,46),"Mejorar +"))inventory.TrySpendMastery(weapon,(MasteryAttribute)i);
                GUI.enabled=old;
            }
            Text(new Rect(right,y+493,500,88),"Elegí otra arma en la pestaña Armas para ver su maestría.\nEl progreso se comparte entre ejemplares de la misma familia. El alcance no aumenta.",17,PlayerHUD.Muted);
        }
    }
}
