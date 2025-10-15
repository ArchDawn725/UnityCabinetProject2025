using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class UIJoinWireup : MonoBehaviour
{
    [SerializeField] Transform p1UiRoot;              // top-level panel/canvas for P1 UI
    [SerializeField] Transform p2UiRoot;              // top-level panel/canvas for P2 UI
    [SerializeField] Selectable p1First;              // first selectable for P1
    [SerializeField] Selectable p2First;              // first selectable for P2

    MultiplayerEventSystem _p1ES, _p2ES;

    void OnEnable()
    {
        if (PlayerInputManager.instance)
            PlayerInputManager.instance.onPlayerJoined += OnPlayerJoined;
    }
    void OnDisable()
    {
        if (PlayerInputManager.instance)
            PlayerInputManager.instance.onPlayerJoined -= OnPlayerJoined;
    }

    void OnPlayerJoined(PlayerInput pi)
    {
        var idx = pi.playerIndex;
        var (es, root, first) = idx == 0
            ? (_p1ES, p1UiRoot, p1First)
            : (_p2ES, p2UiRoot, p2First);

        // Create per-player ES+Module if missing
        if (es == null)
        {
            var go = new GameObject(idx == 0 ? "UI_ES_P1" : "UI_ES_P2");
            es = go.AddComponent<MultiplayerEventSystem>();
            var ui = go.AddComponent<InputSystemUIInputModule>();

            // 1) Scope this ES to that player's UI only
            es.playerRoot = root ? root.gameObject : null;

            // 2) Pair the UI module to THIS player's devices (CRITICAL)
            pi.uiInputModule = ui;

            // 3) (Optional) Bind explicit actions from the player's action map
            // var actions = pi.actions;
            // ui.move   = InputActionReference.Create(actions["UI/Navigate"]);
            // ui.submit = InputActionReference.Create(actions["UI/Submit"]);
            // ui.cancel = InputActionReference.Create(actions["UI/Cancel"]);

            if (idx == 0) _p1ES = es; else _p2ES = es;
        }
        else
        {
            // If you pre-placed an ES+Module in the scene, still pair it & scope it
            var ui = es.GetComponent<InputSystemUIInputModule>();
            pi.uiInputModule = ui;
            es.playerRoot = root ? root.gameObject : null;
        }

        // Give that player immediate focus on their UI
        if (first) es.SetSelectedGameObject(first.gameObject);

        // Prevent device swapping later
        pi.neverAutoSwitchControlSchemes = true;
    }
}
