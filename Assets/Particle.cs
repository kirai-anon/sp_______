using UnityEngine;

public class Particle : MonoBehaviour
{
    private Vector2 velocity;
    private float lifetime;
    private float elapsed;
    private SpriteRenderer sr;
    private Color startColor;

    public void Initialize(Vector2 vel, float life)
    {
        velocity = vel;
        lifetime = life;
        sr = GetComponent<SpriteRenderer>();
        startColor = sr.color;
    }

    void Update()
    {
        elapsed += Time.deltaTime;

        // Move
        transform.position += (Vector3)velocity * Time.deltaTime;

        velocity = new Vector2(velocity.x, velocity.y - (12f * Time.deltaTime));

        // Fade out
        float alpha = 1f - (elapsed / lifetime);
        Color c = startColor;
        c.a = alpha;
        sr.color = c;

        // Destroy when lifetime expires
        if (elapsed >= lifetime)
        {
            Destroy(gameObject);
        }
    }
}