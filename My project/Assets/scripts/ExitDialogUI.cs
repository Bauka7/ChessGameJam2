using UnityEngine;
using UnityEngine.SceneManagement;

public class ExitDialogUI : MonoBehaviour
{
    public GameObject overlay;

    // открыть окно
    public void OpenDialog()
    {
        overlay.SetActive(true);
        Time.timeScale = 0f; // пауза игры
    }

    // нажали NO
    public void CloseDialog()
    {
        overlay.SetActive(false);
        Time.timeScale = 1f;
    }

    // нажали YES
    public void ExitToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }
}