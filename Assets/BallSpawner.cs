using UnityEngine;
using System.Collections.Generic;
using Unity.Mathematics;

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

    private bool isRiftActive = false; // tracking flag

    private void StepGameplay(BallType[] wave)
{
        // if a rift is currently growing or shrinking, stop everything here
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
                Vector2 spawnPos = (Vector2)transform.position + new Vector2(UnityEngine.Random.Range(-xLim + 3, xLim - 3), UnityEngine.Random.Range(0, -floorHeight - 1));

                if (wave.Length > 0)
                {
                    // turn on the lock before creating the rift
                    isRiftActive = true;
                    GameObject riftObj = Instantiate(riftPrefab, new Vector3(spawnPos.x, spawnPos.y, 1), Quaternion.identity);
                    RiftEffect rift = riftObj.GetComponent<RiftEffect>();

                    // pass a second callback to unlock the spawner when the rift finishes shrinking
                    StartCoroutine(rift.RunRiftAnimation(
                        () => {
                            for (int i = 0; i < wave.Length; i++)
                            {
                                SpawnBall(wave[i], spawnPos);
                            }
                        },
                        () => { isRiftActive = false; } // unlock callback
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
                                // if health multiplier is too low just skip the wave and DO NOT spawn a rift
                                NextWave();
                                timer = 1f;
                                break;
                            }
                        case >= 4:
                            {
                                isRiftActive = true;
                                GameObject riftObj = Instantiate(riftPrefab, new Vector3(spawnPos.x, spawnPos.y, 1), Quaternion.identity);
                                RiftEffect rift = riftObj.GetComponent<RiftEffect>();

                                StartCoroutine(rift.RunRiftAnimation(
                                    () =>
                                    {
                                        // starts at 2^2
                                        BallType[] ballTypes = new[]
                                        {
                                            BallType.Hexacontatetragon, // 4
                                            BallType.Chiliaicositetragon, // 8
                                            BallType.Hexacontapentachiliapentacosiatriacontahexagon, // 16
                                            BallType.Hexacontapentachiliapentacosiatriacontahexagon, // 32
                                            BallType.Hexadecamegaheptacosiaheptacontaheptachiliadiacosiahexadecagon, // 64
                                            BallType.Disgigahectatetracontaheptamegatetractamyriatriacontaoctachiliahexahectatetracontaheptagon // 128
                                        };
                                        int startFactor = healthMultiplier >= (1 << (ballTypes.Length + 2))
                                            ? 1 << (ballTypes.Length + 1)
                                            : healthMultiplier;
                                        int startIndex = Mathf.Min((int)math.log2(startFactor) - 2, ballTypes.Length - 1);
                                        for (int i = startIndex; i >= 0; i--)
                                        {
                                            int factor = 1 << (i + 2);
                                            if (healthMultiplier % factor == 0)
                                            {
                                                SpawnBall(ballTypes[i], spawnPos);
                                            }
                                        }
                                    },
                                    () =>
                                    {
                                        isRiftActive = false;
                                        // advance the wave only after it finishes completely
                                        NextWave();
                                    }
                                ));

                                timer = 0f;
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

    public GameObject SpawnCurrencyDrop(Vector3 position, Vector2 velocity, int value)
    {
        GameObject dropObj = new GameObject("CurrencyDrop");
        dropObj.transform.position = position + new Vector3(0, 0, 0.5f);

        CurrencyDrop drop = dropObj.AddComponent<CurrencyDrop>();
        drop.Initialize(drop.transform.position, velocity, value);

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