using UnityEngine;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public enum GameState { Upgrades, Play }
    public GameState CurrentState { get; private set; }

    public static GameManager Instance { get; private set; }
    public SaveData save;

    // Runtime upgrade values (computed from save)
    public int bulletDamage = 1;
    public float fireRate = 0.5f;
    public int lightningDamage = 0;
    public int lightningBounces = 0;  // 0 = lightning not bought
    public float poisonDamagePerSec = 0;
    public float poisonDuration = 0;  // 0 = poison not bought
    public float currencyMultiplier = 1f;

    // Base values
    private const float BASE_FIRE_RATE = 0.5f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadSave();
        ApplyUpgrades();
    }

    private void Start()
    {
        SetState(GameState.Play);
    }

    public void SetState(GameState newState)
    {
        CurrentState = newState;
    }

    public void StartRound()
    {
        if (!PlayerPrefs.HasKey("Played")) {
            PlayerPrefs.SetInt("Played", 1);
            PlayerPrefs.Save();
        }
        SetState(GameState.Play);
    }

    public void ReturnToMenu()
    {
        StartCoroutine(SlowDown());
    }

    private IEnumerator SlowDown()
    {
        float duration = 2f; // Target duration in real-time seconds
        float elapsed = 0f;

        // 1. Smoothly transition Time.timeScale from 1 down to 0 over 1 second
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime; // Track real-world time, not game time

            // Calculate the linear interpolation from 1 to 0
            Time.timeScale = Mathf.Clamp01(1f - (elapsed / duration));

            yield return null; // Wait for the next frame
        }

        // Ensure it perfectly hits 0 at the end of the loop
        Time.timeScale = 0f;

        // 2. Call your state change function
        SetState(GameState.Upgrades);

        // 3. Instantly reset the game speed back to normal
        Time.timeScale = 1f;
    }

    void LoadSave()
    {
        string json = PlayerPrefs.GetString("GameSave", "");
        if (1==0 && !string.IsNullOrEmpty(json))
        {
            save = JsonUtility.FromJson<SaveData>(json);
            // Important: Rebuild the internal dictionary from the loaded list
            save.RefreshCache();
        }
        else
        {
            save = new SaveData();
            // Initialize levels at 0 for all upgrade types
            foreach (UpgradeId id in System.Enum.GetValues(typeof(UpgradeId)))
            {
                save.SetLevel(id, 0);
            }
        }
    }

    public void SaveGame()
    {
        PlayerPrefs.SetString("GameSave", JsonUtility.ToJson(save));
        PlayerPrefs.Save();
    }

    public void ApplyUpgrades()
    {
        bulletDamage = 1 + save.GetLevel(UpgradeId.BulletDamage);

        float fireRateLvl = save.GetLevel(UpgradeId.FireRate);
        fireRate = BASE_FIRE_RATE / (1f + fireRateLvl * 0.2f);

        lightningDamage = save.GetLevel(UpgradeId.LightningDamage); // 0 if not bought

        lightningBounces = save.GetLevel(UpgradeId.LightningBounces); // 0 if not bought at all

        poisonDamagePerSec = save.GetLevel(UpgradeId.PoisonDamagePerSec); // 0 if not bought

        int poisonDurationLvl = save.GetLevel(UpgradeId.PoisonDuration);
        poisonDuration = poisonDurationLvl > 0 ? 1f + (poisonDurationLvl - 1) * 0.5f : 0f;
        
        currencyMultiplier = 1f + save.GetLevel(UpgradeId.CurrencyMultiplier) * 0.5f;
    }

    public void AddCurrency(double amount)
    {
        save.currency += amount * currencyMultiplier;
        SaveGame();
    }
}