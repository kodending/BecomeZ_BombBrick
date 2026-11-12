using System.Collections;
using BombBrick.Game.Core;
using UnityEngine;

namespace BombBrick.Game.Battle
{
    [DisallowMultipleComponent]
    public sealed class BrickBattleRuntimeAdapter : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float defeatResultDelay = 2.5f;

        private IBattleRuntime runtime;
        private bool resultRequested;

        public IBattleRuntime Runtime => runtime;

        private void Awake()
        {
            BrickSurvivorGame brickRuntime = GetComponent<BrickSurvivorGame>();
            if (brickRuntime == null)
                brickRuntime = gameObject.AddComponent<BrickSurvivorGame>();

            runtime = brickRuntime;
            runtime.PlayerDefeated += HandlePlayerDefeated;
        }

        private void OnDestroy()
        {
            if (runtime != null)
                runtime.PlayerDefeated -= HandlePlayerDefeated;
        }

        private void HandlePlayerDefeated()
        {
            if (resultRequested)
                return;

            resultRequested = true;
            StartCoroutine(ShowDefeatResult());
        }

        private IEnumerator ShowDefeatResult()
        {
            yield return new WaitForSecondsRealtime(defeatResultDelay);
            GameFlowController.Instance.FinishChallenge(ChallengeResult.Defeated);
        }
    }
}
