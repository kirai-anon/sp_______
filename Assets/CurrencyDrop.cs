using UnityEngine;

public class CurrencyDrop : MonoBehaviour
{
    private Vector2 velocity;
    private float radius = 0.15f;
    private bool collected = false;

    public void Initialize(Vector2 initialVelocity)
    {
        velocity = initialVelocity;

        // Create sprite
        SpriteRenderer sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = CreateSquareSprite();

        // Rigidbody
        Rigidbody2D rb = gameObject.AddComponent<Rigidbody2D>();
        rb.isKinematic = true;

        // Collider
        CircleCollider2D col = gameObject.AddComponent<CircleCollider2D>();
        col.radius = radius;
        col.isTrigger = true;
    }

    public void UpdatePhysics(float dt, float gravity, float xLim, float floorHeight)
    {
        if (collected) return;

        velocity.y -= gravity * dt;

        Vector3 pos = transform.position;
        pos.x += velocity.x * dt;
        pos.y += velocity.y * dt;

        // Wall collision (no bounce, just stop)
        if (Mathf.Abs(pos.x) > xLim - radius)
        {
            pos.x = Mathf.Sign(pos.x) * (xLim - radius);
            velocity.x = 0;
        }

        // Floor collision (no bounce, just stop)
        if (pos.y < floorHeight + radius)
        {
            pos.y = floorHeight + radius;
            velocity.y = 0;
            velocity.x = 0; // Stop sliding too
        }

        transform.position = pos;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<Player>() != null && !collected)
        {
            collected = true;
            GameManager.Instance.AddCurrency(1);

            // Get reference to spawner to remove from list
            BallSpawner spawner = FindObjectOfType<BallSpawner>();
            if (spawner != null) spawner.RemoveCurrencyDrop(this);

            Destroy(gameObject);
        }
    }

    private Sprite CreateSquareSprite()
    {
        int size = 24;
        Texture2D texture = new Texture2D(size, size);
        Color color = new Color(150f / 255f, 255f / 255f, 255f / 255f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size / (radius * 2f));
    }
}