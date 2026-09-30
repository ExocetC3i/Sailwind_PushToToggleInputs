using BepInEx.Configuration;
using UnityEngine;

namespace PushToToggleInputs
{
    internal static class ShortcutInput
    {
        internal static bool IsDown(KeyboardShortcut shortcut)
        {
            if (shortcut.MainKey == KeyCode.None || !Input.GetKeyDown(shortcut.MainKey))
            {
                return false;
            }

            foreach (KeyCode modifier in shortcut.Modifiers)
            {
                if (modifier != KeyCode.None && !Input.GetKey(modifier))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
