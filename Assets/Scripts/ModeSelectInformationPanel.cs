using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class ModeSelectInformationPanel : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Panel Root")]
    public GameObject panelRoot;

    [Header("Text Area")]
    public TextMeshProUGUI infoText;

    [TextArea(3, 10)]
    [Header("Hover Message")]
    public string hoverMessage;

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
