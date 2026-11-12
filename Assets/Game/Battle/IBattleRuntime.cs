using System;

namespace BombBrick.Game.Battle
{
    public interface IBattleRuntime
    {
        event Action PlayerDefeated;

        bool IsPlayerDefeated { get; }
        int CurrentHealth { get; }
        int MaximumHealth { get; }
        int Score { get; }
        int Kills { get; }
    }
}
