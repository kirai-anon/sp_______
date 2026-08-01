using UnityEngine;
using System.Collections.Generic;

public class BallSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private float spawnInterval = 1f;
    [SerializeField] private int spawnLimit = 1;
    [SerializeField] private int healthMultiplier = 1;

    [Header("Spawn Area")]
    [SerializeField] private Vector2 spawnOffset = Vector2.zero;
    [SerializeField] private float spawnRadius = 0f;

    [Header("Rift Settings")]
    [SerializeField] private GameObject riftPrefab;

    [Header("Ball Appearance")]
    [SerializeField] private int resolution = 64;

    [Header("Physics")]
    [SerializeField] private float gravity = 9.8f;
    [SerializeField] private float xLim = 10f;
    [SerializeField] private float floorHeight = -5f;

    [SerializeField] private AudioClip[] ballSounds;

    private float timer;
    public List<Ball> balls = new List<Ball>();

    public List<CurrencyDrop> currencyDrops = new List<CurrencyDrop>();

    private List<BallType[]> game = new List<BallType[]>();
    private int waveIndex = 0;

    void Start()
    {
        // setup gameplay loop


        game.Add(new BallType[] { BallType.Dodecagon });
        game.Add(new BallType[] { BallType.Dodecagon, BallType.Dodecagon });
        game.Add(new BallType[] { BallType.Tetragon });
        game.Add(new BallType[] { BallType.Tetragon, BallType.Dodecagon});
        game.Add(new BallType[] { BallType.Tetragon, BallType.Tetragon});
        game.Add(new BallType[] { BallType.Pentagon });
        game.Add(new BallType[] { BallType.Pentagon, BallType.Tetragon, BallType.Tetragon });
        game.Add(new BallType[] { BallType.Octagon });
        game.Add(new BallType[] { BallType.Decagon });
        game.Add(new BallType[] { }); // empty last part
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameManager.GameState.Play)
        {
            // Update physics safely using regular for-loops
            for (int i = balls.Count - 1; i >= 0; i--)
            {
                if (balls[i] != null)
                    balls[i].UpdatePhysics(Time.deltaTime, gravity, xLim, floorHeight);
            }

            for (int i = currencyDrops.Count - 1; i >= 0; i--)
            {
                if (currencyDrops[i] != null)
                    currencyDrops[i].UpdatePhysics(Time.deltaTime, gravity, xLim, floorHeight);
            }

            if (game.Count == 0) return;

            if (waveIndex < game.Count)
            {
                StepGameplay(game[waveIndex]);
            }
            else
            {
                StepGameplay(null);
            }
        }
        else
        {
            // GAME OVER / MENU CLEANUP: Wipe out everything safely using reverse loops

            // 1. Purge all remaining active balls
            for (int i = balls.Count - 1; i >= 0; i--)
            {
                if (balls[i] != null)
                {
                    // Assign a truly massive integer value (or call a custom instant kill method)
                    balls[i].TakeTrueDamage(int.MaxValue);
                }
            }

            // 2. Clear out all drifting currency drops from the game space
            for (int i = currencyDrops.Count - 1; i >= 0; i--)
            {
                if (currencyDrops[i] != null)
                {
                    Destroy(currencyDrops[i].gameObject);
                }
            }

            healthMultiplier = 1;
            spawnLimit = 1;
            waveIndex = 0;
        }
    }

    private bool isRiftActive = false; // Add this tracking flag at the class level

    private void StepGameplay(BallType[] wave)
    {
        // 1. CRITICAL LOCK: If a rift is currently growing or shrinking, stop everything here
        if (isRiftActive) return;

        void NextWave()
        {
            waveIndex = 0;
            healthMultiplier += 1;
            spawnLimit += 1;
        }

        bool isCycleComplete = (waveIndex >= game.Count);

        if (isCycleComplete)
        {
            if (balls.Count < spawnLimit)
            {
                NextWave();
            }
            return;
        }

        if (balls.Count < spawnLimit)
        {
            timer += Time.deltaTime;

            if (timer >= spawnInterval)
            {
                // 2. Turn on the lock before creating the rift
                isRiftActive = true;

                Vector2 spawnPos = (Vector2)transform.position + new Vector2(Random.Range(-xLim + 3, xLim - 3), Random.Range(0, -floorHeight - 1));

                GameObject riftObj = Instantiate(riftPrefab, new Vector3(spawnPos.x, spawnPos.y, 1), Quaternion.identity);
                RiftEffect rift = riftObj.GetComponent<RiftEffect>();

                if (wave.Length > 0)
                {
                    // 3. We pass a second callback to unlock the spawner when the rift finishes shrinking
                    StartCoroutine(rift.RunRiftAnimation(
                        () => {
                            for (int i = 0; i < wave.Length; i++)
                            {
                                SpawnBall(wave[i], spawnPos);
                            }
                        },
                        () => { isRiftActive = false; } // Unlock callback
                    ));

                    timer = 0f;
                    waveIndex += 1;
                }
                else
                {
                    switch (healthMultiplier)
                    {
                        case < 4:
                            {
                                NextWave();
                                isRiftActive = false;
                                break;
                            }
                        case >= 4:
                            {
                                StartCoroutine(rift.RunRiftAnimation(
                                    () =>
                                    {
                                        switch (healthMultiplier)
                                        {
                                            case >= 32: goto Case4;
                                            case >= 16: Case4: SpawnBall(BallType.Hexacontapentachiliapentacosiatriacontahexagon, spawnPos); goto Case3;
                                            case >= 8: Case3: SpawnBall(BallType.Chiliaicositetragon, spawnPos); goto Case2;
                                            case >= 4: Case2: SpawnBall(BallType.Hexacontatetragon, spawnPos); break;
                                        }
                                    },
                                    () => { isRiftActive = false; } // Unlock callback
                                ));

                                NextWave();
                                break;
                            }
                    }
                }
            }
        }
        else
        {
            timer = 0f;
        }
    }


    public GameObject SpawnBall(BallType type, Vector2 spawnPos)
    {
        GameObject ballObj = new GameObject($"Ball_{type}");
        ballObj.transform.position = spawnPos;

        Ball ball = ballObj.AddComponent<Ball>();
        ball.Initialize(type, resolution, this, healthMultiplier, ballSounds);

        balls.Add(ball);
        return ballObj;
    }

    public void RemoveBall(Ball ball)
    {
        balls.Remove(ball);
    }

    public GameObject SpawnCurrencyDrop(Vector3 position, Vector2 velocity)
    {
        GameObject dropObj = new GameObject("CurrencyDrop");
        dropObj.transform.position = position + new Vector3(0, 0, 0.5f);

        CurrencyDrop drop = dropObj.AddComponent<CurrencyDrop>();
        drop.Initialize(velocity);

        currencyDrops.Add(drop);
        return dropObj;
    }

    public void RemoveCurrencyDrop(CurrencyDrop drop)
    {
        currencyDrops.Remove(drop);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Vector2 pos = (Vector2)transform.position + spawnOffset;
        Gizmos.DrawWireSphere(pos, 0.2f);
        if (spawnRadius > 0)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(pos, spawnRadius);
        }
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(new Vector3(-xLim, floorHeight - 2, 0), new Vector3(-xLim, floorHeight + 10, 0));
        Gizmos.DrawLine(new Vector3(xLim, floorHeight - 2, 0), new Vector3(xLim, floorHeight + 10, 0));
        Gizmos.DrawLine(new Vector3(-xLim - 2, floorHeight, 0), new Vector3(xLim + 2, floorHeight, 0));
    }
}