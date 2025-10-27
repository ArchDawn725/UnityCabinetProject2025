using UnityEngine;

[CreateAssetMenu(fileName = "RoomSO", menuName = "Scriptable Objects/RoomSO")]
public class RoomSO : ScriptableObject
{
    public RoomType type;
    public GameObject roomPrefab;

    [Tooltip("Enemies to spawn when this room begins.")]
    public EnemyEntry[] enemies;

    public int TotalEnemyCount
    {
        get
        {
            if (enemies == null) return 0;
            int total = 0;
            for (int i = 0; i < enemies.Length; i++)
                total += enemies[i].count < 0 ? 0 : enemies[i].count;
            return total;
        }
    }
}

public enum RoomType
{
    Standard,
    Shop,
    Elite,
    Boss,
    Starter,
}

[System.Serializable]
public struct EnemyEntry
{
    public EnemySO enemy;       
    [Min(0)] public int count;  
}

