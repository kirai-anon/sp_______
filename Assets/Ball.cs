using UnityEngine;
using System.Collections.Generic;

public class Ball : MonoBehaviour
{
    private BallType type;
    private float radius;
    private Color color;
    private float health;
    private float maxHealth;
    private int sides;

    private int baseStrength;

    private SpriteRenderer spriteRenderer;
    private Coroutine damageFlashCoroutine;

    private Vector2 velocity;
    private float spinVelocity;
    private TextMesh healthText;
    private BallSpawner spawner;

    // Poison state
    private List<PoisonEffect> poisonEffects = new List<PoisonEffect>();

    public float Radius => radius;
    public float Health => health;
    public float MaxHealth => maxHealth;

    private AudioClip[] audioClips;

    private Coroutine spawnScaleCoroutine;
    private float spawnScaleDuration = 0.15f;

    private CameraShake cameraShake;

    private GameObject activeDamageNumberObj;
    private DamageNumberAnimator activeDamageAnimator;
    private float lastDamageNumberTime;
    private const float damageStackTimeLimit = 0.4f;

    private Canvas hudCanvas;

    public void Initialize(BallType ballType, int resolution, BallSpawner ballSpawner, int hpMult, AudioClip[] ballSounds, CameraShake cameraControl, Canvas canvas)
    {
        type = ballType;
        spawner = ballSpawner;

        cameraShake = cameraControl;

        audioClips = ballSounds;

        hudCanvas = canvas;

        switch (type)
        {
            default:                 sides = 12; color = new Color(  1f, 0.5f, 0.2f); radius = 1.0f; health = 1;  break; // default: dodecagon
            case BallType.Tetragon:  sides = 4;  color = new Color(0.4f,   1f, 0.4f); radius = 1.2f; health = 3;  break;
            case BallType.Pentagon:  sides = 5;  color = new Color(  1f, 0.4f, 0.7f); radius = 1.5f; health = 5;  break;
            case BallType.Octagon:   sides = 8;  color = new Color(  1f, 0.4f, 0.4f); radius = 1.5f; health = 8;  break;
            case BallType.Decagon:   sides = 10; color = new Color(  1f,   1f, 0.4f); radius = 1.5f; health = 10; break;

            // bosses
            case BallType.Icotetrasagon:       sides = 24;  color = new Color(  1f, 0.7f, 0.3f); radius = 2.0f; health = 24;   break;
            case BallType.Hexacontatetragon:   sides = 32;  color = new Color(  1f, 0.1f, 0.9f); radius = 3.0f; health = 64;   break;
            case BallType.Chiliaicositetragon: sides = 64;  color = new Color(0.3f,   1f,   1f); radius = 4.0f; health = 1028; break;
            case BallType.Hexacontapentachiliapentacosiatriacontahexagon:
                                               sides = 48;  color = new Color(  1f, 0.4f,   1f); radius = 5.0f; health = 65536; break;
            case BallType.Hexadecamegaheptacosiaheptacontaheptachiliadiacosiahexadecagon:
                                               sides = 20;  color = new Color(0.7f,   1f,   1f); radius = 4.0f; health = 16777216; break;
            case BallType.Disgigahectatetracontaheptamegatetractamyriatriacontaoctachiliahexahectatetracontaheptagon:
                                               sides = 16;  color = new Color(0.4f,   1f, 0.4f); radius = 5.0f; health = 2147483647; break;
        }
        
        if (health <= 3) {
            baseStrength = 1;
        } else if (health <= 8) {
            baseStrength = 2;
        } else if (health <= 24) {
            baseStrength = 3;
        } else {
            baseStrength = 4;
        }

        health = health * hpMult;
        maxHealth = health;

        SpriteRenderer sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = CreatePolygonSprite(sides, radius, color, resolution);
        sr.color = color; 
        spriteRenderer = sr;

        // Health text
        GameObject textObj = new GameObject("HealthText");
        textObj.transform.SetParent(transform);
        textObj.transform.localPosition = new Vector3(0, 0, -1);
        healthText = textObj.AddComponent<TextMesh>();
        healthText.text = health.ToString();
        healthText.fontSize = 50;
        healthText.color = Color.white;
        healthText.anchor = TextAnchor.MiddleCenter;
        healthText.alignment = TextAlignment.Center;
        healthText.characterSize = 0.1f;

        // Collider for bullet hits
        CircleCollider2D col = gameObject.AddComponent<CircleCollider2D>();
        col.radius = radius;
        col.isTrigger = true;

        // Velocity
        velocity = new Vector2(Random.Range(-3f, 3f), Random.Range(2f, 5f));
        spinVelocity = -velocity.x;

        spawnScaleCoroutine = StartCoroutine(ScaleUpOnSpawn());
    }

