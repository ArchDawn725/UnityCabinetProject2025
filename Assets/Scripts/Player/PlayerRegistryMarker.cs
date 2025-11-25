using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class PlayerRegistryMarker : MonoBehaviour
{
    PlayerInput _pi;

    void Awake() => _pi = GetComponent<PlayerInput>();
    void OnEnable() => PlayerRegistry.Add(_pi);
    void OnDisable() => PlayerRegistry.Remove(_pi);
}
