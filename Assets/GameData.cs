using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

// --- ENUMS & DATA STRUCTURES ---

public enum BallType
{
    Dodecagon, Tetragon, Pentagon, Octagon,
    Decagon, Icotetrasagon, Hexacontatetragon, Chiliaicositetragon,
    Hexacontapentachiliapentacosiatriacontahexagon
}

public enum UpgradeId
{
    BulletDamage, FireRate, LightningDamage, LightningBounces,
    PoisonDamagePerSec, PoisonDuration, CurrencyMultiplier
}

[Serializable]
public class UpgradeData
{
    public UpgradeId id;
    public string name;
    public int maxLevel;
    public float[] costs;
    public float angle;   // Radians
    public float radius;
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