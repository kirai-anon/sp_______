using UnityEngine;
using TMPro;

public class CurrencyDrop : MonoBehaviour
{
    private Vector2 velocity;
    private float radius = 0.12f;
    private bool collected = false;

    // Denomination and Magnetism Properties
    public int currencyValue = 1;
    public float pullRadius = 8f;
    public float pullStrength = 1f;
    public float mergeDistance = 0.5f; // Proximity threshold to check for a cluster

    private float currentRotationAngle;

    private SpriteRenderer sr;
    private TextMeshPro valueText;
    private CircleCollider2D col;

    public void Initialize(Vector2 spawnPos, Vector2 initialVelocity, int value = 1)
    {
        transform.position = spawnPos;
        velocity = initialVelocity;
        currencyValue = value;

        currentRotationAngle = Random.Range(0f, Mathf.PI * 2);

        sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = CreateSquareSprite(currentRotationAngle);

        Rigidbody2D rb = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;

        col = gameObject.AddComponent<CircleCollider2D>();
        col.radius = radius;
        col.isTrigger = true;

        SetupTextDisplay();
        UpdateVisuals();
    }

    public void UpdatePhysics(float dt, float gravity, float xLim, float floorHeight)
    {
        if (collected) return;

        // Apply natural gravity and air resistance
        velocity.y -= gravity * dt;
        velocity.x *= 0.99f;

        // Continuous Mutual Attraction (Swirling physics)
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, pullRadius);
        foreach (var otherCol in colliders)
        {
            if (otherCol.gameObject == gameObject) continue;

            CurrencyDrop otherDrop = otherCol.GetComponent<CurrencyDrop>();
            if (otherDrop != null && otherDrop.currencyValue == this.currencyValue)
            {
                Vector2 direction = (Vector2)(otherDrop.transform.position - transform.position);
                float distance = direction.magnitude;

                if (distance > 0.001f && distance < pullRadius)
                {
                    float pullFactor = Mathf.Clamp01(1f - (distance / pullRadius));
                    Vector2 acceleration = direction.normalized * (pullStrength * pullFactor);
                    velocity += acceleration * dt;
                }
            }
        }

        // Check for 10-cluster merge condition when drops are tightly packed within mergeDistance
        CheckForClusterMerge();

        // Apply final velocity to position
        Vector3 pos = transform.position;
        pos.x += velocity.x * dt;
        pos.y += velocity.y * dt;

        // Wall collision
        if (Mathf.Abs(pos.x) > xLim - radius)
        {
            pos.x = Mathf.Sign(pos.x) * (xLim - radius);
            velocity.x *= -0.5f;
        }

        // Floor collision
        if (pos.y < floorHeight + radius)
        {
            pos.y = floorHeight + radius;
            velocity.y = -0.9f;
        }

        transform.position = pos;
    }

    private void CheckForClusterMerge()
    {
        // Gather all drops of the same value within the tight merge distance
        Collider2D[] closeColliders = Physics2D.OverlapCircleAll(transform.position, mergeDistance);
        System.Collections.Generic.List<CurrencyDrop> cluster = new System.Collections.Generic.List<CurrencyDrop>();
        cluster.Add(this);

        foreach (var col in closeColliders)
        {
            if (col.gameObject == gameObject) continue;
            CurrencyDrop drop = col.GetComponent<CurrencyDrop>();
            if (drop != null && drop.currencyValue == this.currencyValue)
            {
                cluster.Add(drop);
            }
        }

        // ONLY merge if we have accumulated 10 or more of the same value in this cluster
        if (cluster.Count >= 10)
        {
            // Use instance ID ordering to ensure only ONE drop in the cluster handles the upgrade
            // and destroys the other 9 duplicate drops.
            bool isLeader = true;
            foreach (var drop in cluster)
            {
                if (drop.gameObject.GetInstanceID() < this.gameObject.GetInstanceID())
                {
                    isLeader = false;
                    break;
                }
            }

            if (isLeader)
            {
                // Multiply value by 10 (representing 10 drops of this tier combining into 1 of the next power of 10)
                currencyValue *= 10;
                UpdateVisuals();

                // Destroy 9 of the pooled companion drops
                int destroyedCount = 0;
                BallSpawner spawner = Object.FindFirstObjectByType<BallSpawner>();

                for (int i = 0; i < cluster.Count; i++)
                {
                    if (cluster[i] != this && destroyedCount < 9)
                    {
                        if (spawner != null) spawner.RemoveCurrencyDrop(cluster[i]);
                        Destroy(cluster[i].gameObject);
                        destroyedCount++;
                    }
                }
            }
        }
    }

    private void UpdateVisuals()
    {
        if (sr == null) return;

        float tierLog = Mathf.Max(0, Mathf.Log10(currencyValue));
        float scaleFactor = 1f + (tierLog * 0.15f);
        transform.localScale = Vector3.one * Mathf.Clamp(scaleFactor, 1f, 3f);

        if (valueText != null)
        {
            valueText.text = FormatCurrencyValue(currencyValue);
        }
    }

    private string FormatCurrencyValue(int val)
    {
        if (val >= 1000000) return (val / 1000000) + "M";
        if (val >= 1000) return (val / 1000) + "K";
        return val.ToString();
    }

    private void SetupTextDisplay()
    {
        GameObject textObj = new GameObject("ValueText");
        textObj.transform.SetParent(transform);
        textObj.transform.localPosition = Vector3.zero;

        valueText = textObj.AddComponent<TextMeshPro>();
        valueText.alignment = TextAlignmentOptions.Center;
        valueText.fontSize = 5;
        valueText.color = Color.white;
        
        if (sr != null)
        {
            valueText.sortingLayerID = sr.sortingLayerID;
            valueText.sortingOrder = sr.sortingOrder + 1;
        }

        RectTransform rectTransform = textObj.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(2f, 1f);
        rectTransform.localScale = Vector3.one;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<Player>() != null && !collected)
        {
            collected = true;
            GameManager.Instance.AddCurrency(currencyValue);

            BallSpawner spawner = Object.FindFirstObjectByType<BallSpawner>();
            if (spawner != null) spawner.RemoveCurrencyDrop(this);

            Destroy(gameObject);
        }
    }

    private Sprite CreateSquareSprite(float angleRad)
    {
        int size = 24;
        Texture2D texture = new Texture2D(size, size);
        Color color = new Color(0.59f, 1f, 1f);
        Color transparent = new Color(0, 0, 0, 0);

        // Precalculate sin/cos
        float cos = Mathf.Cos(angleRad);
        float sin = Mathf.Sin(angleRad);

        float center = size / 2f;
        int squareSize = 16; // Size of the square inside the 24x24 texture buffer
        int innerSize = 12;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Translate pixel coordinate relative to center
                float px = x - center;
                float py = y - center;

                // Rotate point backwards by the angle to check if it falls inside the unrotated box
                float rotX = cos * px + sin * py;
                float rotY = -sin * px + cos * py;

                // Check if the rotated coordinate falls within the square bounds
                if (Mathf.Abs(rotX) <= squareSize / 2f && Mathf.Abs(rotY) <= squareSize / 2f &&
                    (Mathf.Abs(rotX) >= innerSize  / 2f || Mathf.Abs(rotY) >= innerSize  / 2f))
                {
                    texture.SetPixel(x, y, color);
                }
                else
                {
                    texture.SetPixel(x, y, transparent);
                }
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size / (radius * 4));
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, pullRadius);
    }
}