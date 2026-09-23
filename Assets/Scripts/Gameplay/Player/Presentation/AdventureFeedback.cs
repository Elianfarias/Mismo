using System.Collections.Generic;
using Mismo.Core;
using Mismo.Gameplay.Player.Equipment.Inventory;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using L = Mismo.Gameplay.Player.Localization.GameLanguage;

namespace Mismo.Gameplay.Player.Presentation
{
    /// <summary>Scene-local HUD: one interaction, grouped XP and queued progression celebrations.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class AdventureFeedback : MonoBehaviour
    {
        sealed class Card
        {
            public RectTransform root;
            public CanvasGroup group;
            public TextMeshProUGUI title, body;
            public MMF_Player feel;
            public float age, duration;
        }
        struct Celebration { public string title, body; public bool level; }
        readonly Queue<Celebration> celebrations = new Queue<Celebration>();
        readonly Queue<string> notices = new Queue<string>();
        PlayerInventory inventory;
        PlayerInteraction interaction;
        Canvas canvas;
        TMP_FontAsset font;
        Card prompt, experience, celebration, notice;
        Image progressFill;
        long groupedExperience;
        float lastExperience = -10, displayedProgress, targetProgress;
        bool suspended;
        string promptLabel, lastNotice;
        string lastQueuedNotice;
        int noticeFrame = -1;

        void Awake()
        {
            ProjectTextSettings.Initialize();
            inventory = GetComponent<PlayerInventory>(); interaction = GetComponent<PlayerInteraction>();
            font = ProjectAssets.Load<TMP_FontAsset>("UI/AdventureFont");
            if (font == null) { Debug.LogError("Falta UI/AdventureFont en el catálogo.", this); enabled = false; return; }
            var root = new GameObject("Adventure HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 15;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 800); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            prompt = CreateCard("Interaction", new Vector2(.5f, 0), new Vector2(0, 180), new Vector2(400, 74), false);
            var key = Box(prompt.root, "Keycap", new Vector2(-162, 0), new Vector2(42, 42), QuietFantasyUI.Amber);
            Text(key.rectTransform, "F", Vector2.zero, new Vector2(40, 40), 25, new Color(.07f,.09f,.1f));
            prompt.title.rectTransform.anchoredPosition = new Vector2(24, 10); prompt.title.rectTransform.sizeDelta = new Vector2(306, 30);
            prompt.body.rectTransform.anchoredPosition = new Vector2(24, -17); prompt.body.rectTransform.sizeDelta = new Vector2(306, 24);
            experience = CreateCard("Experience", new Vector2(.5f, 0), new Vector2(0, 278), new Vector2(292, 70), true, 2.1f);
            Box(experience.root, "Progress track", new Vector2(0,-29),new Vector2(260,2),new Color(.4f,.42f,.4f,.35f));
            progressFill = Box(experience.root, "Progress", new Vector2(0, -29), new Vector2(260, 2), QuietFantasyUI.Amber);
            progressFill.rectTransform.pivot = new Vector2(0,.5f); progressFill.rectTransform.anchoredPosition = new Vector2(-130,-29);
            celebration = CreateCard("Level and mastery", new Vector2(.5f, 1), new Vector2(0, -155), new Vector2(510, 112), true, 3.4f);
            celebration.title.fontSize = 33; celebration.title.fontSizeMax = 33; celebration.title.fontSizeMin = 26; celebration.title.rectTransform.anchoredPosition = new Vector2(0, 17);
            celebration.title.rectTransform.sizeDelta = new Vector2(478,44);
            celebration.body.rectTransform.anchoredPosition = new Vector2(0,-24);
            notice = CreateCard("Rewards", new Vector2(1,.5f), new Vector2(-215, -30), new Vector2(380, 96), true, 3.5f);
            notice.title.gameObject.SetActive(false); notice.title.fontSize = 15; notice.title.fontSizeMax = 15; notice.title.fontSizeMin = 12;
            notice.body.rectTransform.sizeDelta = new Vector2(344,52); notice.body.rectTransform.anchoredPosition = Vector2.zero;
            lastNotice = inventory.Notice;
            inventory.Committed += Committed; inventory.Changed += Changed;
        }

        Card CreateCard(string name, Vector2 anchor, Vector2 position, Vector2 size, bool animated, float duration = 0)
        {
            var background = Box(canvas.transform, name, position, size, new Color(.035f,.055f,.065f,.93f));
            var rect = background.rectTransform; rect.anchorMin = rect.anchorMax = anchor;
            var card = new Card { root = rect, group = rect.gameObject.AddComponent<CanvasGroup>(), duration = duration, age = duration };
            card.group.alpha = 0; card.group.blocksRaycasts = false; card.group.interactable = false;
            Box(rect, "Top rule", new Vector2(0,size.y/2), new Vector2(size.x,1), new Color(.72f,.57f,.31f,.65f));
            Box(rect, "Bottom rule", new Vector2(0,-size.y/2), new Vector2(size.x,1), new Color(.72f,.57f,.31f,.24f));
            foreach(float side in new[]{-1f,1f})
            {
                Box(rect,"Corner",new Vector2(side*(size.x/2-1),size.y/2-6),new Vector2(2,12),new Color(.82f,.67f,.39f,.85f));
                Box(rect,"Corner",new Vector2(side*(size.x/2-1),-size.y/2+6),new Vector2(2,12),new Color(.82f,.67f,.39f,.45f));
            }
            card.title = Text(rect, "", new Vector2(0,12), new Vector2(size.x-30,32), 23, QuietFantasyUI.Amber);
            card.body = Text(rect, "", new Vector2(0,-17), new Vector2(size.x-30,28), 18, QuietFantasyUI.Ink);
            if (animated)
            {
                card.feel = rect.gameObject.AddComponent<MMF_Player>();
                card.feel.InitializationMode = MMFeedbacks.InitializationModes.Script;
                card.feel.ForceTimescaleMode = true;
                card.feel.PlayerTimescaleMode = TimescaleModes.Unscaled;
                card.feel.AddFeedback(new MMF_CanvasGroup { TargetCanvasGroup = card.group, Duration = duration,
                    AlphaCurve = new MMTweenType(new AnimationCurve(new Keyframe(0,0), new Keyframe(.07f,1), new Keyframe(.8f,1), new Keyframe(1,0))) });
                card.feel.Initialization();
            }
            return card;
        }
        static Image Box(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent,false); var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false;
            image.rectTransform.sizeDelta = size; image.rectTransform.anchoredPosition = position; return image;
        }
        TextMeshProUGUI Text(Transform parent, string value, Vector2 position, Vector2 size, float fontSize, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer)); go.transform.SetParent(parent,false);
            var text = go.AddComponent<TextMeshProUGUI>(); text.font = font; text.text = value; text.fontSize = fontSize;
            text.color = color; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false; text.richText = false;
            text.overflowMode = TextOverflowModes.Ellipsis; text.textWrappingMode = TextWrappingModes.Normal;
            text.enableAutoSizing = true; text.fontSizeMin = fontSize*.75f; text.fontSizeMax = fontSize;
            text.rectTransform.sizeDelta = size; text.rectTransform.anchoredPosition = position; return text;
        }
        void Play(Card card)
        {
            card.feel.StopFeedbacks(); card.age = 0; card.group.alpha = 0;
            card.feel.PlayFeedbacks();
            if (suspended) { card.feel.TimescaleMultiplier = 0; card.feel.PauseFeedbacks(); }
        }
        public static long ExperienceDelta(ProgressionData before, ProgressionData after, ProgressionRules rules)
        {
            long result = (long)after.experience-before.experience;
            for (int level=before.level; level<after.level; level++) result += rules.Needed(level);
            return System.Math.Max(0,result);
        }
        void Committed(ProgressionData before, ProgressionData after, string message)
        {
            long gained = ExperienceDelta(before, after, inventory.Rules);
            if (gained > 0)
            {
                if (Time.unscaledTime-lastExperience > .8f) groupedExperience = 0;
                groupedExperience += gained; lastExperience = Time.unscaledTime;
                experience.title.text = L.Format("+{0} EXP", groupedExperience);
                experience.body.text = after.level >= inventory.Rules.characterMaxLevel ? L.Text("Nivel máximo") : L.Format("Nivel {0} · {1} / {2}", after.level, after.experience, inventory.Rules.Needed(after.level));
                displayedProgress = before.level == after.level ? (float)before.experience/inventory.Rules.Needed(before.level) : 0;
                targetProgress = after.level >= inventory.Rules.characterMaxLevel ? 1 : (float)after.experience/inventory.Rules.Needed(after.level);
                Play(experience);
            }
            if (after.level > before.level)
                celebrations.Enqueue(new Celebration { title = "¡Subiste de nivel!", body = L.Format(after.level-before.level==1 ? "Nivel {0} · +{1} punto de atributo" : "Nivel {0} · +{1} puntos de atributo", after.level, after.level-before.level), level = true });
            foreach (var mastery in after.masteries)
            {
                int previous = before.Find(mastery.familyId)?.level ?? 1;
                if (mastery.level <= previous) continue;
                celebrations.Enqueue(new Celebration { title = "Maestría mejorada", body = L.Format(mastery.level-previous==1 ? "{0} · Nivel {1} · +{2} punto" : "{0} · Nivel {1} · +{2} puntos", L.Text(inventory.MasteryDisplayName(mastery.familyId)), mastery.level, mastery.level-previous) });
            }
            // At most 24 celebrations can be pending; repeated rewards cannot grow the HUD without bound.
            while (celebrations.Count > 24) celebrations.Dequeue();
            if (!string.IsNullOrWhiteSpace(message)) ShowNotice(message, false);
        }
        void Changed()
        {
            if (!string.IsNullOrWhiteSpace(inventory.Notice) && (inventory.HasSaveProblem || inventory.Notice != lastNotice))
                ShowNotice(inventory.Notice, inventory.HasSaveProblem);
            lastNotice = inventory.Notice;
        }
        void ShowNotice(string message, bool error)
        {
            if (noticeFrame == Time.frameCount && lastQueuedNotice == message) return;
            noticeFrame = Time.frameCount; lastQueuedNotice = message;
            if (!error && notice.age < notice.duration)
            {
                if (notices.Count < 6) notices.Enqueue(message);
                return;
            }
            PresentNotice(message,error);
        }
        void PresentNotice(string message, bool error)
        {
            notice.title.gameObject.SetActive(error);
            notice.title.text = error ? L.Text("NO SE PUDO GUARDAR") : "";
            notice.body.rectTransform.anchoredPosition = error ? new Vector2(0,-14) : Vector2.zero;
            notice.title.color = error ? new Color(1,.57f,.35f) : QuietFantasyUI.Amber;
            notice.body.text = L.Text(message); Play(notice);
        }
        void LateUpdate()
        {
            if (canvas == null) return;
            bool blocked = GameplayPause.BlocksInput || InventoryPanel.AnyOpen || WorldMapPanel.BlocksGameplay || GetComponent<World.GatheringPlayer>()?.BlocksGameplay == true;
            if (blocked != suspended)
            {
                suspended = blocked; canvas.enabled = !blocked;
                foreach (var card in new[]{experience, celebration, notice})
                {
                    // PauseFeedbacks pauses sequencing; the multiplier also freezes the running alpha curve.
                    card.feel.TimescaleMultiplier = blocked ? 0 : 1;
                    if (blocked) card.feel.PauseFeedbacks(); else card.feel.ResumeFeedbacks();
                }
            }
            if (blocked) return;
            var gathering = GetComponent<World.GatheringPlayer>();
            bool harvesting = gathering != null && gathering.IsHarvesting;
            prompt.root.Find("Keycap").gameObject.SetActive(!harvesting);
            prompt.title.rectTransform.anchoredPosition = new Vector2(harvesting ? 0 : 24,10);
            prompt.body.rectTransform.anchoredPosition = new Vector2(harvesting ? 0 : 24,-17);
            string label = gathering != null && gathering.IsHarvesting ? gathering.HarvestLabel : interaction?.Label;
            string detail = gathering != null && gathering.IsHarvesting ? L.Format("{0:0}%", gathering.HarvestProgress*100) : interaction?.Detail;
            if (promptLabel != label) { promptLabel = label; prompt.group.alpha = 0; }
            prompt.group.alpha = Mathf.MoveTowards(prompt.group.alpha, label == null ? 0 : 1, Time.unscaledDeltaTime*9);
            prompt.title.text = L.Text(label ?? ""); prompt.body.text = L.Text(detail ?? "");
            Tick(experience); Tick(celebration); Tick(notice);
            if (notice.age >= notice.duration && notices.Count > 0) PresentNotice(notices.Dequeue(),false);
            displayedProgress = Mathf.Lerp(displayedProgress,targetProgress,1-Mathf.Exp(-Time.unscaledDeltaTime*7));
            progressFill.rectTransform.sizeDelta = new Vector2(260*Mathf.Clamp01(displayedProgress),2);
            if (celebration.age >= celebration.duration && celebrations.Count > 0)
            {
                var next = celebrations.Dequeue(); celebration.title.text = L.Text(next.title); celebration.body.text = next.body;
                celebration.title.color = next.level ? QuietFantasyUI.Amber : new Color(.6f,.83f,.8f);
                Play(celebration);
            }
        }
        static void Tick(Card card)
        {
            card.age += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(card.age/.35f);
            card.root.localScale = Vector3.one*(1 + .055f*Mathf.Sin(t*Mathf.PI)*(1-t));
        }
        void OnDestroy()
        {
            if (inventory != null) { inventory.Committed -= Committed; inventory.Changed -= Changed; }
            if (canvas != null) { if (Application.isPlaying) Destroy(canvas.gameObject); else DestroyImmediate(canvas.gameObject); }
        }
    }
}
