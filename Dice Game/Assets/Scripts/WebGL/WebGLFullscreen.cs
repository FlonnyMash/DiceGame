using System.Runtime.InteropServices;
using UnityEngine;

namespace DiceGame.WebGL
{
    /// <summary>
    /// WebGL browser fullscreen via the page template JS API.
    /// On other platforms, uses Unity's Screen.fullScreen.
    /// Wire <see cref="EnterFullscreen"/> and <see cref="ToggleFullscreen"/> to UI Button onClick events.
    /// </summary>
    public class WebGLFullscreen : MonoBehaviour
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void WebGLFullscreen_Enter();

        [DllImport("__Internal")]
        private static extern void WebGLFullscreen_Toggle();
#endif

        public void EnterFullscreen()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLFullscreen_Enter();
#else
            Screen.fullScreen = true;
#endif
        }

        public void ToggleFullscreen()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLFullscreen_Toggle();
#else
            Screen.fullScreen = !Screen.fullScreen;
#endif
        }
    }
}
