using UnityEngine;

namespace BombBrick.Game.Core
{
    public sealed partial class GameAudio
    {
        public enum EvolutionCue { Preparation, Impact, Stamp, Afterglow, RankAccent }

        private static readonly string[] EvolutionPaths =
        {
            "Audio/Evolution/prepare", "Audio/Evolution/impact", "Audio/Evolution/stamp",
            "Audio/Evolution/afterglow", "Audio/Evolution/rank2", "Audio/Evolution/rank3",
            "Audio/Evolution/rank4"
        };
        private static readonly float[] EvolutionGains = { .065f, .10f, .12f, .05f, .032f, .037f, .042f };
        private readonly AudioClip[] evolutionClips = new AudioClip[7];

        private void InitializeEvolutionAudio()
        {
            for (int i = 0; i < evolutionClips.Length; i++)
            {
                evolutionClips[i] ??= Resources.Load<AudioClip>(EvolutionPaths[i]);
                if (evolutionClips[i] != null) MeasureCombatClip(evolutionClips[i]);
            }
        }

        public static void PlayEvolution(EvolutionCue cue, int rank)
        {
            if (instance == null || instance.paused ||
                (!GameFrontend.IsProduction && !instance.previewEnabled)) return;
            instance.PlayEvolutionCue(cue, rank);
        }

        private void PlayEvolutionCue(EvolutionCue cue, int rank)
        {
            if (rank < 2 || rank > 4 || (int)cue < 0 || (int)cue > (int)EvolutionCue.RankAccent) return;
            int index = cue == EvolutionCue.RankAccent ? rank + 2 : (int)cue;
            AudioClip clip = evolutionClips[index] ??
                (evolutionClips[index] = Resources.Load<AudioClip>(EvolutionPaths[index]));
            if (clip == null) return;
            var voice = chestVoices[nextChestVoice % chestVoices.Length];
            if (voice == null) return;
            voice.loop = false;
            // Reuse the reward pool's RMS matching, peak budget, SFX volume and scene/pause handling.
            PlayRewardVoice(clip, EvolutionGains[index], 1f, 0f);
        }
    }
}
