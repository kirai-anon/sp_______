using UnityEngine;
using System.Linq;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
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
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadSave();
        ApplyUpgrades();
    }

    void LoadSave()
    {
        string json = PlayerPrefs.GetString("GameSave", "");
        if (!string.IsNullOrEmpty(json))
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