    public void UpdatePhysics(float dt, float gravity, Vector2[] wallPoints)
    {
        velocity.y -= gravity * dt;

        // Sub-step position and collisions to prevent tunneling
        int subSteps = 4;
        float subDt = dt / subSteps;

        for (int i = 0; i < subSteps; i++)
        {
            Vector3 pos = transform.position;
            pos.x += velocity.x * subDt;
            pos.y += velocity.y * subDt;

            LineCollisionHelper.ResolveCollisions(ref pos, ref velocity, radius, wallPoints, true, 0.1f);

            transform.position = pos;
        }

        transform.Rotate(0, 0, spinVelocity * dt * 31.41f);
        healthText.transform.rotation = Quaternion.Euler(0, 0, 0);

        UpdatePoison(dt);
    }

    private System.Collections.IEnumerator ScaleUpOnSpawn()
    {
        float elapsedTime = 0f;
        Vector3 targetScale = Vector3.one;

        // Start at absolute zero scale
        transform.localScale = Vector3.zero;

        while (elapsedTime < spawnScaleDuration)
        {
            elapsedTime += Time.deltaTime;

            // Smoothly interpolate from zero to full scale
            transform.localScale = Vector3.Lerp(Vector3.zero, targetScale, elapsedTime / spawnScaleDuration);

            yield return null;
        }

        // Ensure it perfectly lands on final size
        transform.localScale = targetScale;
    }


    // ---- Damage ----

    public void TakeDamage(float damage, DamageType damageType)
    {
        health -= damage;
        healthText.text = Mathf.RoundToInt(health).ToString();

        if (health <= 0)
        {
            AudioSource.PlayClipAtPoint(audioClips[4 + baseStrength], Camera.main.transform.position, 1.0f);
            cameraShake.TriggerShake(0.3f, 0.15f * baseStrength);//duration,strenght
        }
        else
        {
            AudioSource.PlayClipAtPoint(audioClips[baseStrength], Camera.main.transform.position, 1.0f);
        }

        // Flash effect
        if (damageFlashCoroutine != null)
            StopCoroutine(damageFlashCoroutine);
        damageFlashCoroutine = StartCoroutine(DamageFlash());

        // Spawn floating damage number dynamically
        SpawnDamageNumber(damage, damageType);

        if (health <= 0) DestroyBall();
        else { spawnHitParticles(transform.position); }
    }

    private void SpawnDamageNumber(float damage, DamageType damageType)
    {
        float timeSinceLastSpawn = Time.time - lastDamageNumberTime;

        // Check if we can stack onto the existing active damage number
        if (activeDamageAnimator != null && timeSinceLastSpawn <= damageStackTimeLimit)
        {
            activeDamageAnimator.AddDamage(damage);
            lastDamageNumberTime = Time.time;
            return;
        }

        // Fallback if not assigned in Inspector
        if (hudCanvas == null)
        {
            hudCanvas = FindFirstObjectByType<Canvas>();
        }
        if (hudCanvas == null || Camera.main == null) return;

        // Create the damage number GameObject
        GameObject textObj = new GameObject("DamageNumber");
        textObj.transform.SetParent(hudCanvas.transform, false);

        RectTransform rectTransform = textObj.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);

        // 1. Get the ball's world position with an upward offset
        Vector3 worldPos = transform.position + (Vector3.up * 0.5f);

        // 2. CRITICAL FIX: Always use Camera.main to convert 3D world space to screen pixels
        Vector3 screenPoint = Camera.main.WorldToScreenPoint(worldPos);

