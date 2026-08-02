using UnityEngine;

namespace ZeepCast.Rendering
{
    /// <summary>
    /// Temporarily owns Zeepkist's stock spectator graphics while ZeepCast is
    /// producing the program view. The original active state is restored exactly.
    /// </summary>
    internal sealed class NativeSpectatorGraphics
    {
        private GameObject? _guiHolder;
        private bool _wasActive;

        public void Apply(Component? spectatorUi)
        {
            if (spectatorUi == null)
            {
                return;
            }

            var holder = spectatorUi.transform.Find("GUI Holder");
            if (holder == null)
            {
                return;
            }

            var holderObject = holder.gameObject;
            if (_guiHolder != holderObject)
            {
                Restore();
                _guiHolder = holderObject;
                _wasActive = holderObject.activeSelf;
            }

            if (holderObject.activeSelf)
            {
                holderObject.SetActive(false);
            }
        }

        public void Restore()
        {
            if (_guiHolder != null)
            {
                _guiHolder.SetActive(_wasActive);
            }

            _guiHolder = null;
            _wasActive = false;
        }
    }
}
