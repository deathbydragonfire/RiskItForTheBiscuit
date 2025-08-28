using UnityEngine;
using UnityUtils;

public class AudioManager : Singleton<AudioManager>
{
    [SerializeField] private AudioSource gameplaySource;
    [SerializeField] private AudioSource uiSource;

    [SerializeField] private Vector2 volumeRange = new Vector2(0.9f, 1.1f);
    [SerializeField] private Vector2 pitchRange = new Vector2(0.9f, 1.1f);
    
    
    
    public void PlayOneShot(AudioClip clip, AudioRandomizeMode mode = AudioRandomizeMode.None, float panStereo = 0f) {
        if (clip == null) return;

        float volume = 1f;
        float pitch = 1f;

        switch (mode) {
            case AudioRandomizeMode.Volume:
                volume = Random.Range(volumeRange.x, volumeRange.y);
                break;
            case AudioRandomizeMode.Pitch:
                pitch = Random.Range(pitchRange.x, pitchRange.y);
                break;
            case AudioRandomizeMode.Both:
                volume = Random.Range(volumeRange.x, volumeRange.y);
                pitch = Random.Range(pitchRange.x, pitchRange.y);
                break;
        }

        gameplaySource.pitch = pitch;
        gameplaySource.panStereo = panStereo;
        gameplaySource.PlayOneShot(clip, volume);
    }
    
    public void PlayUI(AudioClip clip) {
        if (clip == null) return;
        uiSource.pitch = 1f;
        uiSource.panStereo = 0f;
        uiSource.PlayOneShot(clip, 1f);
    }
}
public enum AudioRandomizeMode {
    None,
    Volume,
    Pitch,
    Both
}