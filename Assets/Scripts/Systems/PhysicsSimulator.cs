using System.Collections.Generic;
using UnityEngine;

public class PhysicsSimulator : MonoBehaviour {
    // Singleton instance so any script can access the simulator
    public static PhysicsSimulator Instance { get; private set; }

    // Track all moving platforms that need physics simulation
    private readonly HashSet<IPhysicsObject> _simulatedPlatforms = new();

    // Track all players that need physics simulation
    private readonly HashSet<IPhysicsObject> _simulatedPlayers = new();

    // Tracks total time since the game started
    private float _timeSinceStart;

    private void Awake() {
        // Assign the singleton instance on load
        Instance = this;
    }

    // Called every frame to update all simulated objects
    private void Update() {
        float _deltaTime = Time.deltaTime;
        _timeSinceStart += _deltaTime;

        // Run per-frame logic for all platforms
        foreach (var platform in _simulatedPlatforms) {
            platform.TickUpdate(_deltaTime, _timeSinceStart);
        }

        // Run per-frame logic for all players
        foreach (var player in _simulatedPlayers) {
            player.TickUpdate(_deltaTime, _timeSinceStart);
        }
    }

    // Called every physics frame to update physics-based logic
    private void FixedUpdate() {
        float _deltaTime = Time.deltaTime;

        // Run fixed-step logic for platforms
        foreach (var platform in _simulatedPlatforms) {
            platform.TickFixedUpdate(_deltaTime);
        }

        // Run fixed-step logic for players
        foreach (var player in _simulatedPlayers) {
            player.TickFixedUpdate(_deltaTime);
        }
    }

    // Public method to register a platform to the simulator
    public void AddPlatform(IPhysicsObject platform) => _simulatedPlatforms.Add(platform);

    // Public method to register a player to the simulator
    public void AddPlayer(IPhysicsObject player) => _simulatedPlayers.Add(player);

    // Unregister a platform from the simulator
    public void RemovePlatform(IPhysicsObject platform) => _simulatedPlatforms.Remove(platform);

    // Unregister a player from the simulator
    public void RemovePlayer(IPhysicsObject player) => _simulatedPlayers.Remove(player);
}

// Interface that all simulated objects must implement
public interface IPhysicsObject {
    void TickFixedUpdate(float deltaTime);                    // Called every physics frame
    void TickUpdate(float deltaTime, float timeSinceStart);   // Called every visual frame
}

// Interface for moving platforms or similar objects with movement state
public interface IPhysicsMover {
    bool UsesBounding { get; }                  // Whether the platform needs bounding triggers
    bool RequireGrounding { get; }              // If true, only activates after being grounded on

    Vector2 FramePositionDelta { get; }         // How far the platform moved this frame
    Vector2 FramePosition { get; }              // Current world position of the platform
    Vector2 Velocity { get; }                   // Current velocity
    Vector2 TakeOffVelocity { get; }            // Velocity transferred to player when leaving platform
}

// Ensures the PhysicsSimulator is created before any scenes load
public static class SimulatorBootstrapper {
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Execute() {
        var simulator = new GameObject("PhysicsSimulator");
        simulator.AddComponent<PhysicsSimulator>();
        Object.DontDestroyOnLoad(simulator); // Make it persist between scenes
    }
}
