using System;
using Mismo.Gameplay.Player.Equipment.Inventory;
using UnityEngine;
using UnityEngine.Events;
namespace Mismo.Gameplay.Player.Quests
{
    public enum CardinalDirection { North, East, South, West }
    public sealed class CompassAltar:MonoBehaviour
    {
        public string signal="altar.solved",sourceId="forest.altar";
        public CompassStone[] stones=Array.Empty<CompassStone>();
        public CardinalDirection[] solution={CardinalDirection.East,CardinalDirection.West,CardinalDirection.North,CardinalDirection.South};
        public UnityEvent onSolved;
        bool opened;
        public bool Matches()
        {
            if(stones==null||solution==null||stones.Length==0||stones.Length!=solution.Length)return false;
            for(int i=0;i<stones.Length;i++)if(stones[i]==null||stones[i].direction!=solution[i])return false;
            return true;
        }
        public bool TrySolve(PlayerInventory player)
        {
            if(!Matches()||player==null)return false;
            bool saved=player.TryRecordQuestSignal(signal,sourceId);
            if(saved&&!opened){opened=true;onSolved?.Invoke();}return saved;
        }
        public void Restore(PlayerInventory player)
        {
            if(opened||player?.Quests==null)return;
            foreach(var q in player.Quests.quests)
            {
                var state=player.QuestState(q);if(state==null)continue;
                foreach(var o in q.objectives)if(o.kind==QuestObjectiveKind.Signal&&o.signal==signal&&state.signals.Contains(o.id+":"+sourceId))
                {
                    if(stones!=null&&solution!=null&&stones.Length==solution.Length)for(int i=0;i<stones.Length;i++)if(stones[i]!=null)stones[i].SetDirection(solution[i]);
                    opened=true;onSolved?.Invoke();return;
                }
            }
        }
        public bool IsOpened=>opened;
    }
}
