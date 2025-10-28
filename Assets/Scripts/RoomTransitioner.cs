using UnityEngine;

public class RoomTransitioner : MonoBehaviour
{
    [SerializeField] private int NewRoomNumber = -1;
    [SerializeField] private RoomManager roomManager;
    public bool entered;

    private void Start()
    {
        //roomManager = FindAnyObjectByType<RoomManager>();
        roomManager = transform.parent.parent.parent.GetComponent<RoomManager>();
    }
    public void SetRoomNumber(int val)
    {
        NewRoomNumber = val;
        transform.GetChild(0).gameObject.SetActive(true);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player") && NewRoomNumber != -1)
        {
            roomManager.EnterNewRoom(NewRoomNumber);
        }
    }
}
