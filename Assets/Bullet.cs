using UnityEngine;
using System.Collections.Generic;

public class Bullet : MonoBehaviour
{
    [SerializeField] private float speed = 20f;

    private int damage;
    private int lightningDamage;
    private int lightningBounces;
    private float poisonDamagePerSec;
    private float poisonDuration;
    private BallSpawner ballSpawner;
    private bool hasHit = false;

    public void Initialize(int dmg, int ltnDmg, int ltnBounces, float poisonDps, float poisonDur, BallSpawner spawner)
    {
        damage = dmg;
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

        ball.TakeDamage(damage);

        if (lightningBounces > 0 && lightningDamage > 0)
        {
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
        List<Vector3> lightningPositions = new List<Vector3> { firstBall.transform.position };

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

            nearest.TakeTrueDamage(lightningDamage);

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

    private void DrawContinuousLightning(List<Vector3> positions)
    {
        GameObject lightningObj = new GameObject("ContinuousLightning");
        LineRenderer lr = lightningObj.AddComponent<LineRenderer>();

        // Set the points to match our recorded chain length
        lr.positionCount = positions.Count;
        lr.SetPositions(positions.ToArray());

        // Styling
        lr.startWidth = 0.05f;
        lr.endWidth = 0.05f;
        lr.useWorldSpace = true;

        // Fix the deletion bug: Use a simple mobile or unlit material to avoid leaking material instances
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.material.color = new Color(0.4f, 0.8f, 1f);

        // This will cleanly delete the entire chain after 0.15 seconds
        Destroy(lightningObj, 0.15f);
    }
}