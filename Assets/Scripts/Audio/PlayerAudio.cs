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

    [Header("Grab")]
    [SerializeField] private AudioClip grabClip;

    [Header("Death")]
    [SerializeField] private AudioClip deathClip;

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

    public void PlayGrab()
    {
        Play(grabClip);
    }

    // Death reloads the room immediately, destroying this source; play on a detached
    // object that survives the load so the clip (and its reverb tail) isn't cut off.
    public void PlayDeath()
    {
        if (source == null || deathClip == null) return;
        var player = new GameObject("DeathSound").AddComponent<AudioSource>();
        DontDestroyOnLoad(player.gameObject);
        player.outputAudioMixerGroup = source.outputAudioMixerGroup;
        player.volume = source.volume;
        player.pitch = source.pitch;
        player.spatialBlend = 0f;
        player.PlayOneShot(deathClip);
        Destroy(player.gameObject, deathClip.length / Mathf.Max(Mathf.Abs(source.pitch), 0.01f) + 0.1f);
    }

    private void Play(AudioClip clip)
    {
        if (source != null && clip != null) source.PlayOneShot(clip);
    }
}
