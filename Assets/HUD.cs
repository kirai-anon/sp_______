using UnityEngine;
using TMPro;

public class HUD : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI currencyText;

    void Update()
    {
        currencyText.text = $"{GameManager.Instance.save.currency:F0} x{GameManager.Instance.currencyMultiplier:F1}";
    }
}