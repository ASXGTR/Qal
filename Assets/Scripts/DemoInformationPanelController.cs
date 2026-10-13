using UnityEngine;
using TMPro;

public class DemoInformationPanelController : MonoBehaviour
{
    public static DemoInformationPanelController Instance;

    public GameObject panelRoot;
    public TextMeshProUGUI infoText;

    void Awake()
    {
        Instance = this;

        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    // ---------------------------------------------------------
    // GENERIC SHOW/HIDE
    // ---------------------------------------------------------
    public void Show(string message)
    {
        if (panelRoot == null || infoText == null)
            return;

        infoText.text = message;
        panelRoot.SetActive(true);
    }

    public void Hide()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    // ---------------------------------------------------------
    // SYSTEM / AUTHENTICATION MESSAGES (3‑line max)
    // ---------------------------------------------------------
    public void ShowAuthSuccess(string sanitized)
    {
        Show(
            $"Owner Mode Auth Success\n" +
            $"Sanitized password '{sanitized}' accepted.\n" +
            $"ECU PBKDF2 hash matched."
        );
    }

    public void ShowAuthFailure(string sanitized)
    {
        Show(
            $"Authentication Failed\n" +
            $"Sanitized password '{sanitized}' did not match.\n" +
            $"ECU hash mismatch."
        );
    }

    public void ShowLockout(int seconds)
    {
        Show(
            $"Owner Mode Locked\n" +
            $"Too many failed attempts.\n" +
            $"ECU timer active: {seconds}s"
        );
    }

    public void ShowLockoutCleared()
    {
        Show(
            $"Lockout Cleared\n" +
            $"ECU timer expired.\n" +
            $"You may try again."
        );
    }

    public void ShowFactoryReset()
    {
        Show(
            $"[ECU DIAGNOSTIC RESET]\n" +
            $"All security vaults & keys wiped.\n" +
            $"Restoring Factory Mode (0x00)..."
        );
    }

    public void ShowDelivery(string message)
    {
        Show(message);
    }

    public void ShowBluetooth(string message)
    {
        Show(message);
    }

    // ---------------------------------------------------------
    // HOVER SYSTEM
    // ---------------------------------------------------------
    public void OnHoverEnter(string message) => Show(message);
    public void OnHoverExit() => Hide();

    public void HoverManufacturer() { Show("Manufacturer - The company that produced the vehicle this ECU is installed in."); }
    public void HoverModel() { Show("Model - The specific vehicle model associated with this ECU."); }
    public void HoverYear() { Show("Year - The year this vehicle was manufactured."); }
    public void HoverVIN() { Show("VIN - The unique Vehicle Identification Number assigned at manufacturing."); }
    public void HoverSerial() { Show("ECU Serial - The unique serial number of this Electronic Control Unit."); }
    public void HoverMode() { Show("ECU Mode - Indicates whether the ECU is running in Demo or Simulation mode."); }
    public void HoverSeed() { Show("Current Seed - Cryptographic random value used to authenticate ultrasonic tokens."); }
    public void HoverFreshness() { Show("Freshness Counter - Prevents replay attacks by incrementing each challenge."); }
    public void HoverLastToken() { Show("Last Token Received - Most recent ultrasonic authentication token."); }
    public void HoverValidation() { Show("Validation Result - PASS or FAIL depending on token validity."); }
    public void HoverOverride() { Show("Service Override - ECU technician maintenance mode."); }
}