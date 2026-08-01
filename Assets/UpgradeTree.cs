using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;

public class UpgradeTree : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private RectTransform upgradesContainer;
    [SerializeField] private GameObject nodeTemplate;
    [SerializeField] private TextMeshProUGUI currencyText;
    [SerializeField] private Button PlayButton;

    [Header("Layout")]
    [SerializeField] private Vector2 treeCenter = new Vector2(0, 0);

    private static readonly Color COL_ACTIVE = new Color(0.1f, 0.2f, 0.1f);
    private static readonly Color COL_AVAILABLE = new Color(0.2f, 0.2f, 0.1f);
    private static readonly Color COL_LOCKED = new Color(0.1f, 0.1f, 0.1f);

    // CHANGED: Replaced 'angle' and 'radius' with 'x' and 'y' position offsets
    private List<UpgradeData> upgrades = new List<UpgradeData>
    {
        new UpgradeData { id = UpgradeId.BulletDamage,       name = "Bullet Damage",      cost = (int)Mathf.Round(10f/3f),x = -150f, y =  150f },
        new UpgradeData { id = UpgradeId.FireRate,           name = "Fire Rate",          cost = 10,                      x =  -50f, y =  150f },
        new UpgradeData { id = UpgradeId.LightningDamage,    name = "Lightning Dmg",      cost = 50,                      x =   50f, y =  150f },
        new UpgradeData { id = UpgradeId.LightningBounces,   name = "Lightning Bounces",  cost = (int)Mathf.Round(80f/3f),x =  150f, y =  150f },
        new UpgradeData { id = UpgradeId.PoisonDamagePerSec, name = "Poison Dmg/s",       cost = 60,                      x = -150f, y =    0f },
        new UpgradeData { id = UpgradeId.PoisonDuration,     name = "Poison Duration",    cost = (int)Mathf.Round(80f/3f),x =  -50f, y =    0f },
        new UpgradeData { id = UpgradeId.CurrencyMultiplier, name = "Currency Mult",      cost = 20,                      x =   50f, y =    0f },
        new UpgradeData { id = UpgradeId.PlayerHealth,       name = "Player Health",      cost = 10,                      x =  150f, y =    0f }
    };

    private List<RectTransform> nodeTransforms = new List<RectTransform>();
    private List<Image> nodeImages = new List<Image>();
    private List<TextMeshProUGUI> nodeTexts = new List<TextMeshProUGUI>();

    void Start()
    {
        nodeTransforms.Clear();
        nodeImages.Clear();
        nodeTexts.Clear();

        CreateNodes();
        UpdateVisuals();

        if (PlayButton != null)
        {
            PlayButton.onClick.RemoveAllListeners();

            EventTrigger trigger = PlayButton.gameObject.GetComponent<EventTrigger>() ?? PlayButton.gameObject.AddComponent<EventTrigger>();
            trigger.triggers.Clear();

            EventTrigger.Entry entry = new EventTrigger.Entry();
            entry.eventID = EventTriggerType.PointerDown;

            entry.callback.AddListener((data) =>
            {
                PointerEventData eventData = (PointerEventData)data;

                GameManager.Instance.StartRound();

                if (PlayButton.transform.parent != null)
                {
                    ExecuteEvents.ExecuteHierarchy(PlayButton.transform.parent.gameObject, eventData, ExecuteEvents.pointerDownHandler);
                }
            });

            trigger.triggers.Add(entry);
            PlayButton.transform.SetAsLastSibling();
        }
    }

    void Update()
    {
        HandleVisibility();

        if (upgradesContainer != null && upgradesContainer.gameObject.activeSelf)
        {
            UpdateCurrencyText();
        }
    }

    private void HandleVisibility()
    {
        if (GameManager.Instance == null || upgradesContainer == null || PlayButton == null) return;

        var currentState = GameManager.Instance.CurrentState;

        if (currentState == GameManager.GameState.Play)
        {
            upgradesContainer.gameObject.SetActive(false);
            PlayButton.gameObject.SetActive(false);
        }
        else if (currentState == GameManager.GameState.Upgrades)
        {
            PlayButton.gameObject.SetActive(true);

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

    private void CreateNodes()
    {
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
            float posX = upgrades[i].x;
            float posY = upgrades[i].y;
            Vector2 pos = treeCenter + new Vector2(posX, posY);

            nodeTransforms[i].anchoredPosition = pos;
        }
    }

    private void UpdateVisuals()
    {
        for (int i = 0; i < upgrades.Count; i++)
        {
            var upd = upgrades[i];
            int lvl = GameManager.Instance.save.GetLevel(upd.id);

            if (CanBuy(i)) nodeImages[i].color = COL_AVAILABLE;
            else if (lvl > 0) nodeImages[i].color = COL_ACTIVE;
            else nodeImages[i].color = COL_LOCKED;

            nodeTexts[i].text = $"{upd.name}\nLv {lvl}\nNC: {CostValue(upd.cost, lvl)}";
        }
    }

    private float CostValue(int baseCost, int index)
    {
        return Mathf.Round(baseCost * Mathf.Pow(3f, index));
    }

    private bool CanBuy(int index)
    {
        var upd = upgrades[index];
        int lvl = GameManager.Instance.save.GetLevel(upd.id);
        return GameManager.Instance.save.currency >= CostValue(upd.cost, lvl);
    }

    private void TryBuy(int index)
    {
        if (!CanBuy(index)) return;

        var upd = upgrades[index];
        int lvl = GameManager.Instance.save.GetLevel(upd.id);

        GameManager.Instance.save.currency -= CostValue(upd.cost, lvl);
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