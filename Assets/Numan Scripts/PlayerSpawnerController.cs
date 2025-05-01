using Fusion;
using UnityEngine;


public class PlayerSpawnerController : NetworkBehaviour, IPlayerJoined, IPlayerLeft
{
    public static PlayerSpawnerController instance;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private NetworkPrefabRef playerNetworkPrefab = NetworkPrefabRef.Empty;

    void Awake()
    {
        instance = this;
    }

    public override void Spawned()
    {
        if (Runner.IsServer)
        {
            foreach (var item in Runner.ActivePlayers)
            {
                SpawnPlayer(item);
            }
        }
    }

    private void SpawnPlayer(PlayerRef playerRef)
    {
        // var index = playerRef.RawEncoded % spawnPoints.Length;
        if (Runner.IsServer)
        {
            var spawnPoint = spawnPoints[1].transform.position;
            var playerObject = Runner.Spawn(playerNetworkPrefab, spawnPoint, Quaternion.identity, playerRef);

            Runner.SetPlayerObject(playerRef, playerObject);
            Debug.Log($"Player {playerRef} spawned at {spawnPoint}");
        }
    }




    public void PlayerLeft(PlayerRef player)
    {
        // throw new System.NotImplementedException();
    }

    public void PlayerJoined(PlayerRef player)
    {
        Debug.Log($"Player {player} joined");
    }
}
