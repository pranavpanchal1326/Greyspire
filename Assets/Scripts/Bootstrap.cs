using UnityEngine;

// The whole game is built from code, so any scene (even an empty one) works:
// pressing Play creates the Game object, which builds everything else.
public static class Bootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        if (Object.FindAnyObjectByType<Game>() != null) return;
        new GameObject("Game").AddComponent<Game>();
    }
}
