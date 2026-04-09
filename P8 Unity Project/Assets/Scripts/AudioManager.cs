using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    [SerializeField] AudioSource audioSourcePrefab;
    [SerializeField] int maxAudioSourcesCount = 30;
    [SerializeField] List<AudioSource> availableAudioSources = new();

    public static event Action<AudioClip, Transform, float, bool> OnPlayAudio;
    public static event Func<AudioClip, Transform, float, bool, AudioSource> OnPlayLoopAudio;
    public static event Action<AudioSource> OnStopLoopAudio;

    /// <summary>
    /// Plays a specified audio clip at a given location with a specified volume. Optionally, the audio source can be parented to the location if the audio source needs to follow it.
    /// </summary>
    public static void PlaySound(AudioClip clip, Transform location, float volume, bool parented = false) => OnPlayAudio?.Invoke(clip, location, volume, parented);

    /// <summary>
    /// Plays a specified audio clip in a loop at a given location with a specified volume. Optionally, the audio source can be parented to the location if the audio source needs to follow it.
    /// </summary>
    public static AudioSource PlayLoopSound(AudioClip clip, Transform location, float volume, bool parented = false) => OnPlayLoopAudio?.Invoke(clip, location, volume, parented);
    
    /// <summary>
    /// Stops playback of the specified audio source if it is currently playing.
    /// </summary>
    public static void StopSound(AudioSource source) => OnStopLoopAudio?.Invoke(source);
    
    static AudioManager instance;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
            Destroy(gameObject);
        CreateAudioSourcePool();
    }

    void CreateAudioSourcePool()
    {
        for (int i = 0; i < maxAudioSourcesCount; i++ ) {
            var audioSource = Instantiate(audioSourcePrefab, transform);
            audioSource.gameObject.name = $"AudioSource_{i}";
            availableAudioSources.Add(audioSource);
        }
    }

    void OnEnable()
    {
        OnPlayAudio += PlayAudio;
        OnPlayLoopAudio += PlayLoopAudio;
        OnStopLoopAudio += StopLoopAudio;
    }

    void OnDisable()
    {
        OnPlayAudio -= PlayAudio;
        OnPlayLoopAudio -= PlayLoopAudio;
        OnStopLoopAudio -= StopLoopAudio;
    }

    void PlayAudio(AudioClip clip, Transform location, float volume, bool parented)
    {
        if(clip == null)
        {
            Debug.LogWarning("Attempted to play a null audio clip.");
            return;
        }

        // In case an audio source was destroyed or became null, we should clean it up from the pool to avoid errors when trying to access it.
        if (availableAudioSources.Any(item => item == null || item.gameObject == null))
        {
            print("Cleaning up null audio sources from the pool.");
            availableAudioSources.RemoveAll(item => item == null || item.gameObject == null);
        }

        var clipLength = clip.length;
        var audioSource = availableAudioSources.Find(source => !source.isPlaying);
        
        if (audioSource == null || audioSource.gameObject == null)
        {
            audioSource = AddAudioSource();
            print($"Created new audio source for {clip.name}.");
        }

        audioSource.transform.position = location.position;
        
        if (parented)
        {
            audioSource.transform.parent = location;
        }
        audioSource.clip = clip;
        audioSource.volume = volume;
        audioSource.Play();
        
        StartCoroutine(StopAudio(audioSource, clipLength + 0.1f)); // Add a small buffer to ensure the clip has finished playing before relisting the audio source.
    }

    IEnumerator StopAudio(AudioSource source, float delay = 0)
    {
        yield return new WaitForSeconds(delay);
        if (source == null) yield return null;
        source.transform.parent = transform;
        
        source.volume = 0.0001f; // Avoid clipping sounds when stopping.
        yield return new WaitForSeconds(0.1f);
        source.Stop();
    }

    AudioSource PlayLoopAudio(AudioClip clip, Transform location, float volume, bool parented)
    {
        if (clip == null)
        {
            Debug.LogWarning("Attempted to play a null audio clip.");
            return null;
        }

        // In case an audio source was destroyed or became null, we should clean it up from the pool to avoid errors when trying to access it.
        if (availableAudioSources.Any(item => item == null || item.gameObject == null))
        {
            print("Cleaning up null audio sources from the pool.");
            availableAudioSources.RemoveAll(item => item == null || item.gameObject == null);
        }

        var audioSource = availableAudioSources.Find(source => !source.isPlaying);
        
        if (audioSource == null || audioSource.gameObject == null)
        {
            audioSource = AddAudioSource();
            print($"Created new audio source for {clip.name}.");
        }

        audioSource.transform.position = location.position;

        if (parented)
        {
            audioSource.transform.parent = location;
        }
        audioSource.clip = clip;
        audioSource.volume = volume;
        audioSource.loop = true;
        audioSource.Play();

        return audioSource;
    }

    void StopLoopAudio(AudioSource source)
    {
        if (source != null && source.isPlaying)
        {
            source.transform.parent = transform;
            StartCoroutine(StopAudio(source));
        }
    }

    /// <summary>
    /// If there are no available audio sources to play a clip, this method can be called to add more audio sources to the pool.
    /// </summary>
    /// <returns></returns>
    AudioSource AddAudioSource() 
    {
        var audioSource = Instantiate(audioSourcePrefab, transform);
        audioSource.gameObject.name = $"AudioSource_{availableAudioSources.Count}";
        availableAudioSources.Add(audioSource);
        return audioSource;
    }
}
