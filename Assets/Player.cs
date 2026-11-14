using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class Player : MonoBehaviour
{
    [SerializeField] private CameraShake cameraShake;
    [SerializeField] private FlatLandscape flatLandscape;
    private static Sprite cachedHexagonSprite;
    [SerializeField] private BallSpawner ballSpawner;

    public float initPlayerHealth = 1;
    public float playerHealth = 1;
    private float invincibleTimer;

    [SerializeField] private AudioClip shootSound;

    private float shootTimer;

    [SerializeField] private GameObject body;

    private float shootRecoilAmount = 0f;
    private float shootRecoilVelocity = 0f;

    private readonly float shootRecoilImpulse = 7f;
    private readonly float shootRecoilVelocityKept = 0.95f;

    private float phaseTimer;

    float mouseX = 0;

    [SerializeField] private LineRenderer wallPoints;

    void Start()
    {
        ResetPlayer();
    }

    public void ResetPlayer()
    {
        playerHealth = GameManager.Instance.playerHealth;
        mouseX = 0;
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameManager.GameState.Play)
        {
            if (playerHealth > 0f)
            {
                if (invincibleTimer > 0f)
                {
                    invincibleTimer -= Time.deltaTime;
                }
                shootTimer += Time.deltaTime;

                if (Input.GetKey(KeyCode.Mouse0))
                {
                    mouseX = Camera.main.ScreenToWorldPoint(Input.mousePosition).x;
                    mouseX = math.clamp(mouseX, -5.7f, 5.7f);

                    if (shootTimer >= GameManager.Instance.fireRate)
                    {
                        Shoot();
                        shootTimer = 0f;

                        shootRecoilVelocity -= shootRecoilImpulse;
                    }
                }
            }

            if (playerHealth <= 0f)
            {
                Debug.Log("Pdead");
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.ReturnToMenu();
                    playerHealth = 0.001f;
                }
            }
        }
        else
        {
            ResetPlayer();
        }

        shootRecoilVelocity = Mathf.Max(-shootRecoilImpulse, Mathf.Min(shootRecoilImpulse, shootRecoilVelocity));
        shootRecoilAmount += shootRecoilVelocity * Time.deltaTime;
        shootRecoilVelocity = (shootRecoilVelocity - shootRecoilAmount) * shootRecoilVelocityKept;

        body.transform.position =
            new Vector3(
                transform.position.x,
                transform.position.y + shootRecoilAmount,
                transform.position.z
            );

        UpdatePlayer(wallPoints, mouseX);

        phaseTimer += Time.deltaTime;
        phaseTimer %= math.PI * 2;

        Vector2 offset =
            new Vector2(transform.position.x, transform.position.y + 6f) * 0.05f +
            new Vector2(Mathf.Cos(phaseTimer), Mathf.Sin(phaseTimer)) * 0.3f;

        cameraShake.SetOffset(offset);
        flatLandscape.SetOffset(offset * 0.75f);
    }

    public void UpdatePlayer(LineRenderer wallLineRenderer, float mouseX)
    {
        if (wallLineRenderer == null || wallLineRenderer.positionCount < 2) return;

        int count = wallLineRenderer.positionCount;
        Vector2[] wallPoints = new Vector2[count];

        for (int i = 0; i < count; i++)
        {
            Vector3 p = wallLineRenderer.GetPosition(i);
            wallPoints[i] = new Vector2(p.x, p.y);
        }

        float halfWheelBase = 1.0f;
        float wheelRadius = 0.5f;
        float totalOffset = 1.5f; // Updated to match the new -1.25 wheel rest position
        float moveSpeedFactor = 0.8f;

        // 1. Dynamically find raw traversable X bounds based on segment normals (normal.y >= 0)
        float rawMinX = float.MaxValue;
        float rawMaxX = float.MinValue;

        for (int i = 0; i < wallPoints.Length - 1; i++)
        {
            Vector2 a = wallPoints[i];
            Vector2 b = wallPoints[i + 1];

            Vector2 dir = (b - a).normalized;
            Vector2 normal = new Vector2(-dir.y, dir.x);
            if (normal.y < 0) normal = -normal;

            if (normal.y >= 0)
            {
                rawMinX = Mathf.Min(rawMinX, Mathf.Min(a.x, b.x));
                rawMaxX = Mathf.Max(rawMaxX, Mathf.Max(a.x, b.x));
            }
        }

        if (rawMinX > rawMaxX) return;

        // Pad bounds by wheel offset + radius
        float minTraversableX = rawMinX + halfWheelBase + wheelRadius;
        float maxTraversableX = rawMaxX - halfWheelBase - wheelRadius;
        if (minTraversableX > maxTraversableX)
        {
            minTraversableX = rawMinX;
            maxTraversableX = rawMaxX;
        }

        float clampedMouseX = Mathf.Clamp(mouseX, minTraversableX, maxTraversableX);

        // 2. Find current continuous position on the polyline
        GetClosestPointOnLine(transform.position, wallPoints, out int currentSeg, out float currentT);
        Vector2 currentContact = Vector2.Lerp(wallPoints[currentSeg], wallPoints[currentSeg + 1], currentT);

        // 3. Find target segment and t for the clamped mouseX
        int targetSeg = currentSeg;
        float targetT = currentT;
        FindSegmentAndTForX(clampedMouseX, wallPoints, out targetSeg, out targetT);

        // 4. Traverse segment-by-segment from current to target, checking traversability (normal.y >= 0)
        int finalSeg = currentSeg;
        float finalT = currentT;

        int step = (targetSeg >= currentSeg) ? 1 : -1;
        int travIndex = currentSeg;

        while (travIndex != targetSeg)
        {
            int nextIndex = travIndex + step;
            if (nextIndex < 0 || nextIndex >= wallPoints.Length - 1) break;

            Vector2 a = wallPoints[nextIndex];
            Vector2 b = wallPoints[nextIndex + 1];

            Vector2 dir = (b - a).normalized;
            Vector2 normal = new Vector2(-dir.y, dir.x);
            if (normal.y < 0) normal = -normal;

            // Blocked by non-traversable wall/ceiling
            if (normal.y < 0) break;

            travIndex = nextIndex;
            finalSeg = travIndex;

            if (travIndex == targetSeg)
            {
                finalT = targetT;
            }
            else
            {
                finalT = (step > 0) ? 1f : 0f;
            }
        }

        if (travIndex == targetSeg && finalSeg == targetSeg)
        {
            finalT = targetT;
        }

        Vector2 desiredContact = Vector2.Lerp(wallPoints[finalSeg], wallPoints[finalSeg + 1], finalT);

        // 5. Smoothly interpolate contact point towards desired target
        Vector2 smoothedContact = Vector2.Lerp(currentContact, desiredContact, moveSpeedFactor);
        int smoothSeg = currentSeg;
        float smoothT = currentT;
        GetClosestPointOnLine(smoothedContact, wallPoints, out smoothSeg, out smoothT);

        // Get center normal for safety fallback
        Vector2 centerSegA = wallPoints[smoothSeg];
        Vector2 centerSegB = wallPoints[smoothSeg + 1];
        Vector2 centerDir = (centerSegB - centerSegA).normalized;
        Vector2 centerNormal = new Vector2(-centerDir.y, centerDir.x);
        if (centerNormal.y < 0) centerNormal = -centerNormal;

        // 6. Sample Left and Right wheels along the arc distance
        Vector2 leftWheel = GetPointAlongArc(wallPoints, smoothSeg, smoothT, -halfWheelBase, out Vector2 normalL, out bool travL);
        Vector2 rightWheel = GetPointAlongArc(wallPoints, smoothSeg, smoothT, halfWheelBase, out Vector2 normalR, out bool travR);

        // 7. Robust Normal Blending (>90 degree angle safeguards)
        Vector2 avgNormal = centerNormal;
        if (travL && travR)
        {
            if (Vector2.Dot(normalL, normalR) >= 0f)
            {
                Vector2 combined = (normalL + normalR).normalized;
                if (combined.sqrMagnitude > 0.001f)
                {
                    avgNormal = combined;
                }
            }
        }
        else if (travL)
        {
            avgNormal = normalL;
        }
        else if (travR)
        {
            avgNormal = normalR;
        }

        if (avgNormal.y < 0.05f) avgNormal = new Vector2(avgNormal.x, 0.05f).normalized;

        // 8. X-Alignment Correction: Adjust position so final center X precisely matches target X
        Vector2 wheelMidpoint = (leftWheel + rightWheel) * 0.5f;
        Vector2 initialCenterPos = wheelMidpoint + (avgNormal * totalOffset);

        // Calculate the X error caused by the normal offset and correct it
        float xError = clampedMouseX - initialCenterPos.x;
        Vector2 correctedContact = smoothedContact + new Vector2(xError, 0);

        // Re-sample with the corrected contact point for exact X alignment
        GetClosestPointOnLine(correctedContact, wallPoints, out smoothSeg, out smoothT);
        leftWheel = GetPointAlongArc(wallPoints, smoothSeg, smoothT, -halfWheelBase, out normalL, out travL);
        rightWheel = GetPointAlongArc(wallPoints, smoothSeg, smoothT, halfWheelBase, out normalR, out travR);
        wheelMidpoint = (leftWheel + rightWheel) * 0.5f;

        Vector2 finalCenterPos = wheelMidpoint + (avgNormal * totalOffset);

        // Apply final position
        transform.position = new Vector3(finalCenterPos.x, finalCenterPos.y, transform.position.z);

        // 9. Orient tracks to wheel slope, keep body pointing straight up
        Vector2 wheelDirection = (rightWheel - leftWheel).normalized;
        if (wheelDirection.sqrMagnitude > 0.0001f)
        {
            float targetAngle = Mathf.Atan2(wheelDirection.y, wheelDirection.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, targetAngle);

            if (this.body != null)
            {
                this.body.transform.rotation = Quaternion.identity; // Points straight up in world space
            }
        }
    }

    // --- Helper Methods ---

    private void GetClosestPointOnLine(Vector2 pos, Vector2[] points, out int bestSeg, out float bestT)
    {
        bestSeg = 0;
        bestT = 0f;
        float minDistSqr = float.MaxValue;

        for (int i = 0; i < points.Length - 1; i++)
        {
            Vector2 a = points[i];
            Vector2 b = points[i + 1];
            Vector2 ab = b - a;
            float lenSqr = ab.sqrMagnitude;

            float t = 0f;
            if (lenSqr > 0.0001f)
            {
                t = Vector2.Dot(pos - a, ab) / lenSqr;
                t = Mathf.Clamp01(t);
            }

            Vector2 p = a + ab * t;
            float distSqr = (pos - p).sqrMagnitude;

            if (distSqr < minDistSqr)
            {
                minDistSqr = distSqr;
                bestSeg = i;
                bestT = t;
            }
        }
    }

    private void FindSegmentAndTForX(float targetX, Vector2[] points, out int targetSeg, out float targetT)
    {
        targetSeg = 0;
        targetT = 0f;

        for (int i = 0; i < points.Length - 1; i++)
        {
            float minX = Mathf.Min(points[i].x, points[i + 1].x);
            float maxX = Mathf.Max(points[i].x, points[i + 1].x);

            if (targetX >= minX && targetX <= maxX)
            {
                targetSeg = i;
                float lengthX = points[i + 1].x - points[i].x;
                targetT = Mathf.Abs(lengthX) < 0.0001f ? 0f : (targetX - points[i].x) / lengthX;
                targetT = Mathf.Clamp01(targetT);
                return;
            }
        }

        if (targetX < points[0].x)
        {
            targetSeg = 0;
            targetT = 0f;
        }
        else
        {
            targetSeg = points.Length - 2;
            targetT = 1f;
        }
    }

    private Vector2 GetPointAlongArc(Vector2[] points, int startSeg, float startT, float distanceOffset, out Vector2 normal, out bool traversable)
    {
        int currentSeg = startSeg;
        float currentT = startT;
        float remainingDist = Mathf.Abs(distanceOffset);
        int dir = (distanceOffset >= 0) ? 1 : -1;

        normal = Vector2.up;
        traversable = true;

        while (remainingDist > 0f)
        {
            Vector2 a = points[currentSeg];
            Vector2 b = points[currentSeg + 1];
            float segLen = Vector2.Distance(a, b);
            float currentSegCovered = segLen * currentT;

            if (dir > 0)
            {
                float distTopping = segLen - currentSegCovered;
                if (remainingDist <= distTopping)
                {
                    currentT += remainingDist / segLen;
                    remainingDist = 0f;
                }
                else
                {
                    remainingDist -= distTopping;
                    if (currentSeg < points.Length - 2)
                    {
                        currentSeg++;
                        currentT = 0f;
                    }
                    else
                    {
                        currentT = 1f;
                        remainingDist = 0f;
                    }
                }
            }
            else
            {
                if (remainingDist <= currentSegCovered)
                {
                    currentT -= remainingDist / segLen;
                    remainingDist = 0f;
                }
                else
                {
                    remainingDist -= currentSegCovered;
                    if (currentSeg > 0)
                    {
                        currentSeg--;
                        currentT = 1f;
                    }
                    else
                    {
                        currentT = 0f;
                        remainingDist = 0f;
                    }
                }
            }
        }

        Vector2 segA = points[currentSeg];
        Vector2 segB = points[currentSeg + 1];
        Vector2 resultPos = Vector2.Lerp(segA, segB, Mathf.Clamp01(currentT));

        Vector2 segDir = (segB - segA).normalized;
        normal = new Vector2(-segDir.y, segDir.x);
        if (normal.y < 0) normal = -normal;

        traversable = (normal.y >= 0);
        return resultPos;
    }

    private void Shoot()
    {
        Sprite CreatePolygonSprite(int sides, float radius, Color color, int resolution)
        {
            int diameter = resolution;
            Texture2D texture = new Texture2D(diameter, diameter, TextureFormat.RGBA32, false);

            // Use a flat color array for fast bulk processing
            Color32[] pixels = new Color32[diameter * diameter];
            Color32 clearColor = new Color32(0, 0, 0, 0);
            Color32 fillColor = color;

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = clearColor;

            Vector2 center = new Vector2(diameter / 2f, diameter / 2f);
            float radiusPixels = diameter / 2f - 2f;

            // Calculate vertices
            Vector2[] vertices = new Vector2[sides];
            for (int i = 0; i < sides; i++)
            {
                float angle = i * 2f * Mathf.PI / sides - Mathf.PI / 2f;
                vertices[i] = new Vector2(
                    center.x + radiusPixels * Mathf.Cos(angle),
                    center.y + radiusPixels * Mathf.Sin(angle)
                );
            }

            // Fill triangles from the center to each edge
            for (int i = 0; i < sides; i++)
            {
                FillSolidTriangle(pixels, diameter, center, vertices[i], vertices[(i + 1) % sides], fillColor);
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            texture.filterMode = FilterMode.Bilinear;

            return Sprite.Create(texture, new Rect(0, 0, diameter, diameter), new Vector2(0.5f, 0.5f), diameter / (radius * 2f));
        }

        void FillSolidTriangle(Color32[] pixels, int diameter, Vector2 center, Vector2 v1, Vector2 v2, Color32 color)
        {
            int minX = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(center.x, v1.x, v2.x)), 0, diameter - 1);
            int maxX = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(center.x, v1.x, v2.x)), 0, diameter - 1);
            int minY = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(center.y, v1.y, v2.y)), 0, diameter - 1);
            int maxY = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(center.y, v1.y, v2.y)), 0, diameter - 1);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Vector2 p = new Vector2(x, y);
                    if (IsPointInTriangle(p, center, v1, v2))
                    {
                        pixels[y * diameter + x] = color;
                    }
                }
            }
        }

        bool IsPointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float denom = ((b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y));
            if (Mathf.Abs(denom) < 0.0001f) return false;
            float alpha = ((b.y - c.y) * (p.x - c.x) + (c.x - b.x) * (p.y - c.y)) / denom;
            float beta = ((c.y - a.y) * (p.x - c.x) + (a.x - c.x) * (p.y - c.y)) / denom;
            float gamma = 1f - alpha - beta;
            return alpha >= 0 && beta >= 0 && gamma >= 0;
        }

        Sprite GetHexagonSprite()
        {
            if (cachedHexagonSprite == null)
            {
                cachedHexagonSprite = CreatePolygonSprite(6, 0.23f, new Color(1f, 1f, 0f), 32);
            }
            return cachedHexagonSprite;
        }

        GameObject bullet = new("HexagonBullet");
        bullet.transform.position = transform.position + new Vector3(0, 1, 0);

        SpriteRenderer spriteRenderer = bullet.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = GetHexagonSprite();

        bullet.AddComponent<CircleCollider2D>();
        bullet.AddComponent<Rigidbody2D>();
        bullet.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Dynamic;
        bullet.GetComponent<Rigidbody2D>().collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        bullet.AddComponent<Bullet>();
        bullet.GetComponent<Bullet>().Initialize(
            GameManager.Instance.bulletDamage,
            GameManager.Instance.lightningDamage,
            GameManager.Instance.lightningBounces,
            GameManager.Instance.poisonDamagePerSec,
            GameManager.Instance.poisonDuration,
            ballSpawner
        );

        Vector3 soundPosition = Camera.main.transform.position +
            new Vector3(transform.position.x, transform.position.y, 0) * 0.1f;
        float soundPitch = UnityEngine.Random.Range(0.95f, 1.05f);
        AudioHelper.PlayClipAtPoint(shootSound, soundPosition, 1f, soundPitch);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.TryGetComponent<Ball>(out _)) return;
        if (invincibleTimer > 0) return;
        
        Debug.Log("Pdamage");
        playerHealth -= 1f;
        invincibleTimer = 0.3f;
    }
}