using UnityEngine;
using System.Collections;
using TMPro;

public class OwnerModeInformationPanelController : MonoBehaviour
{
    [Header("UI")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;

    private Coroutine autoHideCoroutine;

    /// <summary>
    /// Displays a message. If autoHideSeconds > 0, automatically closes the panel after that duration.
    /// </summary>
    public void ShowInfo(string message, float autoHideSeconds = 0f)
    {
        // Cancel any pending auto-hide timer
        if (autoHideCoroutine != null)
        {
            StopCoroutine(autoHideCoroutine);
            autoHideCoroutine = null;
        }

        // 1. Activate panel
        if (infoPanel != null)
            infoPanel.SetActive(true);
        else
            gameObject.SetActive(true);

        // 2. Apply and force-refresh text
        if (infoText != null)
        {
            infoText.text = message;
            infoText.ForceMeshUpdate();
        }
        else
        {
            Debug.LogWarning("[OwnerInfo] 'infoText' is not assigned in the Inspector!", this);
        }

        // 3. Start auto-hide timer if requested
        if (autoHideSeconds > 0f)
        {
            autoHideCoroutine = StartCoroutine(AutoHideRoutine(autoHideSeconds));
        }
    }

    private IEnumerator AutoHideRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        HideInfo();
    }

    public void HideInfo()
    {
        if (autoHideCoroutine != null)
        {
            StopCoroutine(autoHideCoroutine);
            autoHideCoroutine = null;
        }

        if (infoPanel != null)
            infoPanel.SetActive(false);
        else
            gameObject.SetActive(false);
    }
}