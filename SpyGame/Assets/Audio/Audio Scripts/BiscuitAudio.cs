using System;
using System.Collections.Generic;
using com.cyborgAssets.inspectorButtonPro;
using UnityEngine;
using Random = UnityEngine.Random;

public class BiscuitAudio : MonoBehaviour
{
    public List<AudioClip> biscuitPops; 
    public List<AudioClip> biscuitDrops;
    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    [ProButton]
    public void PlayBiscuitDrop() => audioSource.PlayOneShot(biscuitDrops[Random.Range(0, biscuitDrops.Count)]);

    public void PlayBiscuitPop()
    {
        //send to audiomanager as this object will be destroyed soon
        //AudioMananger.Instance.Play(biscuitPops[Random.Range(0, biscuitPops.Count)]);
    }
}
