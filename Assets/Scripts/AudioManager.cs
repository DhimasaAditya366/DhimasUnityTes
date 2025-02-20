using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    private static AudioManager instance;
    public static AudioManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<AudioManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("AudioManager");
                    instance = go.AddComponent<AudioManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }

    [Header("Background Music")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioClip bgmClip;
    [SerializeField] [Range(0f, 1f)] private float bgmVolume = 0.5f;

    [Header("Sound Effects")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private List<AudioClip> sfxClips = new List<AudioClip>();
    [SerializeField] [Range(0f, 1f)] private float sfxVolume = 1f;

    private void Awake()
    {
        // Singleton pattern
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        // Initialize audio sources if not set
        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;
        }

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.loop = false;
            sfxSource.playOnAwake = false;
        }
    }

    private void Start()
    {
        // Start playing BGM if clip is assigned
        if (bgmClip != null)
        {
            PlayBGM();
        }
    }

    // Play background music
    public void PlayBGM()
    {
        if (bgmClip != null && bgmSource != null)
        {
            bgmSource.clip = bgmClip;
            bgmSource.volume = bgmVolume;
            bgmSource.Play();
        }
    }

    // Change and play new background music
    public void ChangeBGM(AudioClip newClip)
    {
        if (newClip != null && bgmSource != null)
        {
            bgmClip = newClip;
            bgmSource.Stop();
            PlayBGM();
        }
    }

    // Play sound effect by index
    public void PlaySFX(int index)
    {
        if (index >= 0 && index < sfxClips.Count && sfxSource != null)
        {
            sfxSource.PlayOneShot(sfxClips[index], sfxVolume);
        }
        else
        {
            Debug.LogWarning($"SFX index {index} is out of range or sfxSource is null!");
        }
    }

    // Play sound effect by clip reference
    public void PlaySFX(AudioClip clip)
    {
        if (clip != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(clip, sfxVolume);
        }
    }

    // Control volumes
    public void SetBGMVolume(float volume)
    {
        bgmVolume = Mathf.Clamp01(volume);
        if (bgmSource != null)
        {
            bgmSource.volume = bgmVolume;
        }
    }

    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
    }

    // Stop background music
    public void StopBGM()
    {
        if (bgmSource != null)
        {
            bgmSource.Stop();
        }
    }

    // Pause background music
    public void PauseBGM()
    {
        if (bgmSource != null)
        {
            bgmSource.Pause();
        }
    }

    // Resume background music
    public void ResumeBGM()
    {
        if (bgmSource != null)
        {
            bgmSource.UnPause();
        }
    }
}
