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

    // --- Surface-following settings/state (add these to the class fields) ---
    [SerializeField] private float maxClimbAngle = 60f;          // steeper than this counts as a wall
    [SerializeField] private bool invertSurfaceWinding = false;  // tick if the line is drawn with solid ground on its LEFT
    [SerializeField] private float followSharpness = 10f;        // how quickly the contact X chases the mouse
    [SerializeField] private float maxMoveSpeed = 12f;           // world units per second along X

    private int contactSeg = -1;     // segment the vehicle center is touching
    private float contactT = 0f;     // position along that segment (0..1)
    private int lastPointCount = -1;

    public void UpdatePlayer(LineRenderer wallLineRenderer, float mouseX)
    {
        if (wallLineRenderer == null || wallLineRenderer.positionCount < 2) return;

        int count = wallLineRenderer.positionCount;
        Vector2[] wallPoints = new Vector2[count];
        bool lineIsLocal = !wallLineRenderer.useWorldSpace;
        Transform lineTf = wallLineRenderer.transform;

        for (int i = 0; i < count; i++)
        {
            Vector3 p = wallLineRenderer.GetPosition(i);
            if (lineIsLocal) p = lineTf.TransformPoint(p);
            wallPoints[i] = new Vector2(p.x, p.y);
        }

        float halfWheelBase = 1.0f;
        float wheelRadius = 0.35f;
        float totalOffset = 1.6f;
        float minNormalY = Mathf.Cos(maxClimbAngle * Mathf.Deg2Rad);

        // 1. Acquire (or re-acquire) the contact point on the surface
        if (count != lastPointCount || contactSeg < 0 || contactSeg > count - 2)
        {
            lastPointCount = count;
            if (!FindInitialContact(wallPoints, transform.position, totalOffset, minNormalY, out contactSeg, out contactT))
            {
                contactSeg = -1;
                return;
            }
        }

        // 2. Chase mouseX from the stored contact X (not transform.position.x, which is offset along the normal)
        Vector2 segA = wallPoints[contactSeg];
        Vector2 segB = wallPoints[contactSeg + 1];
        float currentX = Mathf.Lerp(segA.x, segB.x, contactT);
        float dt = Time.deltaTime;
        float desiredX = Mathf.Lerp(currentX, mouseX, 1f - Mathf.Exp(-followSharpness * dt));
        desiredX = Mathf.MoveTowards(currentX, desiredX, maxMoveSpeed * dt);

        // Walk along the polyline itself; stops at the first wall / overhang / line end
        AdvanceContactToX(wallPoints, desiredX, minNormalY, ref contactSeg, ref contactT);

        // 3. Keep both wheels (plus radius) on drivable ground: if one side runs out, back the center off
        float reach = halfWheelBase + wheelRadius;
        int s = contactSeg; float t = contactT;
        WalkAlongSurface(wallPoints, ref s, ref t, reach, minNormalY, out float shortFwd);
        s = contactSeg; t = contactT;
        WalkAlongSurface(wallPoints, ref s, ref t, -reach, minNormalY, out float shortBack);

        if (shortFwd > 0f && shortBack <= 0f)
        {
            WalkAlongSurface(wallPoints, ref contactSeg, ref contactT, -shortFwd, minNormalY, out _);
        }
        else if (shortBack > 0f && shortFwd <= 0f)
        {
            WalkAlongSurface(wallPoints, ref contactSeg, ref contactT, shortBack, minNormalY, out _);
        }

        // 4. Wheel contact points along the surface
        s = contactSeg; t = contactT;
        Vector2 backWheel = WalkAlongSurface(wallPoints, ref s, ref t, -halfWheelBase, minNormalY, out _);
        s = contactSeg; t = contactT;
        Vector2 frontWheel = WalkAlongSurface(wallPoints, ref s, ref t, halfWheelBase, minNormalY, out _);

        Vector2 leftWheel = invertSurfaceWinding ? frontWheel : backWheel;
        Vector2 rightWheel = invertSurfaceWinding ? backWheel : frontWheel;

        // 5. Pose from the wheel chord: offset and rotation now use the same "up"
        Vector2 chord = rightWheel - leftWheel;
        if (chord.sqrMagnitude < 0.0001f) return;

        Vector2 wheelDirection = chord.normalized;
        Vector2 hullUp = new Vector2(-wheelDirection.y, wheelDirection.x);

        Vector2 wheelMidpoint = (leftWheel + rightWheel) * 0.5f;
        Vector2 targetCenterPos = wheelMidpoint + (hullUp * totalOffset);

        transform.position = new Vector3(targetCenterPos.x, targetCenterPos.y, transform.position.z);

        float targetAngle = Mathf.Atan2(wheelDirection.y, wheelDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, targetAngle);

        if (this.body != null)
        {
            this.body.transform.rotation = Quaternion.identity;
        }
}

    // --- Helper Methods ---

    // Outward normal from the line's winding. Deliberately NOT flipped upward,
    // so overhang undersides point down and walls point sideways.
    private Vector2 GetSurfaceNormal(Vector2 a, Vector2 b)
    {
        Vector2 d = b - a;
        if (d.sqrMagnitude < 0.00000001f) return Vector2.up;
        d.Normalize();
        Vector2 n = new Vector2(-d.y, d.x);
        return invertSurfaceWinding ? -n : n;
    }

    private bool IsDrivable(Vector2[] points, int seg, float minNormalY)
    {
        Vector2 d = points[seg + 1] - points[seg];
        if (d.sqrMagnitude < 0.00000001f) return true; // zero-length segment: pass through
        return GetSurfaceNormal(points[seg], points[seg + 1]).y >= minNormalY;
    }

    private bool FindInitialContact(Vector2[] points, Vector3 position, float offset, float minNormalY, out int seg, out float t)
    {
        seg = -1;
        t = 0f;
        Vector2 probe = new Vector2(position.x, position.y - offset);
        float bestSqr = float.MaxValue;

        for (int i = 0; i < points.Length - 1; i++)
        {
            if (!IsDrivable(points, i, minNormalY)) continue;

            Vector2 a = points[i];
            Vector2 ab = points[i + 1] - a;
            float lenSqr = ab.sqrMagnitude;
            float tt = lenSqr < 0.00000001f ? 0f : Mathf.Clamp01(Vector2.Dot(probe - a, ab) / lenSqr);
            float dSqr = (a + ab * tt - probe).sqrMagnitude;

            if (dSqr < bestSqr)
            {
                bestSqr = dSqr;
                seg = i;
                t = tt;
            }
        }

        return seg >= 0;
    }

    // Moves the contact along the polyline toward targetX, segment by segment.
    // Drivable segments always run in one X direction, so index order maps cleanly to X.
    private void AdvanceContactToX(Vector2[] points, float targetX, float minNormalY, ref int seg, ref float t)
    {
        int last = points.Length - 2;
        int fwdSign = invertSurfaceWinding ? -1 : 1;

        for (int guard = 0; guard <= points.Length; guard++)
        {
            Vector2 a = points[seg];
            Vector2 b = points[seg + 1];
            float dx = b.x - a.x;

            if (Mathf.Abs(dx) > 0.00001f)
            {
                float tt = (targetX - a.x) / dx;
                if (tt >= 0f && tt <= 1f)
                {
                    t = tt;
                    return;
                }
            }

            float x = Mathf.Lerp(a.x, b.x, t);
            int step = ((targetX > x) == (fwdSign > 0)) ? 1 : -1;
            int next = seg + step;

            if (next < 0 || next > last || !IsDrivable(points, next, minNormalY))
            {
                t = (step > 0) ? 1f : 0f;
                return;
            }

            seg = next;
            t = (step > 0) ? 0f : 1f;
        }
    }

    // Walks an arc distance along the surface (positive = forward in index order).
    // Stops at non-drivable segments or line ends; shortfall = distance that could not be covered.
    private Vector2 WalkAlongSurface(Vector2[] points, ref int seg, ref float t, float distance, float minNormalY, out float shortfall)
    {
        int last = points.Length - 2;
        int step = (distance >= 0f) ? 1 : -1;
        float remaining = Mathf.Abs(distance);

        while (remaining > 0f)
        {
            float segLen = Vector2.Distance(points[seg], points[seg + 1]);
            float available = (step > 0) ? segLen * (1f - t) : segLen * t;

            if (segLen > 0.00001f && remaining <= available)
            {
                t += step * (remaining / segLen);
                remaining = 0f;
                break;
            }

            remaining -= available;
            int next = seg + step;

            if (next < 0 || next > last || !IsDrivable(points, next, minNormalY))
            {
                t = (step > 0) ? 1f : 0f;
                break;
            }

            seg = next;
            t = (step > 0) ? 0f : 1f;
        }

        t = Mathf.Clamp01(t);
        shortfall = Mathf.Max(0f, remaining);
        return Vector2.Lerp(points[seg], points[seg + 1], t);
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