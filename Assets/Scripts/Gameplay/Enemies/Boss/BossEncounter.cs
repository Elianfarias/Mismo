using System;
using UnityEngine;
using UnityEngine.Events;

namespace Mismo.Gameplay.Enemies
{
    /// <summary>Coordinates victory and the region's unique inventory reward.</summary>
    [DisallowMultipleComponent]
    public sealed class BossEncounter : MonoBehaviour
    {
        [SerializeField] private BossController boss;
        [SerializeField] private GameObject lockedPassage;
        [SerializeField] private GameObject rewardIndicator;
        [SerializeField] private string defeatMessage = "BOSS DERROTADO - ACCESO DESBLOQUEADO";
        [SerializeField] private UnityEvent onBossDefeated = new UnityEvent();

        private bool completed;

        public BossController Boss => boss;
        public bool IsCompleted => completed;
        public string DefeatMessage => defeatMessage;
        public event Action<string> Completed;

        public void Configure(BossController bossController, GameObject passage, GameObject reward)
        {
            boss = bossController;
            lockedPassage = passage;
            rewardIndicator = reward;
        }

        private void Awake()
        {
            if (boss == null) boss = FindFirstObjectByType<BossController>();
            if (lockedPassage == null) lockedPassage = GameObject.Find("Boss Passage Gate");
            if (rewardIndicator == null) rewardIndicator = GameObject.Find("Boss Reward Indicator");
            if (lockedPassage == null) lockedPassage = CreateFallbackGate();
            if (rewardIndicator == null) rewardIndicator = CreateFallbackReward();
            if (lockedPassage != null) lockedPassage.SetActive(true);
            if (rewardIndicator != null) rewardIndicator.SetActive(false);
        }

        private void OnEnable()
        {
            if (boss != null) boss.Defeated += HandleBossDefeated;
        }

        private void Start()
        {
            if (boss != null && boss.State == BossState.Dead) HandleBossDefeated();
        }

        private void OnDisable()
        {
            if (boss != null) boss.Defeated -= HandleBossDefeated;
        }

        private void HandleBossDefeated()
        {
            if (completed) return;
            completed = true;
            if (lockedPassage != null) lockedPassage.SetActive(false);
            if (rewardIndicator != null) rewardIndicator.SetActive(true);
            var inventory = FindFirstObjectByType<Mismo.Gameplay.Player.Equipment.Inventory.PlayerInventory>();
            Mismo.Gameplay.Player.World.WeaponRewardPickup.Spawn(inventory, boss.transform.position,
                Mismo.Gameplay.Player.Equipment.Inventory.ItemCatalog.BossRewardId);
            onBossDefeated?.Invoke();
            Completed?.Invoke(defeatMessage);
        }

        private static GameObject CreateFallbackGate()
        {
            GameObject gate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gate.name = "Boss Passage Gate";
            gate.transform.SetPositionAndRotation(new Vector3(0f, 1f, 16f), Quaternion.identity);
            gate.transform.localScale = new Vector3(6f, 2f, 0.5f);
            return gate;
        }

        private static GameObject CreateFallbackReward()
        {
            GameObject reward = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            reward.name = "Boss Reward Indicator";
            reward.transform.SetPositionAndRotation(new Vector3(0f, 0.25f, 15.2f), Quaternion.identity);
            reward.transform.localScale = new Vector3(1.3f, 0.25f, 1.3f);
            return reward;
        }
    }
}
