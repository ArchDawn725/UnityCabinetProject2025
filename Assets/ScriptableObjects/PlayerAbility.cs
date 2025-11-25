using UnityEngine;

[CreateAssetMenu(fileName = "NewPlayerAbility", menuName = "Player Ability", order = 2)]
public class PlayerAbility : ScriptableObject
{
    public string Name;

    public virtual void Fire()
    {

    }
}
