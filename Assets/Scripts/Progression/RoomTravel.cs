using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Only scene paths, IDs and value snapshots survive a room load; room objects never do.</summary>
public static class RoomTravel
{
    public static bool IsLoading { get; private set; }
    private static string entranceScene;
    private static string entranceId;
    private static RoomConnection entranceConnection;
    private static playerController2.BanishState entranceBanish;
    private static bool restoreEntrance;
    private static IDisposable sourceLock;
    private static RoomFade fade;
    private static Rigidbody2D arrivalBody;
    private static bool arrivalWasSimulated;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        ClearSession();
    }

    public static void ClearSession()
    {
        if (fade != null) UnityEngine.Object.Destroy(fade.gameObject);
        fade = null;
        if (arrivalBody != null) arrivalBody.simulated = arrivalWasSimulated;
        arrivalBody = null;
        sourceLock?.Dispose();
        sourceLock = null;
        IsLoading = restoreEntrance = false;
        entranceScene = entranceId = null;
        entranceConnection = null;
        entranceBanish = default;
    }

    public static bool TryTransition(RoomTransition zone, PlayerLife player)
    {
        if (IsLoading || !player.isAlive || player.gameObject.scene != zone.gameObject.scene) return false;
        if (zone.connection == null || zone.arrival == null ||
            !zone.connection.TryGetDestination(zone.gameObject.scene.path, zone.zoneId, out var destination) ||
            !Application.CanStreamedLevelBeLoaded(destination.scenePath))
        {
            Debug.LogError("Room transition needs a valid paired connection, arrival marker and enabled destination build scene.", zone);
            return false;
        }

        var previousScene = entranceScene;
        var previousId = entranceId;
        var previousConnection = entranceConnection;
        var previousBanish = entranceBanish;
        entranceScene = destination.scenePath;
        entranceId = destination.zoneId;
        entranceConnection = zone.connection;
        entranceBanish = player.GetComponent<playerController2>().CaptureBanishState();
        if (BeginLoad(player, destination.scenePath, true)) return true;
        entranceScene = previousScene;
        entranceId = previousId;
        entranceConnection = previousConnection;
        entranceBanish = previousBanish;
        return false;
    }

    public static void Respawn(PlayerLife player)
    {
        if (IsLoading) return;
        // Death ends banish; only room travel carries the active ability across scenes.
        entranceBanish = default;
        BeginLoad(player, player.gameObject.scene.path, false);
    }

    private static bool BeginLoad(PlayerLife player, string path, bool transition)
    {
        if (!Application.CanStreamedLevelBeLoaded(path))
        {
            Debug.LogError("Room scene is not enabled in the build: " + path, player);
            return false;
        }
        IsLoading = true;
        restoreEntrance = path == entranceScene;
        sourceLock = player.GetComponent<PlayerInputLock>().Acquire();
        if (transition) player.GetComponent<playerController2>().CancelActionsForTransition();
        var body = player.GetComponent<Rigidbody2D>();
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0;
        body.simulated = false;
        if (transition)
        {
            fade = RoomFade.Create();
            fade.FadeOut(() => LoadScene(player, path));
            return true;
        }
        return LoadScene(player, path);
    }

    private static bool LoadScene(PlayerLife player, string path)
    {
        try
        {
            if (SceneManager.LoadSceneAsync(path, LoadSceneMode.Single) == null)
                throw new InvalidOperationException("Unity did not start the room load.");
            return true;
        }
        catch (Exception error)
        {
            if (player != null && player.isAlive) player.GetComponent<Rigidbody2D>().simulated = true;
            ClearSession();
            Debug.LogException(error, player);
            return false;
        }
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!IsLoading) return;
        try
        {
            CompleteArrival(scene);
        }
        finally
        {
            sourceLock?.Dispose();
            sourceLock = null;
            if (fade != null)
            {
                foreach (var player in UnityEngine.Object.FindObjectsByType<PlayerLife>())
                {
                    if (player.gameObject.scene != scene) continue;
                    sourceLock = player.GetComponent<PlayerInputLock>().Acquire();
                    arrivalBody = player.GetComponent<Rigidbody2D>();
                    arrivalWasSimulated = arrivalBody.simulated;
                    arrivalBody.simulated = false;
                    break;
                }
                fade.FadeIn(() => FinishArrival(scene));
            }
            else FinishArrival(scene);
        }
    }

    private static void FinishArrival(Scene scene)
    {
        if (arrivalBody != null) arrivalBody.simulated = arrivalWasSimulated;
        arrivalBody = null;
        sourceLock?.Dispose();
        sourceLock = null;
        if (fade != null) UnityEngine.Object.Destroy(fade.gameObject);
        fade = null;
        restoreEntrance = IsLoading = false;
        // Teleport and plane restoration are complete; lethal overlap must use the final state.
        foreach (var check in UnityEngine.Object.FindObjectsByType<PlayerPenetrationCheck>())
            if (check.gameObject.scene == scene) check.CheckNow();
    }

    private static void CompleteArrival(Scene scene)
    {
        PlayerLife player = null;
        foreach (var candidate in UnityEngine.Object.FindObjectsByType<PlayerLife>())
            if (candidate.gameObject.scene == scene) { player = candidate; break; }
        if (restoreEntrance && scene.path == entranceScene && player != null)
        {
            RoomTransition endpoint = null;
            int matches = 0;
            foreach (var zone in UnityEngine.Object.FindObjectsByType<RoomTransition>())
                if (zone.gameObject.scene == scene && zone.zoneId == entranceId && zone.connection == entranceConnection)
                { endpoint = zone; matches++; }
            if (matches == 1 && endpoint.arrival != null)
            {
                using (player.GetComponent<PlayerInputLock>().Acquire())
                {
                    player.transform.position = endpoint.ArrivalPosition;
                    var body = player.GetComponent<Rigidbody2D>();
                    body.position = endpoint.ArrivalPosition;
                    body.linearVelocity = Vector2.zero;
                    body.angularVelocity = 0;
                    player.GetComponent<playerController2>().RestoreBanishState(entranceBanish);
                    // Suppress every overlapping doorway, including intersecting transitions.
                    foreach (var zone in UnityEngine.Object.FindObjectsByType<RoomTransition>())
                        if (zone.gameObject.scene == scene) zone.BlockUntilClear(player);
                    Physics2D.SyncTransforms();
                }
            }
            else
            {
                Debug.LogError("Room entrance is missing, duplicated or has no arrival marker; using the authored player spawn.");
                entranceScene = entranceId = null;
                entranceConnection = null;
            }
        }
        if (player == null) Debug.LogError("Loaded room has no active PlayerLife.");
    }
}
