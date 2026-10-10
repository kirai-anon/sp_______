using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class Player : MonoBehaviour
{
    [SerializeField] private CameraShake cameraShake;
    [SerializeField] private FlatLandscape flatLandscape;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private BallSpawner ballSpawner;

    [SerializeField] public float initPlayerHealth = 1;
    
    public float playerHealth = 1;
    private float invincibleTimer;

    [SerializeField] private AudioClip shootSound;

    private float shootTimer;

    [SerializeField] private GameObject body;

    private float wobbleTimer;

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
                    }

                    wobbleTimer += Time.deltaTime;
                    wobbleTimer %= math.PI * 2;
                    body.transform.position =
                        new Vector3(
                            transform.position.x,
                            transform.position.y + math.cos(wobbleTimer * 29) * 0.13f,
                            transform.position.z
                        );
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

        if (!Input.GetKey(KeyCode.Mouse0))
        {
            wobbleTimer *= 0.8f;
        }

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

        // 1. Extract Vector2 points from the LineRenderer and find absolute line bounds
        int count = wallLineRenderer.positionCount;
        Vector2[] wallPoints = new Vector2[count];
        float minLineX = float.MaxValue;
        float maxLineX = float.MinValue;

        for (int i = 0; i < count; i++)
        {
            Vector3 p = wallLineRenderer.GetPosition(i);
            wallPoints[i] = new Vector2(p.x, p.y);
            if (p.x < minLineX) minLineX = p.x;
            if (p.x > maxLineX) maxLineX = p.x;
        }

        float halfWheelBase = 1.0f;
        float totalOffset = 0.5f + 0.75f; // 1.25 units total from wheel midpoint to visual center

        // 2. Smoothly move the car's center X directly towards the mouse (no normal feedback loop = no jitter)
        float currentX = transform.position.x;
        float targetX = Mathf.Lerp(currentX, mouseX, 0.2f);

        // Clamp center X so the wheels never fall off the ends of the line
        targetX = Mathf.Clamp(targetX, minLineX + halfWheelBase, maxLineX - halfWheelBase);

        // 3. Robust, fail-safe line sampling method for the wheels
        bool GetWheelContact(float queryX, out Vector2 contactPoint, out Vector2 normal, out bool traversable)
        {
            contactPoint = Vector2.zero;
            normal = Vector2.up;
            traversable = false;

            if (wallPoints.Length < 2) return false;

            // Clamp queryX to line bounds to prevent out-of-bounds errors or corner sticking
            queryX = Mathf.Clamp(queryX, minLineX, maxLineX);

            int bestSegIndex = 0;
            float bestT = 0f;
            float minDst = float.MaxValue;

            for (int i = 0; i < wallPoints.Length - 1; i++)
            {
                Vector2 a = wallPoints[i];
                Vector2 b = wallPoints[i + 1];
                float segMinX = Mathf.Min(a.x, b.x);
                float segMaxX = Mathf.Max(a.x, b.x);

                if (queryX >= segMinX - 0.01f && queryX <= segMaxX + 0.01f)
                {
                    bestSegIndex = i;
                    float lengthX = b.x - a.x;
                    bestT = Mathf.Abs(lengthX) < 0.0001f ? 0f : (queryX - a.x) / lengthX;
                    bestT = Mathf.Clamp01(bestT);
                    break;
                }

                float midX = (a.x + b.x) * 0.5f;
                float dst = Mathf.Abs(queryX - midX);
                if (dst < minDst)
                {
                    minDst = dst;
                    bestSegIndex = i;
                    float lengthX = b.x - a.x;
                    bestT = Mathf.Abs(lengthX) < 0.0001f ? 0f : (queryX - a.x) / lengthX;
                    bestT = Mathf.Clamp01(bestT);
                }
            }

            Vector2 segA = wallPoints[bestSegIndex];
            Vector2 segB = wallPoints[bestSegIndex + 1];
            float y = Mathf.Lerp(segA.y, segB.y, bestT);
            contactPoint = new Vector2(queryX, y);

            Vector2 dir = (segB - segA).normalized;
            normal = new Vector2(-dir.y, dir.x);
            if (normal.y < 0) normal = -normal;

            // Traversable rule: -90 to 90 degrees relative to up
            traversable = (normal.y >= 0);
            return true;
        }

        // 4. Position the left and right wheels relative to the stable targetX
        float leftX = targetX - halfWheelBase;
        float rightX = targetX + halfWheelBase;

        GetWheelContact(leftX, out Vector2 leftWheel, out Vector2 normalL, out bool travL);
        GetWheelContact(rightX, out Vector2 rightWheel, out Vector2 normalR, out bool travR);

        // 5. Calculate final position and rotation smoothly
        Vector2 wheelMidpoint = (leftWheel + rightWheel) * 0.5f;
        Vector2 avgNormal = ((travL ? normalL : Vector2.up) + (travR ? normalR : Vector2.up)).normalized;

        // Apply stable X from mouse lerp, and calculate Y using the wheel midpoint + offset
        Vector3 pos = new Vector3(targetX, wheelMidpoint.y + (avgNormal.y * totalOffset), transform.position.z);

        // Smoothly apply position
        transform.position = pos;

        // Orient the car based on the wheel vector
        Vector2 wheelDirection = (rightWheel - leftWheel).normalized;
        if (wheelDirection.sqrMagnitude > 0.0001f)
        {
            float targetAngle = Mathf.Atan2(wheelDirection.y, wheelDirection.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, targetAngle);
            this.body.transform.rotation = Quaternion.Euler(0, 0, 0);
        }
    }

    private void Shoot()
    {
        GameObject bullet = Instantiate(bulletPrefab, transform.position + new Vector3(0, 1, 0), Quaternion.identity);
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