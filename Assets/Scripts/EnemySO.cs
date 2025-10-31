using UnityEngine;

[CreateAssetMenu(fileName = "EnemySO", menuName = "Scriptable Objects/EnemySO")]
public class EnemySO : ScriptableObject
{
    public Color color;
    public float maxHealth = 25f;
    public float moveSpeed = 3f;
}
