using UnityEngine;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

[DisallowMultipleComponent]
public class DestructionTracker : MonoBehaviour
{
    void Awake()
    {
        Debug.Log($"<color=#00FF00>[TRACKER INITIALIZED]</color> Monitoring: <b>{gameObject.name}</b> (Scene: {gameObject.scene.name})", this);
    }

    void OnDisable()
    {
        // Logs if the object is being disabled or hidden
        Debug.LogWarning($"<color=#FFA500>[TRACKER: DISABLED]</color> <b>{gameObject.name}</b> was disabled/hidden.", this);
    }

    void OnDestroy()
    {
        // Capture the full code execution stack trace
        StackTrace stackTrace = new StackTrace(1, true);

        Debug.LogError(
            $"<color=#FF0000><b>[!!! DESTRUCTION DETECTED !!!]</b></color>\n" +
            $"GameObject: <b>{gameObject.name}</b> was just DESTROYED!\n" +
            $"<b>Culprit Call Stack:</b>\n{stackTrace}",
            this
        );
    }
}