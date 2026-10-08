using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

// Play-mode check of the Level01_BanishTutorial1 prompts (see SetupBanishTutorial1Prompts).
// Start Play mode in that room, then run. Set Run below to pick the route (each needs a fresh room load).
//   Route.Main:  spawn -> Aim prompt; tap Aim (no); hold Aim -> Banish prompt; banish self ->
//                Return prompt; zone C -> nothing for 30 s, then BANISH YOURSELF for ~5 s.
//   Route.ZoneB: hold Aim -> Banish prompt; zone B dismisses it and shows nothing else.
//   Route.Death: no death prompt on a fresh load; after dying, it shows first, then Aim again.
public static class ValidateBanishTutorial1Play
{
    enum Route { Main, ZoneB, Death }
    const Route Run = Route.Main;

    const string ScenePath = "Assets/Scenes/Level01/Level01_BanishTutorial1.unity";
    static readonly List<string> passed = new List<string>();

    static string Current => TutorialPrompts.CurrentOwner != null ? TutorialPrompts.CurrentOwner.name : "(none)";
    static string InputDiagnostics()
    {
        var input = PlayerInput.all.Count > 0 ? PlayerInput.all[0] : null;
        var aim = input != null ? input.actions.FindAction("Aim") : null;
        return $"rmb={Mouse.current?.rightButton.isPressed} aimEnabled={aim?.enabled} aimPressed={aim?.IsPressed()} mapEnabled={aim?.actionMap.enabled} devices={(input != null ? string.Join(",", input.devices) : "-")}";
    }

    static void Expect(string name, bool value)
    {
        if (!value) throw new Exception($"{name} (current prompt: {Current})");
        passed.Add(name);
    }

    static async Task Until(string what, Func<bool> condition, float seconds = 5f)
    {
        float deadline = Time.realtimeSinceStartup + seconds;
        while (!condition())
        {
            if (Time.realtimeSinceStartup > deadline) throw new Exception($"Timed out: {what} (current prompt: {Current}; {InputDiagnostics()})");
            await Task.Delay(20);
        }
        passed.Add(what);
    }

    // Moves in short steps with physics off, so a follow camera keeps up (PlayerLife kills the
    // player off camera). Targets sit just above the floor: overlapping terrain is lethal.
    static async Task Teleport(PlayerLife player, Vector2 position)
    {
        var body = player.GetComponent<Rigidbody2D>();
        body.simulated = false;
        Vector2 from = body.position;
        int steps = Mathf.CeilToInt(Vector2.Distance(from, position) / 1.5f);
        for (int i = 1; i <= steps; i++)
        {
            player.transform.position = Vector2.Lerp(from, position, (float)i / steps);
            await Task.Delay(120);
        }
        body.position = position;
        body.linearVelocity = Vector2.zero;
        body.simulated = true;
    }

    // Real editor mouse events overwrite simulated state, so callers re-send it while holding.
    static void SetMouse(bool right, bool left, Vector2 position)
    {
        var state = new MouseState { position = position }.WithButton(MouseButton.Right, right).WithButton(MouseButton.Left, left);
        InputSystem.QueueStateEvent(Mouse.current, state);
    }

    static void SetRightButton(bool down) => SetMouse(down, false, Mouse.current.position.ReadValue());

    // Aims at the player's own body and fires, which banishes the player to plane B.
    static async Task BanishSelf(PlayerLife player)
    {
        Vector2 Pointer() => Camera.main.WorldToScreenPoint(player.GetComponent<Collider2D>().bounds.center);
        float until = Time.realtimeSinceStartup + 0.6f; // past the arm time, even in aim slow motion
        while (Time.realtimeSinceStartup < until)
        {
            SetMouse(true, false, Pointer());
            await Task.Delay(20);
        }
        for (int i = 0; i < 5; i++)
        {
            SetMouse(true, true, Pointer());
            await Task.Delay(20);
        }
        SetMouse(false, false, Pointer());
    }

