using UnityEngine;
using TMPro;

public class AutoClearInput : MonoBehaviour
{
    // ---------------------------------------------------------
    // TMP INPUT FIELD REFERENCE
    // ---------------------------------------------------------
    // This component is attached directly to a TMP_InputField.
    // The script ensures that the field is always cleared whenever
    // it becomes active, inactive, or is first created.
    //
    // This prevents sensitive text (e.g., passwords or security
    // credentials) from being restored by Unity’s internal caching
    // or UI refresh behaviour.
    private TMP_InputField inputField;

    // ---------------------------------------------------------
    // UNITY LIFECYCLE: Awake()
    // ---------------------------------------------------------
    // Awake() is called when the object is first created.
    // We fetch the TMP_InputField component and immediately wipe it
    // to ensure no cached text appears when the UI loads.
    void Awake()
    {
        inputField = GetComponent<TMP_InputField>();
        Wipe();
    }

    // ---------------------------------------------------------
    // UNITY LIFECYCLE: OnEnable()
    // ---------------------------------------------------------
    // OnEnable() is called whenever the GameObject becomes active.
    // TMP can sometimes restore previous text when UI panels are
    // re-enabled, so we wipe the field again to guarantee a clean state.
    void OnEnable()
    {
        Wipe();
    }

    // ---------------------------------------------------------
    // UNITY LIFECYCLE: OnDisable()
    // ---------------------------------------------------------
    // OnDisable() is called whenever the GameObject is deactivated.
    // Clearing the field here prevents Unity from caching the text
    // and restoring it later when the panel is re-enabled.
    void OnDisable()
    {
        Wipe();
    }

    // ---------------------------------------------------------
    // WIPE INPUT FIELD CONTENT
    // ---------------------------------------------------------
    // Wipe() clears all internal TMP buffers:
    //
    //   - inputField.text
    //   - SetTextWithoutNotify() (prevents event callbacks)
    //   - inputField.textComponent.text (rendered mesh text)
    //
    // This “triple wipe” ensures no ghost text, cached text, or
    // mesh‑rebuilt text appears when switching UI panels.
    //
    // This is especially important for password fields or any
    // sensitive input where text persistence is a security risk.
    public void Wipe()
    {
        if (inputField == null)
            inputField = GetComponent<TMP_InputField>();

        if (inputField != null)
        {
            inputField.text = "";                     // Clear main text buffer
            inputField.SetTextWithoutNotify("");      // Clear without triggering events

            if (inputField.textComponent != null)
                inputField.textComponent.text = "";   // Clear rendered text mesh
        }
    }
}
