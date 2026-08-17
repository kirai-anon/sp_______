using System;
using System.Collections.Generic;

// --- ENUMS & DATA STRUCTURES ---

public enum BallType
{
    Dodecagon, // 1
    Tetragon, // 3
    Pentagon, // 5
    Octagon, // 8
    Decagon, // 10
    Icotetrasagon, // 24
    Hexacontatetragon, // 64
    Chiliaicositetragon, // 1024
    Hexacontapentachiliapentacosiatriacontahexagon, // 65'536
    Hexadecamegaheptacosiaheptacontaheptachiliadiacosiahexadecagon, // 16'777'216
    Disgigahectatetracontaheptamegatetractamyriatriacontaoctachiliahexahectatetracontaheptagon // 2'147'483'647
}

public enum UpgradeId
{
    BulletDamage, FireRate, LightningDamage, LightningBounces,
    PoisonDamagePerSec, PoisonDuration, CurrencyMultiplier, PlayerHealth
}

[Serializable]
public class UpgradeData
{
    public UpgradeId id;
    public string name;
    public float cost;
    public float x;
    public float y;
}

// --- SERIALIZATION HELPERS ---

[Serializable]
public class UpgradeLevelEntry
{
    public UpgradeId id;
    public int level;
}

[Serializable]
public class SaveData
{
    public double currency = 0;

    // This list is what Unity actually saves to the disk
    public List<UpgradeLevelEntry> upgradeLevelsList = new List<UpgradeLevelEntry>();

    // This dictionary is for fast access during gameplay (not serialized)
    private Dictionary<UpgradeId, int> _dictCache;

    public int GetLevel(UpgradeId id)
    {
        RefreshCache();
        return _dictCache.ContainsKey(id) ? _dictCache[id] : 0;
    }

    public void SetLevel(UpgradeId id, int level)
    {
        RefreshCache();
        _dictCache[id] = level;
        SyncListFromDict();
    }

    // Transfers Dictionary data back to the List so it can be saved
    public void SyncListFromDict()
    {
        upgradeLevelsList.Clear();
        foreach (var kvp in _dictCache)
        {
            upgradeLevelsList.Add(new UpgradeLevelEntry { id = kvp.Key, level = kvp.Value });
        }
    }

    // Transfers List data to the Dictionary for fast lookup
    public void RefreshCache()
    {
        if (_dictCache != null) return;

        _dictCache = new Dictionary<UpgradeId, int>();
        foreach (var entry in upgradeLevelsList)
        {
            _dictCache[entry.id] = entry.level;
        }
    }
}