    public static async Task<string> Main()
    {
        if (!Application.isPlaying || SceneManager.GetActiveScene().path != ScenePath)
            throw new Exception("Start Play mode in Level01_BanishTutorial1 first.");
        passed.Clear();

        var settings = InputSystem.settings;
        var background = settings.backgroundBehavior;
        var editorInput = settings.editorInputBehaviorInPlayMode;
        bool runInBackground = Application.runInBackground;
        settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        Application.runInBackground = true;
        try
        {
            var player = UnityEngine.Object.FindAnyObjectByType<PlayerLife>();
            await Until("Aim prompt shows at spawn (zone A)", () => Current == "Prompt_Aim");
            await Task.Delay(500); // let the input focus settings above take effect

            if (Run == Route.Death)
            {
                Expect("No death prompt on a fresh room load", Current == "Prompt_Aim");
                player.Kill();
                await Until("After dying, the death prompt shows first", () => Current == "Prompt_Death", 6f);
                Expect("Room reloaded by respawn", RoomTravel.ArrivedByRespawn && player == null);
                await Until("The Aim prompt follows the death prompt", () => Current == "Prompt_Aim", 8f);
                return "PASSED (death route):" + Environment.NewLine + string.Join(Environment.NewLine, passed);
            }

            if (Run == Route.ZoneB)
            {
                await Until("Holding Aim leads to the Banish prompt", () => { SetRightButton(true); return Current == "Prompt_Banish"; }, 4f);
                SetRightButton(false);
                await Teleport(player, new Vector2(3.6f, -5.9f));
                await Until("Zone B dismisses the Banish prompt", () => Current == "(none)", 3f);
                await Task.Delay(2000);
                Expect("Zone B shows nothing else (Return waits for a banish)", Current == "(none)");
                return "PASSED (zone B route):\n" + string.Join("\n", passed);
            }

            SetRightButton(true);
            await Task.Delay(60);
            SetRightButton(false);
            await Task.Delay(600);
            Expect("A tap of Aim does not complete the Aim prompt", Current == "Prompt_Aim");

            await Until("Holding Aim leads to the Banish prompt", () => { SetRightButton(true); return Current == "Prompt_Banish"; }, 4f);
            SetRightButton(false);
            await Task.Delay(1500);
            Expect("Banish prompt stays until a banish or zone B", Current == "Prompt_Banish");

            await BanishSelf(player);
            Expect("The banish succeeded (player on plane B)", player.GetComponent<Banishable>().CurrentPlane == Banishable.Plane.B);
            await Until("A successful banish swaps the Banish prompt for the Return prompt", () => Current == "Prompt_Return");
            await Until("Return prompt leaves after its duration", () => Current == "(none)", 9f);

            await Teleport(player, new Vector2(8.6f, -1.9f));
            float enteredC = Time.realtimeSinceStartup;
            await Task.Delay(1000);
            Expect("Zone C entered, prompt not shown yet", Current == "(none)");
            await Until("BANISH YOURSELF shows ~30 s after zone C", () => Current == "Prompt_BanishYourself", 32f);
            float waited = Time.realtimeSinceStartup - enteredC;
            Expect($"Delay was about 30 s (measured {waited:0.0} s)", waited >= 29.5f && waited <= 31.5f);
            float shown = Time.realtimeSinceStartup;
            await Until("BANISH YOURSELF hides afterwards", () => Current == "(none)", 8f);
            float onScreen = Time.realtimeSinceStartup - shown;
            Expect($"BANISH YOURSELF up ~5 s plus fades (measured {onScreen:0.0} s)", onScreen >= 4.5f && onScreen <= 6.5f);

            return "PASSED (main route):\n" + string.Join("\n", passed);
        }
        finally
        {
            SetRightButton(false);
            settings.backgroundBehavior = background;
            settings.editorInputBehaviorInPlayMode = editorInput;
            Application.runInBackground = runInBackground;
        }
    }
}
