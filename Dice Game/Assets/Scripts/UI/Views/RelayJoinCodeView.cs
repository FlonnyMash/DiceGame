using UnityEngine;
using TMPro;

namespace DiceGame.UI.Views
{
    // Tiny overlay that surfaces the host's Relay join code while waiting for a remote peer
    // to connect through Unity Sessions. GameController shows the code via Show(code) and
    // hides the overlay once UgsNetworkTransport reports NetworkStatus.Connected.
    public class RelayJoinCodeView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _codeLabel;
        [SerializeField] private string _format = "Code: {0}";

        private bool _resolvedLabel;

        private void Awake()
        {
            ResolveLabel();
            gameObject.SetActive(false);
        }

        public void Show(string code)
        {
            // Guard against the inspector being unwired -- without this the overlay would silently
            // keep showing whatever placeholder text the TMP authoring left behind, while the SDK
            // log printed the real join code. Auto-find a TMP_Text in our hierarchy as a fallback
            // so a missing reference can never silently leak a stale code to the player.
            ResolveLabel();
            if (_codeLabel != null)
            {
                _codeLabel.text = string.IsNullOrEmpty(code) ? string.Empty : string.Format(_format, code);
            }
            else
            {
                Debug.LogWarning($"[RelayJoinCodeView] No TextMeshProUGUI assigned to _codeLabel on '{name}'. Cannot display join code '{code}'. Wire the label in the Inspector.", this);
            }
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void ResolveLabel()
        {
            if (_resolvedLabel || _codeLabel != null) { _resolvedLabel = true; return; }

            // Try this GameObject first, then any descendant. We look for the inactive children too
            // because the overlay is disabled by default (Awake hides it).
            _codeLabel = GetComponent<TextMeshProUGUI>();
            if (_codeLabel == null) _codeLabel = GetComponentInChildren<TextMeshProUGUI>(includeInactive: true);

            if (_codeLabel != null)
            {
                Debug.Log($"[RelayJoinCodeView] Auto-resolved _codeLabel to '{_codeLabel.name}'. Wire it explicitly in the Inspector to silence this fallback.", this);
            }
            _resolvedLabel = true;
        }
    }
}
