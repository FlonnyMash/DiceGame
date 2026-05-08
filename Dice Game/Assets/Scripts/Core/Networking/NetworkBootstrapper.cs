using UnityEngine;

// Attach this to the same GameObject as NGO's NetworkManager. The first instance loaded wins:
// it survives scene loads via DontDestroyOnLoad, and any later scenes that ship their own
// NetworkManager prefab destroy themselves on Awake. This prevents the "duplicate
// NetworkManager.Singleton" collisions that crash on the second host attempt after returning
// to the main menu.
//
// Execution order is forced to a very low number so this runs BEFORE NetworkManager.Awake on
// any duplicate GameObject, killing the duplicate before NGO sees it.
[DefaultExecutionOrder(-10000)]
public class NetworkBootstrapper : MonoBehaviour
{
    private static NetworkBootstrapper s_Instance;

    private void Awake()
    {
        if (s_Instance != null && s_Instance != this)
        {
            // Persistent NetworkManager already lives across scenes; kill this scene-local
            // duplicate so its NetworkManager component never tries to claim Singleton.
            Destroy(gameObject);
            return;
        }

        s_Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (s_Instance == this) s_Instance = null;
    }
}
