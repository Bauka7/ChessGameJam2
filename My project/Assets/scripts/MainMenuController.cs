using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Header("Scene name of your game scene")]
    public string gameSceneName = "Game";

    public void PlayGame()
    {
        SceneManager.LoadScene(gameSceneName);
    }

    public void ExitGame()
    {
        Application.Quit();

#if UNITY_EDITOR
        // Чтобы Exit работал в Editor (в Build и так работает)
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}