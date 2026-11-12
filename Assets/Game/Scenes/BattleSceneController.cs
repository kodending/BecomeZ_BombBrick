using BombBrick.Game.Core;
using UnityEngine;

namespace BombBrick.Game.Scenes
{
    public sealed class BattleSceneController : ProductionSceneController
    {
        private void Update()
        {
            if (GameFrontend.BlocksGameplay) return;

#if UNITY_EDITOR
            if (Input.GetKeyDown(KeyCode.F10))
                GameFlowController.Instance.FinishChallenge(ChallengeResult.Cleared);
#endif
        }

        private void OnGUI()
        {
            if (GameFrontend.BlocksGuiInput) return;
#if UNITY_EDITOR
            GUI.Label(new Rect(Screen.width - 280f, Screen.height - 34f, 260f, 24f),
                "ESC 메뉴 · F10 개발용 클리어", BodyStyle);
#endif
        }
    }
}
