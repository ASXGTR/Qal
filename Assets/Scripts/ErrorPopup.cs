using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ErrorPopup : MonoBehaviour
{
    [Header("Panels")]
    public GameObject errorPanel;          // ErrorPanel
    public GameObject confirmCancelPanel;  // Confirm/CancelPanel (unused now)

    [Header("Error UI")]
    public TMP_Text errorTextEntry;        // ErrorTextEntry

    // ---------------------------------------------------------
    // SIMPLE ERROR POPUP (NO CONFIRM/CANCEL)
    // ---------------------------------------------------------
    public void ShowError(string message)
    {
        gameObject.SetActive(true);

        errorPanel.SetActive(true);
        confirmCancelPanel.SetActive(false);   // stays off permanently

        errorTextEntry.text = message;
    }

    // ---------------------------------------------------------
    // HIDE EVERYTHING
    // ---------------------------------------------------------
    public void Hide()
    {
        gameObject.SetActive(false);
        errorPanel.SetActive(false);
        confirmCancelPanel.SetActive(false);
    }
}
