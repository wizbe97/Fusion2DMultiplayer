using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Threading;

public class NetworkRunnerController : MonoBehaviour, INetworkRunnerCallbacks
{
    private List<SessionInfo> availableRooms = new List<SessionInfo>();
    [SerializeField] private NetworkRunner networkRunnerPrefab;

    public NetworkRunner networkRunnerInstance;
    public async void StartGame(GameMode mode, string roomName)
    {
        if (networkRunnerInstance == null)
        {
            networkRunnerInstance = Instantiate(networkRunnerPrefab);
        }
        //Register so we will get the callbacks as well
        networkRunnerInstance.AddCallbacks(this);
        var sceneManager = networkRunnerInstance.GetComponent<NetworkSceneManagerDefault>();
        if (sceneManager == null)
        {
            sceneManager = networkRunnerInstance.gameObject.AddComponent<NetworkSceneManagerDefault>();
        }
        var startGameArgs = new StartGameArgs()
        {
            GameMode = mode,
            SessionName = roomName,
            PlayerCount = 4,
            SceneManager = sceneManager
        };
        CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        var result = await networkRunnerInstance.StartGame(startGameArgs);
        cancellationTokenSource.Cancel();
        if (result.Ok)
        {
            const string SCENE_NAME = "Lobby_Online";
            await networkRunnerInstance.LoadScene(SCENE_NAME);
            Debug.Log("Scene loading finished");
        }
        else
        {
            Debug.LogError($"Failed to start: {result.ShutdownReason}");
        }
    }

    public void JoinRandomRooom()
    {
        Debug.Log($"------------JoinRandomRoom!------------");
        StartGame(GameMode.AutoHostOrClient, string.Empty);
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {

        Debug.Log("OnPlayerJoined");

    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log("OnPlayerLeft");
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        // Debug.Log("OnInput");
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
    {
        // Debug.Log("OnInputMissing");
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {


        Debug.LogError($"Fusion Shutdown - Reason: {shutdownReason}");
        // Optional: show a UI message or delay before going back
        SceneManager.LoadScene("MainMenu");
    }

    public void OnConnectedToServer(NetworkRunner runner)
    {
        Debug.Log("OnConnectedToServer");
    }

    public void OnDisconnectedFromServer(NetworkRunner runner)
    {
        Debug.Log("OnDisconnectedFromServer");
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
    {
        Debug.Log("OnConnectRequest");
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        Debug.Log("OnConnectFailed");
    }

    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)
    {
        Debug.Log("OnUserSimulationMessage");
    }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        foreach (var session in sessionList)
        {
            if (session.IsVisible && session.IsOpen)
            {
                availableRooms.Add(session);
                Debug.Log($"Room: {session.Name}, Players: {session.PlayerCount}/{session.MaxPlayers}");
            }
        }
        Debug.Log("OnSessionListUpdated");
    }

    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
    {
        Debug.Log("OnCustomAuthenticationResponse");
    }

    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
    {
        Debug.Log("OnHostMigration");
    }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ArraySegment<byte> data)
    {
        Debug.Log("OnReliableDataReceived");
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        // Debug.Log("OnSceneLoadDone");
    }

    public void OnSceneLoadStart(NetworkRunner runner)
    {
        // Debug.Log("OnSceneLoadStart");
    }


    #region SceneChange


    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
        throw new NotImplementedException();
    }

    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
        throw new NotImplementedException();
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        throw new NotImplementedException();
    }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
    {
        throw new NotImplementedException();
    }

    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
    {
        throw new NotImplementedException();
    }
    #endregion
}
