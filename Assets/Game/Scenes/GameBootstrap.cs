using BombBrick.Game.Core;
using UnityEngine;

namespace BombBrick.Game.Scenes
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        private void Start()
        {
            // Boot is the title screen. Starting a game never resets the profile.
            _ = GameFlowController.Instance;
        }
    }
}
