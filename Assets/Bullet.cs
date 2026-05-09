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

        // Normal damage
        ball.TakeDamage(damage);

        // Lightning
        if (lightningBounces > 0 && lightningDamage > 0)
        {
            TriggerLightning(ball);
        }

        // Poison
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
        int totalHits = 1 + lightningBounces; // first ball + bounces

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

            // Draw lightning line
            DrawLightningLine(current.transform.position, nearest.transform.position);

            nearest.TakeTrueDamage(lightningDamage);

            // Apply poison to chained balls too
            if (poisonDuration > 0 && poisonDamagePerSec > 0)
                nearest.ApplyPoison(poisonDamagePerSec, poisonDuration);

            hit.Add(nearest);
            current = nearest;
        }
    }

    private void DrawLightningLine(Vector3 start, Vector3 end)
    {
        GameObject lineObj = new GameObject("LightningLine");
        LineRenderer lr = lineObj.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.SetPosition(0, start);
        lr.SetPosition(1, end);
        lr.startWidth = 0.05f;
        lr.endWidth = 0.05f;
        lr.material = new Material(Shader.Find("Sprites-Default"));
        lr.material.color = new Color(0.6f, 0.8f, 1f);
        lr.useWorldSpace = true;

        // Auto-destroy after a short time
        Destroy(lineObj, 0.15f);
    }
}