using System.Collections;
using UnityEngine;

public class RiftEffect : MonoBehaviour
{
    [Header("Animation Settings")]
    public float growDuration = 0.3f;
    public float pauseDuration = 0.5f;
    public float shrinkDuration = 0.3f;
    public Vector3 targetScale = new Vector3(1.5f, 1.5f, 1f);

    // This method handles the entire lifetime of the rift
    public IEnumerator RunRiftAnimation(System.Action spawnCallback, System.Action completeCallback)
    {
        float elapsedTime = 0f;
        while (elapsedTime < growDuration)
        {
            elapsedTime += Time.deltaTime;
            transform.localScale = Vector3.Lerp(Vector3.zero, targetScale, elapsedTime / growDuration);
            yield return null;
        }
        transform.localScale = targetScale;

        elapsedTime = 0f;
        while (elapsedTime < pauseDuration)
        {
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        spawnCallback.Invoke();

        elapsedTime = 0f;
        while (elapsedTime < pauseDuration)
        {
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        elapsedTime = 0f;
        while (elapsedTime < shrinkDuration)
        {
            elapsedTime += Time.deltaTime;
            transform.localScale = Vector3.Lerp(targetScale, Vector3.zero, elapsedTime / shrinkDuration);
            yield return null;
        }
        transform.localScale = Vector3.zero;

        completeCallback?.Invoke();

        Destroy(gameObject);
    }
}