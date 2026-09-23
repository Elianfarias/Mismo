using System;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.Equipment.Inventory;
using Mismo.Gameplay.Player.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Mismo.Gameplay.Player.Presentation
{
    /// <summary>Providers offer actions during Update; exactly one is selected and executed in LateUpdate.</summary>
    [DefaultExecutionOrder(50)]
    public sealed class PlayerInteraction : MonoBehaviour
    {
        public const Key InteractionKey = Key.F;
        PlayerInventory inventory;
        Health health;
        int frame = -1;
        int consumedFrame = -1;
        float score;
        UnityEngine.Object candidate;
        string label, detail;
        Action action;
        public string Label { get; private set; }
        public string Detail { get; private set; }
        public bool Available => inventory != null && inventory.CanInteract &&
            (health == null || !health.IsDead) && !GameplayPause.BlocksInput && !InventoryPanel.AnyOpen &&
            !WorldMapPanel.BlocksGameplay && GetComponent<GatheringPlayer>()?.Busy != true && !CompanionPlayer.IsRiding(gameObject);

        void Awake() { inventory = GetComponent<PlayerInventory>(); health = GetComponent<Health>(); }
        public static void Offer(PlayerInventory owner, UnityEngine.Object source, Vector3 position, string label, string detail, Action action)
        {
            if (owner == null || source == null) return;
            var selector = owner.GetComponent<PlayerInteraction>();
            if (selector == null || !selector.Available || selector.consumedFrame == Time.frameCount) return;
            if (selector.frame != Time.frameCount)
            { selector.frame = Time.frameCount; selector.score = float.MaxValue; selector.candidate = null; }
            float score = (position - owner.transform.position).sqrMagnitude;
            if (score > selector.score || score == selector.score && selector.candidate != null && source.GetHashCode() >= selector.candidate.GetHashCode()) return;
            selector.score = score; selector.candidate = source; selector.label = label; selector.detail = detail; selector.action = action;
        }
        void LateUpdate() => Resolve(Keyboard.current?[InteractionKey].wasPressedThisFrame == true);
        void Resolve(bool pressed)
        {
            Label = Detail = null;
            if (frame != Time.frameCount || candidate == null || !Available) return;
            Label = label; Detail = detail;
            if (!pressed) return;
            var selected = action;
            consumedFrame = Time.frameCount;
            action = null; candidate = null; Label = Detail = null;
            selected?.Invoke();
        }
        void OnDisable() { frame = consumedFrame = -1; candidate = null; action = null; Label = Detail = null; }
    }
}
