using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuUI : MonoBehaviour
{
    public GameObject panelMainMenu;
    public GameObject panelSaveSelection;

    private bool isOnline = false;
    private bool isLocal = false;
    private bool isOffline = false;

    private void Start()
    {
        panelMainMenu.SetActive(true);
        panelSaveSelection.SetActive(false);
    }

    public void PlayOnline()
    {
        isOnline = true;
        isLocal = false;
        isOffline = false;
        ShowSaveSelection();
    }

    public void PlayLocal()
    {
        isOnline = true;
        isLocal = true;
        isOffline = false;
        ShowSaveSelection();
    }

    public void PlayOffline()
    {
        isOnline = false;
        isLocal = false;
        isOffline = true;
        ShowSaveSelection();
    }

    private void ShowSaveSelection()
    {
        panelMainMenu.SetActive(false);
        panelSaveSelection.SetActive(true);
    }

    public void SelectSaveSlot(int slotId)
    {
        PlayerPrefs.SetInt("SaveSlot", slotId);
        PlayerPrefs.SetInt("PlayOnline", isOnline ? 1 : 0);

        if (isOnline)
        {
            SceneManager.LoadScene("Lobby_Online");
            Debug.Log("Loading Online Lobby Scene with Save Slot: " + slotId);
        }
        else
        {
            SceneManager.LoadScene("Lobby_Offline");
            Debug.Log("Loading Offline Lobby Scene with Save Slot: " + slotId);
        }
    }

    public void BackToMainMenu()
    {
        panelSaveSelection.SetActive(false);
        panelMainMenu.SetActive(true);
        isOnline = false;
        isLocal = false;
        isOffline = false;
    }

    public void OpenSettings()
    {
        // TODO: Open settings panel
        Debug.Log("Open Settings Panel");
    }

    public void ExitGame()
    {
        isOnline = false;
        isLocal = false;
        isOffline = false;
        Application.Quit();
    }
}
