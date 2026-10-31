using UnityEngine;

public static class LineCollisionHelper
{
    public static void ResolveCollisions(ref Vector3 pos, ref Vector2 velocity, float radius, Vector2[] wallPoints, bool isBallBounce = false, float lineThickness = 0f, bool isPlayer = false)
    {
        if (wallPoints == null || wallPoints.Length < 2) return;

        // The effective collision radius combines the object's radius and half the line's thickness
        float effectiveRadius = radius + (lineThickness * 0.5f);
        float effectiveRadiusSqr = effectiveRadius * effectiveRadius;

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

            if (sqrDist < effectiveRadiusSqr)
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

                pos = closest + normal * effectiveRadius;

                if (isPlayer)
                {
                    if (normal.y < 0)
                    {
                        continue;
                    }

                    velocity = normal;
                    pos = closest + normal * effectiveRadius;
                }
                else
                {
                    float dot = Vector2.Dot(velocity, normal);
                    if (dot < 0)
                    {
                        if (isBallBounce)
                        {
                            Vector2 bounceVel = velocity - 2f * dot * normal;

                            float yVelAb = Mathf.Abs(bounceVel.normalized.y);
                            float yLim = 0.8f;

                            float bS = 14f;
                            float xLim = 9f;

                            if (normal.y > 0)
                            {
                                if (yVelAb >= yLim && yVelAb > Mathf.Abs(bounceVel.normalized.x))
                                {
                                    velocity = bounceVel.normalized * bS;
                                }
                                else
                                {
                                    float xVel;
                                    if (bounceVel.x >= 0) xVel = Mathf.Min(bounceVel.x, xLim);
                                    else /* align text */ xVel = Mathf.Max(bounceVel.x, -xLim);
                                    velocity = (new Vector2(xVel, bS)).normalized * bS;
                                }
                            }
                            else // ceiling
                            {
                                velocity = bounceVel;
                            }
                        }
                        else
                        {
                            velocity -= (1f + 0.5f) * dot * normal;
                        }
                    }
                }
            }
        }

        if (pos.y < -48)
        {
            pos = new Vector3(0, 0, pos.z);
            velocity = new Vector2(velocity.x, 0);
        }
    }
}