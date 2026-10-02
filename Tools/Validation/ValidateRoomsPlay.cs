using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public static class ValidateRoomsPlay
{
    private const string A = "Assets/Scenes/Rooms/ExampleRoom_A.unity";
    private const string B = "Assets/Scenes/Rooms/ExampleRoom_B.unity";
    private static readonly List<string> passed = new List<string>();
    private static PlayerLife Player => UnityEngine.Object.FindAnyObjectByType<PlayerLife>();
    private static RoomTransition Zone => UnityEngine.Object.FindAnyObjectByType<RoomTransition>();
    private static void Expect(string name, bool value)
    {
        if (!value) throw new Exception(name);
        passed.Add(name);
    }
    private static async Task Until(Func<bool> condition)
    {
        float deadline = Time.realtimeSinceStartup + 8;
        while (!condition())
        {
            if (Time.realtimeSinceStartup > deadline) throw new Exception("Timed out waiting for room operation");
            await Task.Delay(20);
        }
    }
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static object Get(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

    public static async Task<string> Main()
    {
        if (!Application.isPlaying || SceneManager.GetActiveScene().path != A) throw new Exception("Start Play mode in ExampleRoom_A first.");
        passed.Clear();
        bool previousBackground = Application.runInBackground;
        Application.runInBackground = true;
        Vector3 loadedPosition = default;
        void ObserveArrival(Scene scene, LoadSceneMode mode)
        {
            if (Player != null) loadedPosition = Player.transform.position;
        }
        SceneManager.sceneLoaded += ObserveArrival;
        try
        {
        await Task.Delay(100);
        foreach (var floor in UnityEngine.Object.FindObjectsByType<CompositeCollider2D>())
            Expect("Example floor has collision geometry on " + floor.name, floor.pathCount > 0);
        var player = Player;
        var controller = player.GetComponent<playerController2>();
        var inputs = player.GetComponent<PlayerInput>();
        var gate = player.GetComponent<PlayerInputLock>();
        var enabled = new List<InputAction>();
        foreach (var action in inputs.actions) if (action.enabled) enabled.Add(action);
        Expect("Player actions initially enabled", enabled.Count > 0);
        var first = gate.Acquire(); var second = gate.Acquire();
        bool allDisabled = true;
        foreach (var action in inputs.actions) allDisabled &= !action.enabled;
        Expect("Overlapping locks disable all input actions", gate.IsLocked && allDisabled);
        first.Dispose(); first.Dispose();
        Expect("Releasing one lease cannot unlock another owner", gate.IsLocked);
        second.Dispose();
        bool restored = true;
        foreach (var action in enabled) restored &= action.enabled;
        Expect("Last lease restores previously enabled inputs", !gate.IsLocked && restored);

        Set(controller, "isPreviewHeld", true);
        Set(controller, "isTargeting", true);
        Set(controller, "_isDashing", true);
        Set(controller, "_isWallJumping", true);
        Set(controller, "moveInput", Vector2.right);
        controller.RestoreBanishState(new playerController2.BanishState { active = true, playerBanished = true, remaining = 10 });
        controller.CancelActionsForTransition();
        Expect("Transition cancels movement, dash, wall jump, aiming and preview", !(bool)Get(controller, "isPreviewHeld") && !(bool)Get(controller, "isTargeting") && !controller.isDashing && !controller.isWallJumping && (Vector2)Get(controller, "moveInput") == Vector2.zero);
        Expect("Transition cancellation preserves active self-banish", controller.CaptureBanishState().active && controller.GetComponent<Banishable>().CurrentPlane == Banishable.Plane.B);
        var beforeId = player.GetEntityId();
        Expect("A to B load starts", RoomTravel.TryTransition(Zone, player));
        Expect("Loading immediately locks input and freezes source physics", gate.IsLocked && !player.GetComponent<Rigidbody2D>().simulated);
        player.Kill();
        Expect("Death is ignored during transition", player.isAlive);
        await Until(() => !RoomTravel.IsLoading && SceneManager.GetActiveScene().path == B);
        player = Player; controller = player.GetComponent<playerController2>();
        Expect("Destination has a new player at paired entrance", player.GetEntityId() != beforeId && Vector2.Distance(loadedPosition, Zone.ArrivalPosition) < 0.01f);
        Expect("Arrival restores player banish and timer", controller.CaptureBanishState().active && controller.CaptureBanishState().playerBanished && controller.CaptureBanishState().remaining > 0);
        Expect("Destination input unlocked", !player.GetComponent<PlayerInputLock>().IsLocked);
        beforeId = player.GetEntityId();
        // Invoke real death: cancellation, reload and entrance restoration.
        player.Kill();
        Expect("Death locks and cancels banish", player.GetComponent<PlayerInputLock>().IsLocked && !controller.CaptureBanishState().active);
        await Until(() => !RoomTravel.IsLoading && Player != null && Player.GetEntityId() != beforeId);
        player = Player;
        Expect("Death reloads only room B at last entrance", SceneManager.GetActiveScene().path == B && Vector2.Distance(loadedPosition, Zone.ArrivalPosition) < 0.01f);
        Expect("Respawn does not revive the cancelled banish", !player.GetComponent<playerController2>().CaptureBanishState().active);
        player.GetComponent<playerController2>().RestoreBanishState(new playerController2.BanishState { active = true, playerBanished = true, remaining = 10 });
        await Task.Delay(100); // Let the doorway observe that the arrival marker is clear.
        // Arrival suppression must remain in effect while any solid body overlap remains.
        Zone.BlockUntilClear(player);
        player.transform.position = Zone.transform.TransformPoint(Zone.areaSize / 2);
        player.GetComponent<Rigidbody2D>().position = player.transform.position;
        Physics2D.SyncTransforms();
        await Task.Delay(100);
        Expect("Arrival overlap cannot immediately bounce back", SceneManager.GetActiveScene().path == B && !RoomTravel.IsLoading);
        player.transform.position = Zone.ArrivalPosition;
        player.GetComponent<Rigidbody2D>().position = player.transform.position;
        Physics2D.SyncTransforms();
        await Task.Delay(100);
        // Exercise actual trigger callbacks through a physics overlap for the return direction.
        player.transform.position = Zone.transform.TransformPoint(Zone.areaSize / 2);
        player.GetComponent<Rigidbody2D>().position = player.transform.position;
        Physics2D.SyncTransforms();
        await Until(() => !RoomTravel.IsLoading && SceneManager.GetActiveScene().path == A);
        Expect("B to A works through real 2D trigger physics on the banished player plane", Vector2.Distance(loadedPosition, Zone.ArrivalPosition) < 0.01f);

        player = Player;
        controller = player.GetComponent<playerController2>();
        player.GetComponent<PlayerActionCancellation>().CancelAll();
        var camera = Camera.main;
        float z = camera.WorldToViewportPoint(player.transform.position).z;
        Vector3 edge = camera.ViewportToWorldPoint(new Vector3(0.5f, 1, z));
        var body = player.GetComponent<Rigidbody2D>();
        body.simulated = false;
        player.transform.position = edge + Vector3.up * 3;
        await Task.Delay(100);
        Expect("Three tiles beyond camera remain safe", player != null && player.isAlive);
        beforeId = player.GetEntityId();
        player.transform.position = edge + Vector3.up * 5;
        await Until(() => !RoomTravel.IsLoading && Player != null && Player.GetEntityId() != beforeId);
        Expect("Five tiles beyond camera trigger room respawn", SceneManager.GetActiveScene().path == A);
        return string.Join("\n", passed);
        }
        finally
        {
            SceneManager.sceneLoaded -= ObserveArrival;
            Application.runInBackground = previousBackground;
        }
    }
}
