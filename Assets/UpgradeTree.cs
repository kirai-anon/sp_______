using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class UpgradeTree : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private RectTransform upgradesContainer;
    [SerializeField] private GameObject nodeTemplate;
    [SerializeField] private GameObject lineTemplate;
    [SerializeField] private TextMeshProUGUI currencyText;
    [SerializeField] private Button PlayButton;

    [Header("Layout")]
    [SerializeField] private float treeRadius = 200f;
    [SerializeField] private Vector2 treeCenter = new Vector2(0, 0);

    private static readonly Color COL_ACTIVE = new Color(0.1f, 0.2f, 0.1f);
    private static readonly Color COL_AVAILABLE = new Color(0.2f, 0.2f, 0.1f);
    private static readonly Color COL_LOCKED = new Color(0.1f, 0.1f, 0.1f);

    private List<UpgradeData> upgrades = new List<UpgradeData>
    {
        new UpgradeData { id = UpgradeId.BulletDamage,       name = "Bullet Damage",      cost = 10,  angle = 0f,                 radius = 100f },
        new UpgradeData { id = UpgradeId.FireRate,           name = "Fire Rate",          cost = 10,  angle = Mathf.PI / 3f,      radius = 300f },
        new UpgradeData { id = UpgradeId.LightningDamage,    name = "Lightning Dmg",      cost = 30,  angle = 2f * Mathf.PI / 3f, radius = 200f },
        new UpgradeData { id = UpgradeId.LightningBounces,   name = "Lightning Bounces", cost = 50,  angle = Mathf.PI,            radius = 100f },
        new UpgradeData { id = UpgradeId.PoisonDamagePerSec, name = "Poison Dmg/s",       cost = 30,  angle = 4f * Mathf.PI / 3f, radius = 200f },
        new UpgradeData { id = UpgradeId.PoisonDuration,     name = "Poison Duration",   cost = 40,  angle = 5f * Mathf.PI / 3f, radius = 300f },
        new UpgradeData { id = UpgradeId.CurrencyMultiplier, name = "Currency Mult",      cost = 50,  angle = Mathf.PI / 2f,      radius = 100f },
    };

    // FIX: Changed type from Button to RectTransform
    private List<RectTransform> nodeTransforms = new List<RectTransform>();
    private List<Image> nodeImages = new List<Image>();
    private List<TextMeshProUGUI> nodeTexts = new List<TextMeshProUGUI>();
    private List<GameObject> lineObjects = new List<GameObject>();

    private bool dragging = false;
    private float currentRotation = 0f;
    private float lastMouseAngle = 0f;

    private float minRotation = Mathf.Infinity;
    private float maxRotation = Mathf.Infinity;
    [SerializeField] private float rotMult = 5 * Mathf.PI;

    void Start()
    {
        nodeTransforms.Clear();
        nodeImages.Clear();
        nodeTexts.Clear();
        lineObjects.Clear();


        CreateLines();
        CreateNodes();
        UpdateVisuals();

        if (PlayButton != null)
        {
            PlayButton.onClick.RemoveAllListeners();
            PlayButton.onClick.AddListener(() => GameManager.Instance.StartRound());
            PlayButton.transform.SetAsLastSibling();
        }
    }

    void Update()
    {
        // FIX: Handle Dynamic Visibility States
        HandleVisibility();

        // Only allow dragging and math processing if the upgrades are active/visible
        if (upgradesContainer != null && upgradesContainer.gameObject.activeSelf)
        {
            HandleDrag();
            UpdateCurrencyText();

            if (minRotation == Mathf.Infinity)
            {
                minRotation = 0f;
                maxRotation = 0f;
                for (int i = 0; i < upgrades.Count; i++)
                {
                    float indexRotation = upgrades[i].angle;
                    if (indexRotation < minRotation) minRotation = indexRotation;
                    if (indexRotation > maxRotation) maxRotation = indexRotation;
                }
            }
        }
    }

    private void HandleVisibility()
    {
        if (GameManager.Instance == null || upgradesContainer == null || PlayButton == null) return;

        var currentState = GameManager.Instance.CurrentState;

        // 1. Check GameState Conditions
        if (currentState == GameManager.GameState.Play)
        {
            upgradesContainer.gameObject.SetActive(false);
            PlayButton.gameObject.SetActive(false);
            if (currencyText != null) currencyText.gameObject.SetActive(false);
        }
        else if (currentState == GameManager.GameState.Upgrades)
        {
            // Play Button always shows during Upgrades state
            PlayButton.gameObject.SetActive(true);

            // 2. FIX: Check "Played" Key. If player hasn't played yet, hide the tree elements.
            if (!PlayerPrefs.HasKey("Played"))
            {
                upgradesContainer.gameObject.SetActive(false);
                if (currencyText != null) currencyText.gameObject.SetActive(false);
            }
            else
            {
                upgradesContainer.gameObject.SetActive(true);
                if (currencyText != null) currencyText.gameObject.SetActive(true);
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

            currentRotation += delta * 3f;
            currentRotation = currentRotation % 360;

            lastMouseAngle = mouseAngle;
            PositionNodes();
        }
    }

    private void CreateNodes()
    {
        // FIX: Spawns inside upgradesContainer instead of canvas root
        Transform parent = upgradesContainer != null ? upgradesContainer : canvas.transform;

        for (int i = 0; i < upgrades.Count; i++)
        {
            GameObject node = Instantiate(nodeTemplate, parent);
            node.SetActive(true);

            RectTransform rectTrans = node.GetComponent<RectTransform>();
            Button btn = node.GetComponent<Button>();
            Image img = node.GetComponent<Image>();

            if (btn == null || rectTrans == null) continue;

            TextMeshProUGUI txt = node.GetComponentInChildren<TextMeshProUGUI>();

            int index = i;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => TryBuy(index));

            var upd = upgrades[i];
            int lvl = GameManager.Instance.save.GetLevel(upd.id);
            if (txt != null) txt.text = $"{upd.name}\nLv {lvl}";

            nodeTransforms.Add(rectTrans);
            nodeImages.Add(img);
            nodeTexts.Add(txt);
        }
        PositionNodes();
    }

    private void PositionNodes()
    {
        if (nodeTransforms == null || nodeTransforms.Count < upgrades.Count)
            return;

        for (int i = 0; i < upgrades.Count; i++)
        {
            float angle = upgrades[i].angle + currentRotation;
            float r = upgrades[i].radius;
            Vector2 pos = treeCenter + new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r);

            // FIX: This now correctly applies to RectTransform
            nodeTransforms[i].anchoredPosition = pos;
        }
        UpdateLines();
    }

    private void CreateLines()
    {
        // FIX: Spawns inside upgradesContainer instead of canvas root
        Transform parent = upgradesContainer != null ? upgradesContainer : canvas.transform;

        for (int i = 0; i < upgrades.Count; i++)
        {
            GameObject line = Instantiate(lineTemplate, parent);
            line.SetActive(true);
            lineObjects.Add(line);
        }
    }

    private void UpdateLines()
    {
        // Prevent execution if nodes haven't been generated yet
        if (nodeTransforms.Count < upgrades.Count || lineObjects.Count < upgrades.Count) return;

        for (int i = 0; i < upgrades.Count; i++)
        {
            RectTransform lineRT = lineObjects[i].GetComponent<RectTransform>();
            Vector2 start = treeCenter;

            // FIX: Safely reads the anchoredPosition from the RectTransform list
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
        UpdateLines();
        for (int i = 0; i < upgrades.Count; i++)
        {
            var upd = upgrades[i];
            int lvl = GameManager.Instance.save.GetLevel(upd.id);

            if (CanBuy(i)) nodeImages[i].color = COL_AVAILABLE;
            else if (lvl > 0) nodeImages[i].color = COL_ACTIVE;
            else nodeImages[i].color = COL_LOCKED;

            nodeTexts[i].text = $"{upd.name}\nLv {lvl}";
        }
    }

    private bool CanBuy(int index)
    {
        var upd = upgrades[index];
        int lvl = GameManager.Instance.save.GetLevel(upd.id);
        return GameManager.Instance.save.currency >= upd.cost * Mathf.Pow(2, lvl);
    }

    private void TryBuy(int index)
    {
        if (!CanBuy(index)) return;

        var upd = upgrades[index];
        int lvl = GameManager.Instance.save.GetLevel(upd.id);

        GameManager.Instance.save.currency -= upd.cost * Mathf.Pow(2, lvl);
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