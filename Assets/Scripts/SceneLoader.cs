using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    // ============================================================================================================
    //  SCENE LOADER — MULTI-SCENE WORKFLOW CONTROLLER
    // ============================================================================================================

    [Header("Scene Build Configuration")]
    [SerializeField] private string keyDetectionScene = "QAL";                 // Scene 0: Key Ingress Wakeup
    [SerializeField] private string modeSelectionScene = "ModeSelect";         // Scene 1: Mode Select (Dealer vs Consumer)
    [SerializeField] private string initialPasswordScene = "InitialPassword";  // Scene 2: Consumer First-Time Setup
    [SerializeField] private string demoScene = "DemoScene";                   // Scene 3: Full Owner Vehicle HMI
    [SerializeField] private string dealershipScene = "DealershipScene";       // Scene 4: Dedicated Showroom Dealership Mode

    private const string DEALERSHIP_PASSWORD_KEY = "ECU_DEALERSHIP_PASSWORD";

    // ============================================================================================================
    // ENTRY POINT — CALLED FROM Scene 0 (QAL)
    // ============================================================================================================
    public void LoadNextScene()
    {
        if (FakeECU.Instance != null)
        {
            FakeECU.Instance.RefreshFromStorage();

            if (FakeECU.Instance.IsUserPasswordSet)
            {
                SceneManager.LoadScene(demoScene);
                return;
            }
        }
        else
        {
            bool hasVault = PlayerPrefs.HasKey("ECU_USER_SALT") &&
                            PlayerPrefs.HasKey("ECU_ENCRYPTED_SECRET") &&
                            PlayerPrefs.HasKey("ECU_ITERATIONS");

            if (hasVault || PlayerPrefs.GetInt("ConsumerPasswordSet", 0) == 1)
            {
                SceneManager.LoadScene(demoScene);
                return;
            }
        }

        SceneManager.LoadScene(modeSelectionScene);
    }

    // ============================================================================================================
    // BUTTON: DEALERSHIP MODE (CALLED FROM ModeSelect SCENE)
    // ============================================================================================================
    public void SelectDealershipMode()
    {
        // Loads the password-gated Dealership Showroom scene directly
        SceneManager.LoadScene(dealershipScene);
    }

    // ============================================================================================================
    // BUTTON: CONSUMER MODE (CALLED FROM ModeSelect SCENE)
    // ============================================================================================================
    public void SelectConsumerMode()
    {
        PlayerPrefs.DeleteKey(DEALERSHIP_PASSWORD_KEY);
        PlayerPrefs.Save();

        if (FakeECU.Instance != null)
        {
            FakeECU.Instance.RefreshFromStorage();

            if (FakeECU.Instance.IsUserPasswordSet)
            {
                SceneManager.LoadScene(demoScene);
                return;
            }
        }
        else
        {
            if (PlayerPrefs.GetInt("ConsumerPasswordSet", 0) == 1)
            {
                SceneManager.LoadScene(demoScene);
                return;
            }
        }

        SceneManager.LoadScene(initialPasswordScene);
    }

    // ============================================================================================================
    // GENERIC SCENE LOADERS
    // ============================================================================================================
    public void LoadDemoScene() => SceneManager.LoadScene(demoScene);
    public void LoadModeSelectionScene() => SceneManager.LoadScene(modeSelectionScene);
    public void LoadInitialPasswordScene() => SceneManager.LoadScene(initialPasswordScene);
    public void LoadQALScene() => SceneManager.LoadScene(keyDetectionScene);
    public void LoadDealershipScene() => SceneManager.LoadScene(dealershipScene);
}