using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public void OnPlayPressed()
    {
        SceneManager.LoadScene(1);
    }
    public void OnApplicationQuit()
    {
        Application.Quit();
    }

}