        // If the ball is behind the camera, don't spawn the number
        if (screenPoint.z < 0)
        {
            Destroy(textObj);
            return;
        }

        // 3. Convert screen pixels to the Canvas's local coordinate space
        Camera uiCamera = (hudCanvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : hudCanvas.worldCamera;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(hudCanvas.transform as RectTransform, screenPoint, uiCamera, out Vector2 localPoint))
        {
            rectTransform.anchoredPosition = localPoint;
        }

        // Configure TextMeshPro
        TMPro.TextMeshProUGUI tmp = textObj.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.fontSize = 24; // Will scale dynamically with your 720x720 Reference Resolution CanvasScaler
        tmp.alignment = TMPro.TextAlignmentOptions.Center;

        // Apply custom colors based on damage type
        switch (damageType)
        {
            case DamageType.Lightning:
                tmp.color = Color.lightCyan;
                break;
            case DamageType.Poison:
                tmp.color = Color.mediumPurple;
                break;
            case DamageType.Normal:
            default:
                tmp.color = Color.softRed;
                break;
        }

        activeDamageNumberObj = textObj;
        activeDamageAnimator = textObj.AddComponent<DamageNumberAnimator>();

        // Set initial damage value and update timestamp
        activeDamageAnimator.SetInitialDamage(damage);
        lastDamageNumberTime = Time.time;
    }

    public class DamageNumberAnimator : MonoBehaviour
    {
        private readonly float duration = 1.2f;
        private float elapsed = 0f;
        private float vx;
        private float vy;
        private RectTransform rect;
        private TMPro.TextMeshProUGUI tmp;
        private float accumulatedDamage = 0f;

        void Start()
        {
            rect = GetComponent<RectTransform>();
            tmp = GetComponent<TMPro.TextMeshProUGUI>();

            vx = UnityEngine.Random.Range(-50f, 50f);
            vy = UnityEngine.Random.Range(150f, 250f);
        }

        public void SetInitialDamage(float damage)
        {
            accumulatedDamage = damage;
            UpdateTextDisplay();
        }

        public void AddDamage(float damage)
        {
            accumulatedDamage += damage;
            // Optional: Reset the fade/elapsed time slightly or keep it running smoothly
            elapsed = Mathf.Max(0f, elapsed - 0.2f);
            UpdateTextDisplay();
        }

        private void UpdateTextDisplay()
        {
            if (tmp != null)
            {
                tmp.text = Mathf.Abs(Mathf.RoundToInt(accumulatedDamage)).ToString();
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            elapsed += dt;

            if (elapsed >= duration)
            {
                Destroy(gameObject);
                return;
            }

            vy -= 400f * dt;
            rect.localPosition += new Vector3(vx * dt, vy * dt, 0);

            if (tmp != null)
            {
                Color c = tmp.color;
                c.a = 1f - (elapsed / duration);
                tmp.color = c;
            }
        }
    }

    private System.Collections.IEnumerator DamageFlash()
    {
        float elapsed = 0f;

        // Flash white
        while (elapsed < 0.02f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / 0.02f;
            spriteRenderer.color = Color.Lerp(color, Color.white, t);
            yield return null;
        }

        // Ease into red
        elapsed = 0f;
        while (elapsed < 0.05f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / 0.05f;
            spriteRenderer.color = Color.Lerp(Color.white, Color.red, t);
            yield return null;
        }

        // Back to original
        elapsed = 0f;
        while (elapsed < 0.054f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / 0.05f;
            spriteRenderer.color = Color.Lerp(Color.red, color, t);
            yield return null;
        }

        spriteRenderer.color = color;
    }

    // Poison and lightning bypass armor (full damage regardless)
    public void TakeTrueDamage(float damage, DamageType damageType)
    {
        TakeDamage(damage, damageType); // Currently same, but won't be reduced if armor is added later
    }

    public void DestroyBall()
    {
        int totalValue = Mathf.CeilToInt(maxHealth / 2f) * 500;
        Vector3 pos = transform.position;

        SpawnDeathParticles(pos);

        if (totalValue <= 0) return;

        int exponent = Mathf.CeilToInt(Mathf.Log10(totalValue));
        int currentPower = Mathf.RoundToInt(Mathf.Pow(10, exponent));

        while (totalValue > 0 && currentPower >= 1)
        {
            int count = totalValue / currentPower;
            if (count > 0)
            {
                for (int i = 0; i < count; i++)
                {
                    Vector2 randomVel = velocity + new Vector2(Random.Range(-5f, 5f), Random.Range(2f, 5f));
                    spawner.SpawnCurrencyDrop(pos, randomVel, currentPower);
                }
                totalValue %= currentPower;
            }
            currentPower /= 10;
        }

        switch (type)
        {
            default:
                break; // end instantly

            case BallType.Pentagon:
                for (int i = 0; i < 2; i++) { var b = spawner.SpawnBall(BallType.Dodecagon, pos); }
                break;
            case BallType.Octagon:
                for (int i = 0; i < 2; i++) { var b = spawner.SpawnBall(BallType.Tetragon, pos); }
                break;
            case BallType.Icotetrasagon:
                for (int i = 0; i < 2; i++) { var b = spawner.SpawnBall(BallType.Octagon, pos); }
                break;
            case BallType.Hexacontatetragon:
                for (int i = 0; i < 2; i++) { var b = spawner.SpawnBall(BallType.Icotetrasagon, pos); }
                break;
            case BallType.Chiliaicositetragon:
                for (int i = 0; i < 2; i++) { var b = spawner.SpawnBall(BallType.Hexacontatetragon, pos); }
                break;
            case BallType.Hexacontapentachiliapentacosiatriacontahexagon:
                for (int i = 0; i < 2; i++) { var b = spawner.SpawnBall(BallType.Chiliaicositetragon, pos); }
                break;
        }

        spawner.RemoveBall(this);
        Destroy(gameObject);
    }

    // ---- Poison ----

    public void ApplyPoison(float damagePerSec, float duration)
    {
        poisonEffects.Add(new PoisonEffect { damagePerSec = damagePerSec, duration = duration, elapsed = 0f });
    }

    private void UpdatePoison(float dt)
    {
        for (int i = poisonEffects.Count - 1; i >= 0; i--)
        {
            var p = poisonEffects[i];
            p.elapsed += dt;

            // Accumulate fractional damage
            p.damageAccumulator += p.damagePerSec * dt;
            if (p.damageAccumulator >= 1f)
            {
                int dmg = (int)p.damageAccumulator;
                p.damageAccumulator -= dmg;
                TakeTrueDamage(dmg, DamageType.Poison);
                if (health <= 0) return; // Already destroyed
            }

            if (p.elapsed >= p.duration)
                poisonEffects.RemoveAt(i);
        }
    }

    // ---- Sprite Creation ----

    private Sprite CreatePolygonSprite(int sides, float radius, Color color, int resolution)
    {
        int diameter = resolution;
        Texture2D texture = new Texture2D(diameter, diameter);

        // Clear the texture
        for (int y = 0; y < diameter; y++)
            for (int x = 0; x < diameter; x++)
                texture.SetPixel(x, y, Color.clear);

        Vector2 center = new Vector2(diameter / 2f, diameter / 2f);
        float radiusPixels = diameter / 2f - 2;

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

        // Gradient fill - USE WHITE/GRAY instead of color
        for (int i = 0; i < sides; i++)
            FillTriangleWithGradient(texture, center, vertices[i], vertices[(i + 1) % sides], Color.white);

        // Outline - USE WHITE instead of color
        for (int i = 0; i < sides; i++)
            DrawLine(texture, vertices[i], vertices[(i + 1) % sides], Color.white, 2f);

        texture.Apply();
        texture.filterMode = FilterMode.Bilinear;

        return Sprite.Create(texture, new Rect(0, 0, diameter, diameter), new Vector2(0.5f, 0.5f), diameter / (radius * 2f));
    }

    private void FillTriangleWithGradient(Texture2D texture, Vector2 center, Vector2 v1, Vector2 v2, Color edgeColor)
    {
        int minX = Mathf.FloorToInt(Mathf.Min(center.x, v1.x, v2.x));
        int maxX = Mathf.CeilToInt(Mathf.Max(center.x, v1.x, v2.x));
        int minY = Mathf.FloorToInt(Mathf.Min(center.y, v1.y, v2.y));
        int maxY = Mathf.CeilToInt(Mathf.Max(center.y, v1.y, v2.y));

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2 point = new Vector2(x, y);
                if (IsPointInTriangle(point, center, v1, v2))
                {
                    float distToCenter = Vector2.Distance(point, center);
                    float maxDist = Vector2.Distance(center, v1);
                    float t = Mathf.Clamp01(distToCenter / maxDist);

                    // Use grayscale gradient (white to transparent)
                    Color gradientColor = Color.white;
                    gradientColor.a = t * 0.25f;

                    if (x >= 0 && x < texture.width && y >= 0 && y < texture.height)
                        texture.SetPixel(x, y, gradientColor);
                }
            }
        }
    }

    private bool IsPointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float denom = ((b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y));
        if (Mathf.Abs(denom) < 0.0001f) return false;
        float alpha = ((b.y - c.y) * (p.x - c.x) + (c.x - b.x) * (p.y - c.y)) / denom;
        float beta = ((c.y - a.y) * (p.x - c.x) + (a.x - c.x) * (p.y - c.y)) / denom;
        float gamma = 1f - alpha - beta;
        return alpha >= 0 && beta >= 0 && gamma >= 0;
    }

    private void DrawLine(Texture2D texture, Vector2 start, Vector2 end, Color color, float width)
    {
        int steps = Mathf.CeilToInt(Vector2.Distance(start, end));
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            Vector2 point = Vector2.Lerp(start, end, t);
            for (float dx = -width / 2; dx <= width / 2; dx += 0.5f)
                for (float dy = -width / 2; dy <= width / 2; dy += 0.5f)
                {
                    int x = Mathf.RoundToInt(point.x + dx);
                    int y = Mathf.RoundToInt(point.y + dy);
                    if (x >= 0 && x < texture.width && y >= 0 && y < texture.height)
                        texture.SetPixel(x, y, color);
                }
        }
    }

    private void SpawnDeathParticles(Vector3 position)
    {
        int particleCount = UnityEngine.Random.Range(15, 30);

        for (int i = 0; i < particleCount; i++)
        {
            GameObject particle = new GameObject("Particle");
            particle.transform.position = position;

            SpriteRenderer sr = particle.AddComponent<SpriteRenderer>();
            sr.sprite = CreateParticleSprite();
            sr.color = color; // Use ball's color

            Particle p = particle.AddComponent<Particle>();
            Vector2 randomDir = UnityEngine.Random.insideUnitCircle.normalized;
            float speed = UnityEngine.Random.Range(6f, 12f);
            p.Initialize(randomDir * speed, UnityEngine.Random.Range(0.4f, 1.2f));
        }
    }
    
    private void spawnHitParticles(Vector3 position)
    {
        int particleCount = Mathf.Max(UnityEngine.Random.Range(-1, 3), 0);

        for (int i = 0; i < particleCount; i++)
        {
            GameObject particle = new GameObject("Particle");
            particle.transform.position = position;

            SpriteRenderer sr = particle.AddComponent<SpriteRenderer>();
            sr.sprite = CreateParticleSprite();
            sr.color = color; // Use ball's color

            Particle p = particle.AddComponent<Particle>();
            Vector2 randomDir = UnityEngine.Random.insideUnitCircle.normalized;
            float speed = UnityEngine.Random.Range(1f, 2f);
            p.Initialize(randomDir * speed, UnityEngine.Random.Range(0.4f, 1.2f));
        }
    }

    private Sprite CreateParticleSprite()
    {
        int size = 20;
        Texture2D texture = new Texture2D(size, size);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                texture.SetPixel(x, y, Color.white);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}

// Poison effect instance (stackable)
public class PoisonEffect
{
    public float damagePerSec;
    public float duration;
    public float elapsed;
    public float damageAccumulator;
}