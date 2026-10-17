using UnityEngine;

// Turns something steadily about its own forward axis: a windmill's sails.
public class Turn : MonoBehaviour
{
    public float degreesPerSecond = 16f;

    void Update()
    {
        transform.Rotate(0f, 0f, degreesPerSecond * GameClocks.WorldDelta, Space.Self);
    }
}
