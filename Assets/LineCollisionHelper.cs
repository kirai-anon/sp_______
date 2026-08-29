using UnityEngine;

public static class LineCollisionHelper
{
    public static void ResolveCollisions(ref Vector3 pos, ref Vector2 velocity, float radius, Vector2[] wallPoints, bool isBallBounce = false)
    {
        if (wallPoints == null || wallPoints.Length < 2) return;

        for (int i = 0; i < wallPoints.Length - 1; i++)
        {
            Vector2 a = wallPoints[i];
            Vector2 b = wallPoints[i + 1];

            Vector2 pa = (Vector2)pos - a;
            Vector2 ba = b - a;
            float baSqrLen = Vector2.Dot(ba, ba);
            if (baSqrLen < 0.0001f) continue;

            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / baSqrLen);
            Vector2 closest = a + ba * h;

            Vector2 normal = (Vector2)pos - closest;
            float sqrDist = normal.sqrMagnitude;

            if (sqrDist < radius * radius)
            {
                float dist = Mathf.Sqrt(sqrDist);
                if (dist > 0.0001f)
                {
                    normal /= dist;
                }
                else
                {
                    normal = new Vector2(-ba.y, ba.x).normalized;
                }

                // Positional correction (push out of wall)
                pos = closest + normal * radius;

                // Velocity response
                float dot = Vector2.Dot(velocity, normal);
                if (dot < 0)
                {
                    if (isBallBounce)
                    {
                        // Custom ball behavior (e.g., floor bounce boost vs wall reflection)
                        if (Mathf.Abs(normal.y) > 0.7f && normal.y > 0) // roughly pointing up (floor)
                        {
                            velocity -= 2f * dot * normal;
                            velocity.y = 12.0f;
                        }
                        else
                        {
                            velocity -= 2f * dot * normal;
                        }
                    }
                    else
                    {
                        // Currency dampening bounce
                        velocity -= (1f + 0.5f) * dot * normal;
                    }
                }
            }
        }
    }
}