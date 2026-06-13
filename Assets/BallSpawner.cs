using UnityEngine;
using System.Collections.Generic;
using Unity.VisualScripting;
using System.Runtime.CompilerServices;

public class BallSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private float spawnInterval = 1f;
    [SerializeField] private int spawnLimit = 1;
    [SerializeField] private int healthMultiplier = 1;

    [Header("Spawn Area")]
    [SerializeField] private Vector2 spawnOffset = Vector2.zero;
    [SerializeField] private float spawnRadius = 0f;

    [Header("Ball Appearance")]
    [SerializeField] private int resolution = 64;

    [Header("Physics")]
    [SerializeField] private float gravity = 9.8f;
    [SerializeField] private float xLim = 10f;
    [SerializeField] private float floorHeight = -5f;

    [SerializeField] private AudioClip[] ballSounds;

    private float timer;
    private bool isSpawning;
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

            if (isSpawning)
            {
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

            // 3. Completely empty the lists so references aren't left trailing
            balls.Clear();
            currencyDrops.Clear();
        }
    }

    private void StepGameplay(BallType[] wave)
    {
        void NextWave()
        {
            waveIndex = 0;
            healthMultiplier += 1;
            spawnLimit += 1;
        }

        // Check if we are currently "waiting" to reset the cycle
        bool isCycleComplete = (waveIndex >= game.Count);

        if (isCycleComplete)
        {
            // WAIT here until the player has cleared enough balls 
            // to actually allow the NEW spawnLimit to take effect
            if (balls.Count < spawnLimit)
            {
                NextWave();
                // The next frame will now proceed to the 'else' block below
            }
            return;
        }
        
        // Normal Spawning Logic
        if (balls.Count < spawnLimit)
        {
            timer += Time.deltaTime;

            if (timer >= spawnInterval)
            {
                if (waveIndex != game.Count - 1) {
                    for (int i = 0; i < wave.Length; i++)
                    {
                        SpawnBall(wave[i]);
                    }

                    timer = 0f;
                    waveIndex += 1;
                    // After this, waveIndex might equal game.Count, 
                    // triggering the 'isCycleComplete' check on the next frame.
                }
                else
                {
                    switch (healthMultiplier)
                    {
                        default: timer = spawnInterval; break;
                        case >= 64: goto Case4;
                        case >= 32: Case4: SpawnBall(BallType.Hexacontapentachiliapentacosiatriacontahexagon); goto Case3;
                        case >= 16: Case3: SpawnBall(BallType.Chiliaicositetragon); goto Case2;
                        case >= 8: Case2: SpawnBall(BallType.Hexacontatetragon); goto Case1;
                        case >= 4: Case1: SpawnBall(BallType.Icotetrasagon); break;
                    }
                    NextWave(); // kind of forgot this needs to be used by every switch
                }
            }
        }
        else
        {
            timer = 0f;
        }
    }

    public GameObject SpawnBall(BallType type)
    {
        Vector2 spawnPos = (Vector2)transform.position + spawnOffset;
        if (spawnRadius > 0)
            spawnPos += Random.insideUnitCircle * spawnRadius;

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

    public void StartSpawning() { isSpawning = true; timer = 0f; }
    public void StopSpawning() { isSpawning = false; }

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