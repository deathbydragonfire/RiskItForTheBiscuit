using System.Collections;
using com.cyborgAssets.inspectorButtonPro;
using UnityEngine;
using UnityEngine.Audio;

  public class MusicLayerSystem : MonoBehaviour
  {
    [SerializeField] private AudioClip[] audioStems;
  
    [SerializeField][Range(0.01f, 12f)] private float fadeInTime = 1.5f;
  
    [SerializeField][Range(0.01f, 12f)] private float fadeOutTime = 1.5f;
  
    [Tooltip("If you are using the Unity Audio Mixer, this is where they will be routed to, else leave empty")]
    [SerializeField] private AudioMixerGroup musicMixerGroup; 
  
    private AudioSource[] audioSources;
  
    //currentIntensity = 0 means the first music layer will play when you start the system
    private int currentIntensity;
  
    private int amountOfStems;
  
    //The max music volume, the fade in will go up to this value
    private const float MusicVolumeMax = 1f;
  
    // used as a way to stop multiple starts
    private bool musicSystemIsOn;

    private void Awake()
    {
      Init();
    }

    private void Init()
    {
      musicSystemIsOn = false;
      amountOfStems = audioStems.Length;

      if (amountOfStems <= 0)
      {
        Debug.LogWarning("No audio clips added to the audio stems!!");
        return; 
      }
      audioSources = new AudioSource[amountOfStems];
    
      for (int i = 0; i < amountOfStems; i++)
      {
        AudioSource newAudioSource = gameObject.AddComponent<AudioSource>();
    
        newAudioSource.playOnAwake = false;
        newAudioSource.loop = true;

        newAudioSource.clip = audioStems[i];

        if(musicMixerGroup != null)
          newAudioSource.outputAudioMixerGroup = musicMixerGroup;
      
        audioSources[i] = newAudioSource;
      }
    }
  
  
    [ProButton]
    public void StartMusicSystem()
    {
      if (musicSystemIsOn) return;
      musicSystemIsOn = true;
    
      StartCoroutine(FadeInMusicStem(audioSources[0],fadeInTime));

      for (int i = 1; i < audioSources.Length; i++)
      {
        audioSources[i].Play();
        audioSources[i].mute = true;
      }

      currentIntensity = 0;
    }
  

    /// <summary>
    /// Does a complete stop of the music system 
    /// </summary>
    [ProButton]
    public void StopMusicSystem()
    {
      if(!musicSystemIsOn) return;
    
      foreach (AudioSource musicSource in audioSources)
      {
        StartCoroutine(FadeOutMusicStem(musicSource, fadeOutTime));
      }

      StartCoroutine(StopAudioSourcesAfterElapsedTime());
    }

    private IEnumerator StopAudioSourcesAfterElapsedTime()
    {
      yield return new WaitForSeconds(fadeOutTime);
      foreach (AudioSource musicSource in audioSources)
      {
        musicSource.Stop();
      }
      musicSystemIsOn = false;
    }
  
    [ProButton]
    public void IncreaseIntensity()
    {
      if (!musicSystemIsOn) return;
    
      if (currentIntensity >= amountOfStems - 1)
      {
        Debug.LogWarning("MusicLayerSystem: Already reached highest intensity");
        return;
      }
      currentIntensity++;
      StartCoroutine(FadeInMusicStem(audioSources[currentIntensity], fadeInTime));
    
    }
  
  
    [ProButton]
    public void DecreaseIntensity()
    {
      if (!musicSystemIsOn) return;

      if (currentIntensity <= 0)
      {
        Debug.LogWarning("MusicLayerSystem: Already reached lowest intensity");
        return;
      }
      StartCoroutine(FadeOutMusicStem(audioSources[currentIntensity], fadeOutTime));
      currentIntensity--;
    }
  
  
  
    // using coroutine so its happening at a fixed frame rate, instead of constant update checks
    private IEnumerator FadeInMusicStem(AudioSource activeSource, float transitionTime)
    {
      activeSource.volume = 0.0f;
      activeSource.mute = false;

      for (float t = 0.0f; t <= transitionTime; t += Time.deltaTime)
      {
        activeSource.volume = (t / transitionTime) * MusicVolumeMax;
        yield return null;
      }

      activeSource.volume = MusicVolumeMax; 

    }

    private IEnumerator FadeOutMusicStem(AudioSource activeSource, float transitionTime)
    {

      for (float t = 0.0f; t <= transitionTime; t += Time.deltaTime)
      {
        activeSource.volume = (MusicVolumeMax -((t / transitionTime) * MusicVolumeMax));
        yield return null;
      }
      activeSource.mute = true;

      activeSource.volume = MusicVolumeMax;
    }
    
    
    
    //get all pickup spheres 
    //when a sphere is picked up, check how many is available 
    
    // keep track how many in basket 
    
  }
