using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneController : MonoBehaviour
{
    [SerializeField] private GameObject popupPanel;

    public void LoadScene(int sceneIndex)
    {
        // If we're going to main menu (scene 0), reset the GameManager
        if (sceneIndex == 0 && GameManager.Instance != null)
        {
            Destroy(GameManager.Instance.gameObject);
        }

        SceneManager.LoadScene(sceneIndex);

        // If loading the game scene, wait and initialize
        if (sceneIndex == 1)
        {
            StartCoroutine(InitializeGameScene());
        }
    }

    private IEnumerator InitializeGameScene()
    {
        // Wait for the scene to load
        yield return null;

        // Find or create GameManager
        if (GameManager.Instance == null)
        {
            // Find the GameManager prefab in the scene
            GameObject gameManagerObject = GameObject.FindGameObjectWithTag("GameManager");
            if (gameManagerObject != null)
            {
                gameManagerObject.SetActive(true);
            }
            else
            {
                Debug.LogError("No GameManager found in the scene!");
            }
        }

        // Initialize UI and game state
        if (GameManager.Instance != null)
        {
            GameManager.Instance.InitializeGame();
        }
    }

    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    public void QuitApplication()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
    }

    public void OpenPopupPanel()
    {
        if (popupPanel != null)
        {
            popupPanel.SetActive(true);
        }
        else
        {
            Debug.LogWarning("Popup Panel reference is missing!");
        }
    }

    public void ClosePopupPanel()
    {
        if (popupPanel != null)
        {
            popupPanel.SetActive(false);
        }
    }
}
