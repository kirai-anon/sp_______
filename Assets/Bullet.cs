using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using Unity.Mathematics;

public class Bullet : MonoBehaviour
{
    [SerializeField] private float speed = 20f;

    private float bulletDamage;
    private float lightningDamage;
    private int lightningBounces;
    private float poisonDamagePerSec;
    private float poisonDuration;
    private BallSpawner ballSpawner;
    private bool hasHit = false;

    public void Initialize(float dmg, float ltnDmg, int ltnBounces, float poisonDps, float poisonDur, BallSpawner spawner)
    {
        bulletDamage = dmg;
        lightningDamage = ltnDmg;
        lightningBounces = ltnBounces;
        poisonDamagePerSec = poisonDps;
        poisonDuration = poisonDur;
        ballSpawner = spawner;
    }

    void Update()
    {
        transform.position += Vector3.up * speed * Time.deltaTime;
        if (transform.position.y > 10f) Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (hasHit) return;
        if (!other.TryGetComponent<Ball>(out Ball ball)) return;
        hasHit = true;

        ball.TakeDamage(bulletDamage, DamageType.Normal);

        if (lightningBounces > 0 && lightningDamage > 0)
        {
            ball.TakeTrueDamage(lightningDamage, DamageType.Lightning);
            TriggerLightning(ball);
        }

        if (poisonDuration > 0 && poisonDamagePerSec > 0)
        {
            ball.ApplyPoison(poisonDamagePerSec, poisonDuration);
        }

        Destroy(gameObject);
    }

    private void TriggerLightning(Ball firstBall)
    {
        List<Ball> hit = new List<Ball> { firstBall };
        Ball current = firstBall;
        int totalHits = 1 + lightningBounces;

        // Keep track of positions for our continuous line
        List<Vector3> lightningPositions = new List<Vector3> { gameObject.transform.position, firstBall.transform.position };

        for (int i = 1; i < totalHits; i++)
        {
            Ball nearest = null;
            float nearestDist = float.MaxValue;

            foreach (Ball b in ballSpawner.balls)
            {
                if (hit.Contains(b)) continue;
                float dist = Vector2.Distance(current.transform.position, b.transform.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = b;
                }
            }

            if (nearest == null) break;

            nearest.TakeTrueDamage(lightningDamage, DamageType.Lightning);

            if (poisonDuration > 0 && poisonDamagePerSec > 0)
                nearest.ApplyPoison(poisonDamagePerSec, poisonDuration);

            // Record the next point in the lightning chain
            lightningPositions.Add(nearest.transform.position);

            hit.Add(nearest);
            current = nearest;
        }

        // Draw the entire continuous lightning path at once
        if (lightningPositions.Count > 1)
        {
            DrawContinuousLightning(lightningPositions);
        }
    }

    public void DrawContinuousLightning(List<Vector3> basePoints)
    {
        if (basePoints == null || basePoints.Count < 2) return;

        GameObject lightningObj = new GameObject("ContinuousLightning");
        LineRenderer lr = lightningObj.AddComponent<LineRenderer>();

        List<Vector3> fullLightningPositions = new List<Vector3>();

        // Iterate through each pair of points in the user's base chain
        for (int i = 0; i < basePoints.Count - 1; i++)
        {
            Vector3 segmentStart = basePoints[i];
            Vector3 segmentEnd = basePoints[i + 1];

            // Generate jagged sub-points for this specific segment
            // Parameters: start, end, displacement, generations
            List<Vector3> segmentPoints = GenerateLightningPath(segmentStart, segmentEnd, 0.3f, 3);

            // If this isn't the first segment, remove the first point 
            // to prevent duplicate overlapping vertices where segments connect
            if (i > 0 && segmentPoints.Count > 0)
            {
                segmentPoints.RemoveAt(0);
            }

            fullLightningPositions.AddRange(segmentPoints);
        }

        // Apply points to the LineRenderer
        lr.positionCount = fullLightningPositions.Count;
        lr.SetPositions(fullLightningPositions.ToArray());

        // Styling
        lr.startWidth = 0.07f;
        lr.endWidth = 0.07f;
        lr.useWorldSpace = true;

        // Material and Color
        lr.material = new Material(Shader.Find("Sprites/Default"));
        Color lightningColor = new Color(1f, 1f, 1f); // Electric blue-white
        lr.startColor = lightningColor;
        lr.endColor = lightningColor;

        // Start the fade and destroy coroutine
        LightningFade fader = lightningObj.AddComponent<LightningFade>();
        fader.Initialize(lr, 0.3f);
    }

    private List<Vector3> GenerateLightningPath(Vector3 start, Vector3 end, float displacement, int generations)
    {
        List<Vector3> points = new List<Vector3> { start, end };

        float x_d = (end.x - start.x);
        float y_d = (end.y - start.y);
        float dist = math.sqrt(math.pow(x_d,2)+math.pow(y_d,2));

        for (int i = 0; i < generations; i++)
        {
            List<Vector3> nextPoints = new List<Vector3>();
            for (int j = 0; j < points.Count - 1; j++)
            {
                Vector3 p1 = points[j];
                Vector3 p2 = points[j + 1];

                Vector3 mid = (p1 + p2) * 0.5f;
                Vector3 randomOffset = displacement * dist * UnityEngine.Random.insideUnitSphere;
                mid += randomOffset;

                nextPoints.Add(p1);
                nextPoints.Add(mid);
            }
            nextPoints.Add(points[points.Count - 1]);
            points = nextPoints;
            displacement *= 0.5f;
        }

        return points;
    }

    // Helper component to handle fading over time
    public class LightningFade : MonoBehaviour
    {
        private LineRenderer lineRenderer;
        private float duration;
        private float elapsedTime;
        private Color initialColor;

        public void Initialize(LineRenderer lr, float lifeTime)
        {
            lineRenderer = lr;
            duration = lifeTime;
            initialColor = lr.startColor;
            StartCoroutine(FadeRoutine());
        }

        private IEnumerator FadeRoutine()
        {
            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float alpha = Mathf.Lerp(1f, 0f, elapsedTime / duration);

                Color fadedColor = new Color(initialColor.r, initialColor.g, initialColor.b, alpha);
                lineRenderer.startColor = fadedColor;
                lineRenderer.endColor = fadedColor;

                yield return null;
            }

            Destroy(gameObject);
        }
    }
}