using System;
using System.Collections.Generic;
using com.cyborgAssets.inspectorButtonPro;
using UnityEngine;
using Random = UnityEngine.Random;

public class BiscuitAudio : MonoBehaviour
{
    public List<AudioClip> biscuitPops; 
    public List<AudioClip> biscuitDrops;
   [SerializeField] private AudioSource audioSource;
   [SerializeField] private MagneticAttractor magneticAttractor;

   private void Start()
   {
       magneticAttractor.OnBiscuitDropped.AddListener(PlayBiscuitDrop);
       magneticAttractor.OnSelfDestruct.AddListener(PlayBiscuitPop);
   }

   [ProButton]
    public void PlayBiscuitDrop() => audioSource.PlayOneShot(biscuitDrops[Random.Range(0, biscuitDrops.Count)]);

    public void PlayBiscuitPop() =>   AudioManager.Instance.PlayOneShot(biscuitPops[Random.Range(0, biscuitPops.Count)], AudioRandomizeMode.Both);
   
}
