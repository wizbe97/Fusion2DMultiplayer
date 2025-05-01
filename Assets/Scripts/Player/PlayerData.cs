using UnityEngine;
using Fusion;
using FusionUtilsEvents;

public class PlayerData : NetworkBehaviour {
    [Networked] public NetworkString<_16> DisplayName { get; set; }
    [Networked] public int SelectedCharacter { get; set; }
    [Networked] public bool IsReady { get; set; }
    [Networked] public NetworkObject Instance { get; set; }

    public FusionEvent OnPlayerDataSpawnedEvent;

    private ChangeDetector _changeDetector;

    public override void Spawned() {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

        // Assign nickname automatically
        if (Object.HasInputAuthority) {
            string name = Runner.IsServer && Object.InputAuthority == Runner.LocalPlayer
                ? "Player 1"
                : "Player 2";

            RPC_SetDisplayName(name);
        }

        // Set Player Object for authority mapping
        Runner.SetPlayerObject(Object.InputAuthority, Object);

        // Store in GameManager if this player is state authority
        if (Object.HasStateAuthority) {
            GameManager.Instance.SetPlayerDataObject(Object.InputAuthority, this);
        }

        // Raise spawn event
        OnPlayerDataSpawnedEvent?.Raise(Object.InputAuthority, Runner);

        DontDestroyOnLoad(this.gameObject);
    }

    public override void Render() {
        foreach (var change in _changeDetector.DetectChanges(this)) {
            if (change == nameof(SelectedCharacter) || change == nameof(IsReady)) {
                OnPlayerDataSpawnedEvent?.Raise(Object.InputAuthority, Runner);
            }
        }
    }

    [Rpc(sources: RpcSources.InputAuthority, targets: RpcTargets.StateAuthority)]
    public void RPC_SetDisplayName(string name) {
        DisplayName = name;
    }

    [Rpc(sources: RpcSources.InputAuthority, targets: RpcTargets.StateAuthority)]
    public void RPC_SetCharacter(int characterIndex) {
        SelectedCharacter = characterIndex;
    }

    [Rpc(sources: RpcSources.InputAuthority, targets: RpcTargets.StateAuthority)]
    public void RPC_SetReady(bool ready) {
        IsReady = ready;
    }
}
