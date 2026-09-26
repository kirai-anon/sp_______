using System.Collections;
using UnityEngine;

public class CameraShake : MonoBehaviour
{
    private Vector3 offsetPosition;
    private Vector3 currentShakeOffset;
    private Vector3 baseLocalPosition;

    void Start()
    {
        // Save the initial local position as our baseline
        baseLocalPosition = transform.localPosition;
    }

    void LateUpdate()
    {
        // Apply the persistent offset + any active shake offset every frame
        transform.localPosition = baseLocalPosition + offsetPosition + currentShakeOffset;
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
            float strength = (1f - (elapsed / duration)) * magnitude;
            Vector2 randomPoint = Random.insideUnitCircle * strength;

            // Store just the shake offset temporarily
            currentShakeOffset = new Vector3(randomPoint.x, randomPoint.y, 0f);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Clear the shake offset when finished
        currentShakeOffset = Vector3.zero;
    }

    public void SetOffset(Vector2 offset)
    {
        offsetPosition = new Vector3(offset.x, offset.y, 0f);
    }
}