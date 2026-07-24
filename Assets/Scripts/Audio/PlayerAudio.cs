using UnityEngine;

// Central home for the player's one-shot SFX (banish, and whatever else gets added later).
[RequireComponent(typeof(AudioSource))]
public class PlayerAudio : MonoBehaviour
{
    [SerializeField] private AudioSource source;

    [Header("Banish")]
    [SerializeField] private AudioClip banishArmClip;
    [SerializeField] private AudioClip banishFireClip;
    [SerializeField] private AudioClip banishReturnClip;

    private void Awake()
    {
        if (source == null)
        {
            source = GetComponent<AudioSource>();
        }
    }

    public void PlayBanishArm()
    {
        Play(banishArmClip);
    }

    public void PlayBanishFire()
    {
        Play(banishFireClip);
    }

    public void PlayBanishReturn()
    {
        Play(banishReturnClip);
    }

    private void Play(AudioClip clip)
    {
        if (source != null && clip != null) source.PlayOneShot(clip);
    }
}
