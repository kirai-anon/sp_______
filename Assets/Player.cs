using Unity.Mathematics;
using UnityEngine;

public class Player : MonoBehaviour
{
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private BallSpawner ballSpawner;

    [SerializeField] public float initPlayerHealth = 1;
    
    public float playerHealth = 1;
    private float invincibleTimer;

    [SerializeField] private AudioClip shootSound;

    private float shootTimer;

    [SerializeField] private GameObject body;

    private float wobbleTimer;

    float mouseX = 0;

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
                else
                {
                    wobbleTimer *= 0.8f;
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

        Vector3 pos = transform.position;
        pos.x = Mathf.Lerp(pos.x, mouseX, 0.2f);
        pos.y = -6f;
        transform.position = pos;
    }

    private void Shoot()
    {
        GameObject bullet = Instantiate(bulletPrefab, transform.position + new Vector3(0, 0.2f, 0), Quaternion.identity);
        bullet.GetComponent<Bullet>().Initialize(
            GameManager.Instance.bulletDamage,
            GameManager.Instance.lightningDamage,
            GameManager.Instance.lightningBounces,
            GameManager.Instance.poisonDamagePerSec,
            GameManager.Instance.poisonDuration,
            ballSpawner
        );

        AudioSource.PlayClipAtPoint(shootSound, Camera.main.transform.position, 1.0f);
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