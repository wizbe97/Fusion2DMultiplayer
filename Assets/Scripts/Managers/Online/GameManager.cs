using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Fusion;
using FusionUtilsEvents;

public class GameManager : MonoBehaviour {
    public static GameManager Instance;

    [Header("Events")]
    public FusionEvent OnPlayerLeftEvent;
    public FusionEvent OnRunnerShutDownEvent;

    public enum GameState {
        Lobby,
        Playing,
        Loading
    }

    public GameState State { get; private set; }

    [Header("Scene/Session")]
    public LevelManager LevelManager;
    [SerializeField] private GameObject exitCanvas;

    // Optional: store player-specific data
    private Dictionary<PlayerRef, PlayerData> _playerData = new();

    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(this.transform.parent ? this.transform.parent.gameObject : this.gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(transform.parent ? transform.parent.gameObject : gameObject);
    }

    private void OnEnable() {
        OnPlayerLeftEvent?.RegisterResponse(PlayerDisconnected);
        OnRunnerShutDownEvent?.RegisterResponse(OnRunnerShutdown);
    }

    private void OnDisable() {
        OnPlayerLeftEvent?.RemoveResponse(PlayerDisconnected);
        OnRunnerShutDownEvent?.RemoveResponse(OnRunnerShutdown);
    }

    private void Update() {
        if (State == GameState.Playing && Input.GetKeyDown(KeyCode.Escape)) {
            if (exitCanvas != null) {
                exitCanvas.SetActive(!exitCanvas.activeSelf);
            }
        }
    }

    public void SetGameState(GameState state) {
        State = state;
    }

    public void SetPlayerDataObject(PlayerRef player, PlayerData data) {
        if (!_playerData.ContainsKey(player)) {
            _playerData.Add(player, data);
        }
    }

    public PlayerData GetPlayerData(PlayerRef player) {
        return _playerData.TryGetValue(player, out var data) ? data : null;
    }

    public void PlayerDisconnected(PlayerRef player, NetworkRunner runner) {
        if (_playerData.TryGetValue(player, out var data)) {
            if (data.Instance) runner.Despawn(data.Instance);
            if (data.Object) runner.Despawn(data.Object);
            _playerData.Remove(player);
        }
    }

    public void OnRunnerShutdown(PlayerRef _, NetworkRunner runner) {
        ExitSession();
    }

    public void LeaveRoom() {
        _ = LeaveRoomAsync();
    }

    private async Task LeaveRoomAsync() {
        await ShutdownRunner();
    }

    private async Task ShutdownRunner() {
        if (FusionHelper.LocalRunner != null) {
            await FusionHelper.LocalRunner.Shutdown();
        }

        SetGameState(GameState.Lobby);
        _playerData.Clear();
    }

    public void ExitSession() {
        _ = ShutdownRunner();
        LevelManager?.ResetLoadedScene();
        SceneManager.LoadScene(0); // Main Menu
        if (exitCanvas != null)
            exitCanvas.SetActive(false);
    }

    public void ExitGame() {
        _ = ShutdownRunner();
        Application.Quit();
    }
}
