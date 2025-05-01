using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Fusion;
using FusionUtilsEvents;

public class LobbyCanvas : MonoBehaviour
{
    public GameLauncher launcher;

    public FusionEvent OnPlayerJoinedEvent;
    public FusionEvent OnPlayerLeftEvent;
    public FusionEvent OnShutdownEvent;
    public FusionEvent OnPlayerDataSpawnedEvent;
    public int sceneIndexToLoad;

    [Header("UI")]
    [SerializeField] private GameObject panelLobby;
    [SerializeField] private TextMeshProUGUI playerListText;
    [SerializeField] private TextMeshProUGUI roomNameText;
    [SerializeField] private Button startButton;

    private void Start()
    {
        int saveSlot = PlayerPrefs.GetInt("SaveSlot", 0);
        bool isOnline = PlayerPrefs.GetInt("PlayOnline", 0) == 1;
        Debug.Log($"[LobbyCanvas] SaveSlot={saveSlot}, PlayOnline={isOnline}");

        if (isOnline)
        {
            launcher = FindObjectOfType<GameLauncher>();
            var levelManager = FindObjectOfType<LevelManager>();
            Debug.Log("[LobbyCanvas] Launching GameLauncher...");
            launcher.LaunchGame(GameMode.AutoHostOrClient, $"session_{saveSlot}", levelManager);
        }

        panelLobby.SetActive(false);
        startButton.gameObject.SetActive(false);
    }


    private void OnEnable()
    {
        OnPlayerJoinedEvent.RegisterResponse(ShowLobbyUI);
        OnShutdownEvent.RegisterResponse(HideLobbyUI);
        OnPlayerLeftEvent.RegisterResponse(UpdatePlayerList);
        OnPlayerDataSpawnedEvent.RegisterResponse(UpdatePlayerList);
    }

    private void OnDisable()
    {
        OnPlayerJoinedEvent.RemoveResponse(ShowLobbyUI);
        OnShutdownEvent.RemoveResponse(HideLobbyUI);
        OnPlayerLeftEvent.RemoveResponse(UpdatePlayerList);
        OnPlayerDataSpawnedEvent.RemoveResponse(UpdatePlayerList);
    }

    private void ShowLobbyUI(PlayerRef player, NetworkRunner runner)
    {
        panelLobby.SetActive(true);
        UpdatePlayerList(player, runner);
    }

    private void HideLobbyUI(PlayerRef player, NetworkRunner runner)
    {
        panelLobby.SetActive(false);
    }

    private void UpdatePlayerList(PlayerRef _, NetworkRunner runner)
    {
        string players = "";
        foreach (var player in runner.ActivePlayers)
        {
            var data = GameManager.Instance.GetPlayerData(player);
            string isYou = player == runner.LocalPlayer ? " (You)" : "";
            string name = data != null ? data.DisplayName.ToString() : $"Player {player.PlayerId}";
            players += $"{name}{isYou}\n";
        }

        playerListText.text = players;
        roomNameText.text = $"Room: {runner.SessionInfo.Name}";
        startButton.gameObject.SetActive(runner.IsServer && CanStartGame(runner));
    }

    private bool CanStartGame(NetworkRunner runner)
    {
        var players = runner.ActivePlayers;
        if (new List<PlayerRef>(runner.ActivePlayers).Count != 2) return false;

        var selected = new HashSet<int>();
        foreach (var player in players)
        {
            var data = GameManager.Instance.GetPlayerData(player);
            if (data == null || !data.IsReady) return false;
            selected.Add(data.SelectedCharacter);
        }

        return selected.Count == 2; // must have different characters
    }

    public void StartGameButtonPressed()
    {
        if (FusionHelper.LocalRunner.IsServer)
        {
            FusionHelper.LocalRunner.SessionInfo.IsOpen = false;
            FusionHelper.LocalRunner.SessionInfo.IsVisible = false;

            LoadingManager.Instance.LoadSpecificLevel(FusionHelper.LocalRunner, sceneIndexToLoad);
        }
    }
}
