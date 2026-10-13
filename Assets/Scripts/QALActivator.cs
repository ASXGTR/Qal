using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class QALActivator : MonoBehaviour
{
    // ============================================================================================================
    //  QAL ACTIVATOR — VEHICLE ENTRY & PRIMARY KEY DETECTION (DEMO ENTRY POINT)
    // ============================================================================================================
    //
    //  This component represents the first step of the demo: a user entering a vehicle
    //  and the ECU detecting the presence of a valid primary key (RF/BLE/UWB/NFC).
    //
    //  The script provides:
    //      • A visual “key detected” animation (orange pulsing → green linked)
    //      • Clear status text explaining each step
    //      • Automatic transition to the next scene once the key is “linked”
    //
    //  Scene transition is now handled by SceneLoader so that the immobilizer lifecycle
    //  (Factory → Dealership → Customer) is respected.
    //
    // ============================================================================================================

    [Header("UI")]
    public Image keyImage;                 // Static key icon
    public Image glowBox;                  // Glow animation background
    public TextMeshProUGUI statusText;     // Status text shown to the user
    public Button activateKeyButton;       // Button to begin the key detection sequence

    [Header("Scene Loader")]
    public SceneLoader sceneLoader;        // Reference to SceneLoader in the scene

    // Glow colours used during animation
    private Color orangeDim = new Color(1f, 0.55f, 0f);
    private Color orangeBright = new Color(1f, 0.8f, 0.3f);
    private Color greenLinked = new Color(0f, 0.8f, 0f);

    void Start()
    {
        glowBox.gameObject.SetActive(false);
        statusText.text = "Waiting for key…";

        activateKeyButton.onClick.AddListener(OnActivateKeyPressed);
    }

    // Called when the user presses the Activate Key button
    public void OnActivateKeyPressed()
    {
        activateKeyButton.interactable = false;

        glowBox.gameObject.SetActive(true);
        glowBox.color = orangeDim;

        StartCoroutine(KeyActivationSequence());
    }

    // Animated key detection + linking sequence
    IEnumerator KeyActivationSequence()
    {
        statusText.text = "Key detected… linking…";

        float timer = 0f;
        float pulseDuration = 4f;

        // Orange pulsing animation
        while (timer < pulseDuration)
        {
            glowBox.color = Color.Lerp(orangeDim, orangeBright, Mathf.PingPong(Time.time * 2f, 1f));
            timer += Time.deltaTime;
            yield return null;
        }

        // Switch to green to indicate successful linking
        glowBox.color = greenLinked;
        statusText.text = "Key linked";

        // Hold green for 2 seconds so the user can see the final state
        yield return new WaitForSeconds(2f);

        // Use SceneLoader instead of SceneManager
        if (sceneLoader != null)
        {
            sceneLoader.LoadNextScene();
        }
        else
        {
            Debug.LogError("SceneLoader reference missing on QALActivator!");
        }
    }
}
