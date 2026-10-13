using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class QALInformationPanel : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Panel Root")]
    public GameObject panelRoot;

    [Header("Text Area")]
    public TextMeshProUGUI infoText;

    [TextArea(3, 10)]
    [Header("Hover Message")]
    public string hoverMessage =
        "In a real vehicle, the ECU automatically detects when a valid key is inside the cabin.\n\n" +
        "This can be done using RF, BLE, UWB, or NFC signals.\n\n" +
        "The Activate Key button represents that moment:\n" +
        "• The vehicle senses the key nearby\n" +
        "• The ECU confirms proximity\n" +
        "• The system prepares for secure authentication\n\n" +
        "This is the first step before any digital key or ultrasonic token validation.";

    void Start()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    // ============================================================================================================
    //  POINTER ENTER — SHOW PANEL
    // ============================================================================================================
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (panelRoot != null)
            panelRoot.SetActive(true);

        if (infoText != null)
            infoText.text = hoverMessage;
    }

    // ============================================================================================================
    //  POINTER EXIT — HIDE PANEL
    // ============================================================================================================
    public void OnPointerExit(PointerEventData eventData)
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }
}
