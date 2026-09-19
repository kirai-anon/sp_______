using System.Collections;
using UnityEngine;

public class CameraShake : MonoBehaviour
{
    private Vector3 originalPos;

    void Start()
    {
        originalPos = transform.localPosition;
    }

    public void TriggerShake(float duration, float magnitude)
    {
        StartCoroutine(Shake(duration, magnitude));
    }

    private IEnumerator Shake(float duration, float magnitude)
    {
        float elapsed = 0.0f;

        while (elapsed < duration)
        {
            transform.localPosition = (1 - elapsed / duration) * magnitude * Random.insideUnitSphere;

            elapsed += Time.deltaTime;

            yield return null;
        }

        // Reset back to the original local position
        transform.localPosition = originalPos;
    }

    void Update()
    {
        
    }
}