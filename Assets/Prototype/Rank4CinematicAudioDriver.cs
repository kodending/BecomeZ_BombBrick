using UnityEngine;

public sealed class Rank4CinematicAudioDriver:MonoBehaviour
{
    private Rank4CinematicAudio owner;
    public void Initialize(Rank4CinematicAudio audio)=>owner=audio;
    private void Update()=>owner?.UpdateAudio(Time.unscaledDeltaTime);
    private void OnDisable()=>owner?.Stop();
}
