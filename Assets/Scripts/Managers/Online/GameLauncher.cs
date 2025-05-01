using UnityEngine;
using Fusion;

public class GameLauncher : MonoBehaviour {
    [SerializeField] private GameObject fusionLauncherPrefab;

    public void LaunchGame(GameMode mode, string roomName, LevelManager levelManager) {
        var existing = FindObjectOfType<FusionLauncher>();
        if (existing == null) {
            existing = Instantiate(fusionLauncherPrefab).GetComponent<FusionLauncher>();
        }

        levelManager.Launcher = existing;
        existing.Launch(mode, roomName, levelManager);
    }
}
