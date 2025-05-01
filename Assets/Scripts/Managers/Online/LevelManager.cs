using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Fusion;

public class LevelManager : NetworkSceneManagerDefault
{

    [HideInInspector] public FusionLauncher Launcher;
    // [SerializeField] private LoadingManager loadingManager;

    private Scene _loadedScene;

    public void ResetLoadedScene()
    {
        _loadedScene = default;
        // if (loadingManager != null)
        //     loadingManager.ResetLastLevelsIndex();
    }

    /// <summary>
    /// Overrides Fusion scene loading to hook in loading screen and state changes.
    /// </summary>
    protected override IEnumerator LoadSceneCoroutine(SceneRef sceneRef, NetworkLoadSceneParameters sceneParams)
    {
        // if (loadingManager != null)
        // {
        //     loadingManager.StartLoadingScreen();
        // }
        GameManager.Instance?.SetGameState(GameManager.GameState.Loading);
        Launcher.SetConnectionStatus(FusionLauncher.ConnectionStatus.Loading, "");
        yield return new WaitForSeconds(0.5f); // optional delay
        yield return base.LoadSceneCoroutine(sceneRef, sceneParams);
        Launcher.SetConnectionStatus(FusionLauncher.ConnectionStatus.Loaded, "");
        yield return new WaitForSeconds(1f);
        // if (loadingManager != null)
        //     loadingManager.FinishLoadingScreen();
        GameManager.Instance?.SetGameState(GameManager.GameState.Playing);
    }
}
