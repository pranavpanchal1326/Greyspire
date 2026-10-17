using UnityEngine;

// Slow turn and bob, for things that should feel alive (the Colour Heart's core).
public class Spin : MonoBehaviour
{
    public float degreesPerSecond = 40f;
    public float bobHeight = 0.2f;
    public float bobSpeed = 1.5f;

    Vector3 origin;

    void Start()
    {
        origin = transform.localPosition;
    }

    void Update()
    {
        transform.Rotate(0, degreesPerSecond * GameClocks.WorldDelta, 0, Space.World);
        transform.localPosition = origin + Vector3.up * Mathf.Sin(Time.time * bobSpeed) * bobHeight;
    }
}
