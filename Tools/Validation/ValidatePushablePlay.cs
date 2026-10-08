using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

public static class ValidatePushablePlay
{
    public static string Main()
    {
        if (!Application.isPlaying) throw new Exception("Run in Play mode.");
        var passed = new List<string>();
        Action<string, bool> expect = (name, ok) => { if (!ok) throw new Exception(name); passed.Add(name); };
        var scene = SceneManager.CreateScene("Pushable validation", new CreateSceneParameters(LocalPhysicsMode.Physics2D));
        var keyboard = InputSystem.AddDevice<Keyboard>();
        var mouse = InputSystem.AddDevice<Mouse>();
        GameObject player = null;
        var previousUpdateMode = InputSystem.settings.updateMode;
        InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
        try
        {
            player = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Entities/Player.prefab"));
            SceneManager.MoveGameObjectToScene(player, scene);
            var controller = player.GetComponent<playerController2>();
            var playerBody = player.GetComponent<Rigidbody2D>();
            var playerCollider = player.GetComponent<CapsuleCollider2D>();
            player.GetComponent<PlayerInput>().SwitchCurrentControlScheme("Keyboard&Mouse", keyboard, mouse);
            player.GetComponent<PlayerInput>().ActivateInput();
            player.transform.position = Vector3.zero;
            Physics2D.SyncTransforms();
            player.transform.position += Vector3.up * (0.01f - playerCollider.bounds.min.y);
            var floor = new GameObject("Validation ground");
            SceneManager.MoveGameObjectToScene(floor, scene);
            floor.layer = LayerMask.NameToLayer("Ground");
            floor.transform.position = new Vector3(0, -0.5f, 0);
            floor.AddComponent<BoxCollider2D>().size = new Vector2(50, 1);
            var box = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Entities/Box.prefab"));
            SceneManager.MoveGameObjectToScene(box, scene);
            Physics2D.SyncTransforms();
            box.transform.position = new Vector3(playerCollider.bounds.max.x + 0.1f, 0.01f, 0);
            var body = box.GetComponent<Rigidbody2D>();
            var pushable = box.GetComponent<Pushable>();
            var physics = scene.GetPhysicsScene2D();
            var fixedUpdate = typeof(playerController2).GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
            Action<int> step = count => { for (int i = 0; i < count; i++) { Physics2D.SyncTransforms(); fixedUpdate.Invoke(controller, null); physics.Simulate(Time.fixedDeltaTime); } };
            Action<Key[]> keys = held => { InputSystem.QueueStateEvent(keyboard, new KeyboardState(held)); InputSystem.Update(); };
            step(5);
            float idleX = body.position.x;
            keys(new[] { Key.D }); step(20);
            expect("Walking into ungrabbed Box cannot move it", Mathf.Abs(body.position.x - idleX) < 0.01f);
            keys(new[] { Key.F });
            var action = player.GetComponent<PlayerInput>().actions.FindAction("Player/Interact");
            expect("Interact receives F through PlayerInput", action.WasPerformedThisFrame());
            expect("F grabs Box and unlocks X", controller.GrabbedBody == body && (body.constraints & RigidbodyConstraints2D.FreezePositionX) == 0);
            keys(new[] { Key.D }); step(20);
            expect("Grabbed Box moves right", body.position.x > idleX + 0.5f);
            float rightX = body.position.x;
            keys(new[] { Key.A }); step(20);
            expect("Grabbed Box moves left", body.position.x < rightX - 0.5f);
            keys(new[] { Key.Space });
            expect("Successful player jump gives Box upward velocity", playerBody.linearVelocity.y > 0 && Mathf.Abs(body.linearVelocity.y - playerBody.linearVelocity.y) < 0.01f);
            step(3);
            keys(new[] { Key.F });
            expect("Second F releases and restores horizontal lock", controller.GrabbedBody == null && (body.constraints & RigidbodyConstraints2D.FreezePositionX) != 0 && Mathf.Abs(body.linearVelocity.x) < 0.01f);
            // Re-grab on the same side after landing, then verify shared cancellation.
            keys(Array.Empty<Key>()); step(80);
            controller._isFacingRight = true;
            Physics2D.SyncTransforms();
            playerBody.position += Vector2.right * (box.GetComponent<BoxCollider2D>().bounds.min.x - playerCollider.bounds.max.x - 0.05f);
            Physics2D.SyncTransforms();
            keys(new[] { Key.F });
            expect("Can grab again after release", controller.GrabbedBody == body);
            using (player.GetComponent<PlayerInputLock>().Acquire())
                expect("Input lock releases grab and locks Box", controller.GrabbedBody == null && (body.constraints & RigidbodyConstraints2D.FreezePositionX) != 0);
            keys(Array.Empty<Key>()); keys(new[] { Key.F });
            expect("Can grab after input unlock", controller.GrabbedBody == body);
            controller.CancelActionsForTransition();
            expect("Room transition cancellation releases Box", controller.GrabbedBody == null && (body.constraints & RigidbodyConstraints2D.FreezePositionX) != 0);
            keys(Array.Empty<Key>()); keys(new[] { Key.F });
            expect("Re-grab before target disable", controller.GrabbedBody == body);
            pushable.enabled = false;
            expect("Disabling Pushable releases and locks Box", controller.GrabbedBody == null && (body.constraints & RigidbodyConstraints2D.FreezePositionX) != 0);
            pushable.enabled = true;
            keys(Array.Empty<Key>()); keys(new[] { Key.F });
            box.GetComponent<Banishable>().TogglePlane();
            step(1);
            expect("Separating planes releases and locks Box", controller.GrabbedBody == null && (body.constraints & RigidbodyConstraints2D.FreezePositionX) != 0);
            return string.Join("\n", passed);
        }
        finally
        {
            if (player != null) player.SetActive(false);
            InputSystem.RemoveDevice(keyboard);
            InputSystem.RemoveDevice(mouse);
            InputSystem.settings.updateMode = previousUpdateMode;
            SceneManager.UnloadSceneAsync(scene);
        }
    }
}
