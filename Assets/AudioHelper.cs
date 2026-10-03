using UnityEngine;

public static class AudioHelper
{
    public static AudioSource PlayClipAtPoint(AudioClip clip, Vector3 position, float volume, float pitch)
    {
        // 1. Create and position the temporary audio source
        GameObject tempGO = new GameObject("TempAudio_WithPitch");
        tempGO.transform.position = position;

        // 2. Attach and configure the AudioSource component
        AudioSource audioSource = tempGO.AddComponent<AudioSource>();
        audioSource.clip = clip;
        audioSource.volume = volume;
        audioSource.pitch = pitch;

        // 3. Force 3D spatial audio calculations
        audioSource.spatialBlend = 1f;
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        audioSource.minDistance = 1f;
        audioSource.maxDistance = 50f;

        // 4. Play the sound
        audioSource.Play();

        // 5. Clean up the object safely when finished (scaled by pitch speed)
        float destroyDelay = clip.length / Mathf.Abs(pitch);
        GameObject.Destroy(tempGO, destroyDelay);

        return audioSource;
    }
}