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

                // Positional correction (push out to the surface of the thick line)
                pos = closest + normal * effectiveRadius;

                if (isPlayer)
                {
                    // Check if the surface normal is traversable (-90 to 90 degrees relative to up -> normal.y >= 0)
                    if (normal.y < 0)
                    {
                        // Untraversable wall/ceiling (steeper than 90 degrees): push player back out of it horizontally/vertically
                        continue;
                    }

                    // Return the valid surface normal via the velocity parameter for tilting
                    velocity = normal;
                    pos = closest + normal * effectiveRadius;
                }
                else
                {
                    // Velocity response
                    float dot = Vector2.Dot(velocity, normal);
                    if (dot < 0)
                    {
                        if (isBallBounce)
                        {
                            // 1. Calculate the standard bounce vector first to see where the player wants to deflect
                            Vector2 bouncedVelocity = velocity - 2f * dot * normal;

                            float xVelAbs = Mathf.Abs(bouncedVelocity.x);
                            float yVelAbs = Mathf.Abs(bouncedVelocity.y);

                            float bS = 14f;

                            // 2. Filter by surface type
                            if (normal.y > 0.71f) // Walkable Floor / Shallow Slope
                            {
                                // Apply your speed rules to the resulting deflection
                                if (yVelAbs >= xVelAbs)
                                {
                                    velocity = bouncedVelocity.normalized * bS;
                                }
                                else // yvel is smaller than xvel
                                {
                                    bouncedVelocity.y = bS;
                                    velocity = bouncedVelocity.normalized * bS;
                                }
                            }
                            else if (normal.y > 0) // Steep Wall / Side of a slope pointing up
                            {
                                bouncedVelocity.y = bS;
                                velocity = bouncedVelocity.normalized * bS;
                            }
                            else // Ceiling / Roof (normal.y <= 0)
                            {
                                velocity = bouncedVelocity;
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

        if (pos.y < -48)
        {
            pos = new Vector3(0, 0, pos.z);
            velocity = new Vector2(velocity.x, 0);
        }
    }
}