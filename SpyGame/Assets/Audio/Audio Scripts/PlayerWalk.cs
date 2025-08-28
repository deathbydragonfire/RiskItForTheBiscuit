using com.cyborgAssets.inspectorButtonPro;
using UnityEngine;

public class PlayerWalk : MonoBehaviour
{
    //TODO: hook start and stop based on player movement
    
    public AudioClip[] footsteps;

    [SerializeField] [Range(0.001f, 1f)] private float stepInterval = 0.5f;
    [SerializeField] private PlayerAnimationController playerAnim;

    private AudioSource audioSource;
    private int lastClipIndex = -1; 
    private float timer;
    private bool isPlaying =true;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }
    

    void Update()
    {
        if (!isPlaying || footsteps.Length == 0) return;

        timer += Time.deltaTime;
        if (timer >= stepInterval && playerAnim.IsMoving)
        {
            PlayFootstep();
            timer = 0f;
        }
    }

    [ProButton]
    public void StartFootsteps()
    {
        isPlaying = true;
        timer = stepInterval; // force immediate first step
    }

    [ProButton]
    public void StopFootsteps()
    {
        isPlaying = false;
    }

    [ProButton]
    private void PlayFootstep()
    {
        if (footsteps.Length == 0) return;

        int clipIndex;
        do
        {
            clipIndex = Random.Range(0, footsteps.Length);
        }
        while (clipIndex == lastClipIndex && footsteps.Length > 1);

        lastClipIndex = clipIndex;
        audioSource.PlayOneShot(footsteps[clipIndex]);
    }
}
