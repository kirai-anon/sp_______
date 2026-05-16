using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using System.Runtime.CompilerServices;

public class UpgradeTree : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private GameObject nodeTemplate;
    [SerializeField] private GameObject lineTemplate;
    [SerializeField] private TextMeshProUGUI currencyText;

    [Header("Layout")]
    [SerializeField] private float treeRadius = 200f;
    [SerializeField] private Vector2 treeCenter = new Vector2(0, 0);

    private static readonly Color COL_ACTIVE = new Color(0.1f, 0.2f, 0.1f);
    private static readonly Color COL_AVAILABLE = new Color(0.2f, 0.2f, 0.1f);
    private static readonly Color COL_LOCKED = new Color(0.1f, 0.1f, 0.1f);

    private List<UpgradeData> upgrades = new List<UpgradeData>
    {
        new UpgradeData { id = UpgradeId.BulletDamage,       name = "Bullet Damage",     maxLevel = 5, costs = new float[] { 10, 25, 50, 100, 200 },  angle = 0f,                 radius = 100f },
        new UpgradeData { id = UpgradeId.FireRate,           name = "Fire Rate",         maxLevel = 5, costs = new float[] { 10, 25, 50, 100, 200 },  angle = Mathf.PI / 3f,      radius = 300f },
        new UpgradeData { id = UpgradeId.LightningDamage,    name = "Lightning Dmg",     maxLevel = 5, costs = new float[] { 30, 60, 120, 240, 480 }, angle = 2f * Mathf.PI / 3f, radius = 200f },
        new UpgradeData { id = UpgradeId.LightningBounces,   name = "Lightning Bounces", maxLevel = 3, costs = new float[] { 50, 150, 400 },          angle = Mathf.PI,           radius = 100f },
        new UpgradeData { id = UpgradeId.PoisonDamagePerSec, name = "Poison Dmg/s",      maxLevel = 5, costs = new float[] { 30, 60, 120, 240, 480 }, angle = 4f * Mathf.PI / 3f, radius = 200f },
        new UpgradeData { id = UpgradeId.PoisonDuration,     name = "Poison Duration",   maxLevel = 3, costs = new float[] { 40, 100, 250 },          angle = 5f * Mathf.PI / 3f, radius = 300f },
        new UpgradeData { id = UpgradeId.CurrencyMultiplier, name = "Currency Mult",     maxLevel = 3, costs = new float[] { 50, 150, 400 },          angle = Mathf.PI / 2f,      radius = 100f },
    };

    private List<RectTransform> nodeTransforms = new List<RectTransform>();
    private List<Image> nodeImages = new List<Image>();
    private List<TextMeshProUGUI> nodeTexts = new List<TextMeshProUGUI>();
    private List<GameObject> lineObjects = new List<GameObject>();

    private bool dragging = false;
    private float currentRotation = 0f;
    private float lastMouseAngle = 0f;

    private float minRotation = Mathf.Infinity;
    private float maxRotation = Mathf.Infinity;
    [SerializeField] private float rotMult = 5* Mathf.PI;

    void Start()
    {
        // Clear lists just in case of a hot-reload bug
        nodeTransforms.Clear();
        nodeImages.Clear();
        nodeTexts.Clear();
        lineObjects.Clear();

        CreateNodes(); // This fills nodeTransforms
        CreateLines(); // This fills lineObjects
        UpdateVisuals();
    }

    void Update()
    {
        HandleDrag();
        UpdateCurrencyText();

        if (minRotation == Mathf.Infinity)
        {
            minRotation = 0f;
            maxRotation = 0f;

            for (int i = 0; i < upgrades.Count; i++) // set rotation limits, dynamic
            {
                float indexRotation = upgrades[i].angle;

                if (indexRotation < minRotation) minRotation = indexRotation;
                if (indexRotation > maxRotation) maxRotation = indexRotation;
            }
        }
    }

    private void HandleDrag()
    {
        Vector2 mousePos = (Vector2)Input.mousePosition - new Vector2(canvas.transform.position.x, canvas.transform.position.y);
        float mouseAngle = Mathf.Atan2(mousePos.y - treeCenter.y, mousePos.x - treeCenter.x);

        if (Input.GetMouseButtonDown(0))
        {
            dragging = true;
            lastMouseAngle = mouseAngle;
        }
        if (Input.GetMouseButtonUp(0)) dragging = false;

        if (dragging)
        {
            float delta = mouseAngle - lastMouseAngle;
            if (delta > Mathf.PI) delta -= 2f * Mathf.PI;
            if (delta < -Mathf.PI) delta += 2f * Mathf.PI;
            currentRotation += delta;

            if (currentRotation < minRotation * rotMult - rotMult/2) currentRotation = minRotation * rotMult - rotMult/2; Debug.Log("Min: " + (minRotation * rotMult - rotMult/2));
            if (currentRotation > maxRotation * rotMult - rotMult/2) currentRotation = maxRotation * rotMult - rotMult/2; Debug.Log("Max: " + (maxRotation * rotMult - rotMult/2));

            lastMouseAngle = mouseAngle;
            PositionNodes();
        }
    }

    private void CreateNodes()
    {
        // Clear lists to prevent index mismatch
        nodeTransforms.Clear();
        nodeImages.Clear();
        nodeTexts.Clear();

        for (int i = 0; i < upgrades.Count; i++)
        {
            // 1. Instantiate
            GameObject node = Instantiate(nodeTemplate, canvas.transform);
            node.SetActive(true);

            // 2. Get Components with Explicit Namespaces
            RectTransform rt = node.GetComponent<RectTransform>();
            Button btn = node.GetComponentInChildren<Button>(true);
            Image img = node.GetComponentInChildren<Image>(true);

            if (btn == null)
            {
                // If this still hits, the component is fundamentally not 
                // reachable on this GameObject or any of its children.
                Debug.LogError($"CRITICAL: Node {i} has no Button in root or children!");
                continue;
            }

            // Try getting TMP from children
            TextMeshProUGUI txt = node.GetComponentInChildren<TextMeshProUGUI>();

            // 4. Setup Listener
            int index = i;
            btn.onClick.RemoveAllListeners(); // Clean slate
            btn.onClick.AddListener(() => TryBuy(index));

            // 5. Data Initialization
            var upd = upgrades[i];
            int lvl = GameManager.Instance.save.GetLevel(upd.id);
            if (txt != null) txt.text = $"{upd.name}\nLv {lvl}/{upd.maxLevel}";

            // 6. Add to Lists
            nodeTransforms.Add(rt);
            nodeImages.Add(img);
            nodeTexts.Add(txt);
        }
        PositionNodes();
    }

    private void PositionNodes()
    {
        // SAFETY CHECK: If the list isn't ready, don't try to position anything
        if (nodeTransforms == null || nodeTransforms.Count < upgrades.Count)
            return;

        for (int i = 0; i < upgrades.Count; i++)
        {
            float angle = upgrades[i].angle + currentRotation;
            angle = Unity.Mathematics.math.tanh(angle)*Mathf.PI/2 + Mathf.PI/2;
            float r = upgrades[i].radius;
            Vector2 pos = treeCenter + new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r);

            // This is where the crash was happening
            nodeTransforms[i].anchoredPosition = pos;
        }
        UpdateLines();
    }

    private void CreateLines()
    {
        for (int i = 0; i < upgrades.Count; i++)
        {
            GameObject line = Instantiate(lineTemplate, canvas.transform);
            line.SetActive(true);
            lineObjects.Add(line);
        }
    }

    private void UpdateLines()
    {
        for (int i = 0; i < upgrades.Count; i++)
        {
            RectTransform lineRT = lineObjects[i].GetComponent<RectTransform>();
            Vector2 start = treeCenter;
            Vector2 end = nodeTransforms[i].anchoredPosition;

            Vector2 diff = end - start;
            float dist = diff.magnitude;
            float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;

            lineRT.anchoredPosition = start;
            lineRT.sizeDelta = new Vector2(dist, 4f);
            lineRT.rotation = Quaternion.Euler(0, 0, angle);

            int lvl = GameManager.Instance.save.GetLevel(upgrades[i].id);
            Image lineImg = lineObjects[i].GetComponent<Image>();

            if (lvl > 0) lineImg.color = COL_ACTIVE;
            else if (CanBuy(i)) lineImg.color = COL_AVAILABLE;
            else lineImg.color = COL_LOCKED;
        }
    }

    private void UpdateVisuals()
    {
        for (int i = 0; i < upgrades.Count; i++)
        {
            var upd = upgrades[i];
            int lvl = GameManager.Instance.save.GetLevel(upd.id);

            if (lvl >= upd.maxLevel) nodeImages[i].color = COL_ACTIVE;
            else if (CanBuy(i)) nodeImages[i].color = COL_AVAILABLE;
            else nodeImages[i].color = COL_LOCKED;

            nodeTexts[i].text = $"{upd.name}\nLv {lvl}/{upd.maxLevel}";
        }
        UpdateLines();
    }

    private bool CanBuy(int index)
    {
        var upd = upgrades[index];
        int lvl = GameManager.Instance.save.GetLevel(upd.id);
        if (lvl >= upd.maxLevel) return false;
        return GameManager.Instance.save.currency >= upd.costs[lvl];
    }

    private void TryBuy(int index)
    {
        if (!CanBuy(index)) return;

        var upd = upgrades[index];
        int lvl = GameManager.Instance.save.GetLevel(upd.id);

        GameManager.Instance.save.currency -= upd.costs[lvl];
        GameManager.Instance.save.SetLevel(upd.id, lvl + 1);

        GameManager.Instance.SaveGame();
        GameManager.Instance.ApplyUpgrades();
        UpdateVisuals();
    }

    private void UpdateCurrencyText()
    {
        currencyText.text = $"Currency: {GameManager.Instance.save.currency:F0}";
    }
}