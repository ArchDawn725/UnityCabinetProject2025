using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class UIHelper : MonoBehaviour
{
    public static UIHelper singleton { get; private set; }

    private void Awake() { singleton = this; }

    // Overload: explicitly target a player's EventSystem
    public void JumpToElement(Selectable elementToSelect, MultiplayerEventSystem forPlayer)
    {
        if (!elementToSelect || !forPlayer) return;
        forPlayer.SetSelectedGameObject(elementToSelect.gameObject);
    }
}
