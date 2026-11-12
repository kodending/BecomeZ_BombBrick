using System;
using System.Collections.Generic;
using System.Linq;

namespace BombBrick.Game.Core
{
    public enum ChallengeResult
    {
        None,
        Cleared,
        Defeated,
        Abandoned
    }

    [Serializable]
    public sealed class GameRunSession
    {
        public int Difficulty { get; private set; } = 1;
        public ChallengeResult Result { get; private set; }
        public float ElapsedSeconds { get; private set; }
        public bool ExpeditionRequested { get; private set; }
        public bool ResumeRequested { get; private set; }
        public string RegionId { get; private set; } = "prototype_region";
        public ExpeditionDifficulty SelectedExpeditionDifficulty { get; private set; } =
            ExpeditionDifficulty.Normal;
        public int CompletedWave { get; private set; }
        public int TotalWaves { get; private set; } = 12;
        public int RemainingLives { get; private set; } = 3;
        public int Gold { get; private set; }
        public int HunterTokensEarned { get; private set; }
        public int RareCaptureNetsEarned { get; private set; }
        public bool HasPlayerExperienceSettlement { get; private set; }
        public int PlayerExperienceBefore { get; private set; }
        public int PlayerExperienceAfter { get; private set; }
        public int PlayerExperienceEarned { get; private set; }
        public int PlayerLevelBefore { get; private set; }
        public int PlayerLevelAfter { get; private set; }
        public bool HasExpeditionProgressUnlockNotice { get; private set; }
        public string ProgressUnlockSourceRegionId { get; private set; } = "";
        public string ProgressUnlockRegionId { get; private set; } = "";
        public ExpeditionDifficulty ProgressUnlockDifficulty { get; private set; } =
            ExpeditionDifficulty.Normal;
        private readonly List<string> capturedMonsterInstanceIds =
            new List<string>();
        public IReadOnlyList<string> CapturedMonsterInstanceIds =>
            capturedMonsterInstanceIds;

        public void Begin(int difficulty)
        {
            Difficulty = Math.Max(1, Math.Min(3, difficulty));
            Result = ChallengeResult.None;
            ElapsedSeconds = 0f;
            ExpeditionRequested = false;
            ResumeRequested = false;
            AcknowledgeExpeditionProgressUnlock();
        }

        public void BeginExpedition(
            string regionId,
            bool resume,
            ExpeditionDifficulty difficulty = ExpeditionDifficulty.Normal)
        {
            RegionId = string.IsNullOrWhiteSpace(regionId)
                ? "prototype_region"
                : regionId;
            Result = ChallengeResult.None;
            ElapsedSeconds = 0f;
            ExpeditionRequested = true;
            ResumeRequested = resume;
            SelectedExpeditionDifficulty = ExpeditionDifficultyRules.Clamp(difficulty);
            CompletedWave = 0;
            TotalWaves = 12;
            RemainingLives = 3;
            Gold = 0;
            HunterTokensEarned = 0;
            RareCaptureNetsEarned = 0;
            HasPlayerExperienceSettlement = false;
            PlayerExperienceBefore = 0;
            PlayerExperienceAfter = 0;
            PlayerExperienceEarned = 0;
            PlayerLevelBefore = 0;
            PlayerLevelAfter = 0;
            AcknowledgeExpeditionProgressUnlock();
            capturedMonsterInstanceIds.Clear();
        }

        public void Tick(float deltaTime)
        {
            if (Result == ChallengeResult.None)
                ElapsedSeconds += Math.Max(0f, deltaTime);
        }

        public void Finish(ChallengeResult result)
        {
            Result = result;
        }

        public void FinishExpedition(
            ChallengeResult result,
            int completedWave,
            int totalWaves,
            int remainingLives,
            int gold)
        {
            Result = result;
            CompletedWave = Math.Max(0, completedWave);
            TotalWaves = Math.Max(1, totalWaves);
            RemainingLives = Math.Max(0, remainingLives);
            Gold = Math.Max(0, gold);
            HunterTokensEarned = HunterTokenRewardSystem.Calculate(
                RegionId,
                CompletedWave,
                TotalWaves,
                result == ChallengeResult.Cleared);
        }

        public void RegisterCapturedMonster(string instanceId)
        {
            if (string.IsNullOrWhiteSpace(instanceId) ||
                capturedMonsterInstanceIds.Contains(instanceId))
                return;
            capturedMonsterInstanceIds.Add(instanceId);
        }

        public void RegisterRareCaptureNetReward(int count)
        {
            RareCaptureNetsEarned = Math.Max(0, count);
        }

        public void RegisterPlayerExperienceSettlement(
            int experienceBefore,
            int experienceAfter,
            int experienceEarned,
            int levelBefore,
            int levelAfter)
        {
            HasPlayerExperienceSettlement = true;
            PlayerExperienceBefore = Math.Max(0, experienceBefore);
            PlayerExperienceAfter = Math.Max(
                PlayerExperienceBefore, experienceAfter);
            PlayerExperienceEarned = Math.Max(0, experienceEarned);
            PlayerLevelBefore = Math.Max(0, levelBefore);
            PlayerLevelAfter = Math.Max(PlayerLevelBefore, levelAfter);
        }

        public void RegisterExpeditionProgressUnlock(
            string sourceRegionId,
            string unlockedRegionId,
            ExpeditionDifficulty unlockedDifficulty)
        {
            HasExpeditionProgressUnlockNotice = true;
            ProgressUnlockSourceRegionId = sourceRegionId ?? "";
            ProgressUnlockRegionId = unlockedRegionId ?? "";
            ProgressUnlockDifficulty =
                ExpeditionDifficultyRules.Clamp(unlockedDifficulty);
        }

        public void AcknowledgeExpeditionProgressUnlock()
        {
            HasExpeditionProgressUnlockNotice = false;
            ProgressUnlockSourceRegionId = "";
            ProgressUnlockRegionId = "";
            ProgressUnlockDifficulty = ExpeditionDifficulty.Normal;
        }

        public void SyncCapturedMonsters(IEnumerable<string> instanceIds)
        {
            capturedMonsterInstanceIds.Clear();
            if (instanceIds == null)
                return;
            capturedMonsterInstanceIds.AddRange(
                instanceIds
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.Ordinal));
        }

        public void LeaveExpeditionFlow()
        {
            ExpeditionRequested = false;
            ResumeRequested = false;
        }
    }
}
