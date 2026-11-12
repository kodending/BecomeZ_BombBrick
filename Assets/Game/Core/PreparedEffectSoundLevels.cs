using System;
using UnityEngine;

namespace BombBrick.Game.Core
{
    public sealed class PreparedEffectSoundLevels : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public AudioClip clip;
            public int samples, channels, frequency;
            public float peak, rms;
        }
        public Entry[] clips;
    }
}
