using HarmonyLib;
using UnityEngine;

namespace PushToToggleInputs
{
    internal enum WinchDirection
    {
        None,
        PullIn,
        PayOut
    }

    internal static class WinchToggleState
    {
        private const float NativeInputMultiplier = 0.69f;
        private const float MaximumInput = 50f;
        private const float QuickReleaseInput = -100f;
        private const float MissingPointerMultiplier = 1f;

        private static GPButtonRopeWinch _activeWinch;
        private static WinchDirection _direction;
        private static int _lastToggleFrame = -1;
        private static int _lastConflictFrame = -1;
        private static bool _warnedMissingPointer;
        private static bool _warnedInvalidMultiplier;

        internal static void ApplyInput(
            GPButtonRopeWinch winch,
            ref float currentInput)
        {
            if (!GameState.playing)
            {
                Clear();
                return;
            }

            var isBeingOperated = IsBeingOperated(winch);
            if (!isBeingOperated)
            {
                ClearFor(winch);
                return;
            }

            var pullInPressed = ShortcutInput.IsDown(PushToToggleInputsPlugin.WinchPullInShortcut.Value);
            var payOutPressed = ShortcutInput.IsDown(PushToToggleInputsPlugin.WinchPayOutShortcut.Value);
            if (pullInPressed && payOutPressed)
            {
                if (_lastConflictFrame != Time.frameCount)
                {
                    PushToToggleInputsPlugin.PluginLog.LogWarning(
                        "Both winch shortcuts were pressed together; no winch direction was changed.");
                    _lastConflictFrame = Time.frameCount;
                }
            }
            else if ((pullInPressed || payOutPressed) && _lastToggleFrame != Time.frameCount)
            {
                _lastToggleFrame = Time.frameCount;
                if (_activeWinch != winch)
                {
                    _activeWinch = winch;
                    _direction = WinchDirection.None;
                }

                var requestedDirection = pullInPressed ? WinchDirection.PullIn : WinchDirection.PayOut;
                _direction = _direction == requestedDirection ? WinchDirection.None : requestedDirection;
                PushToToggleInputsPlugin.PluginLog.LogInfo(
                    _direction == WinchDirection.None
                        ? "Winch toggle disabled."
                        : $"Winch {_direction.ToString().ToLowerInvariant()} enabled.");
            }

            if (_activeWinch != winch || _direction == WinchDirection.None)
            {
                return;
            }

            if (KeyboardMovementInput.IsForwardOrBackwardHeld())
            {
                ClearFor(winch);
                return;
            }

            var clickedBy = Traverse.Create(winch).Field("isClickedBy").GetValue<GoPointerMovement>();
            var stickyClickedBy = Traverse.Create(winch).Field("stickyClickedBy").GetValue<GoPointer>();
            var keyboardMultiplier = GetKeyboardMultiplier(clickedBy, stickyClickedBy);
            if (float.IsNaN(keyboardMultiplier) || float.IsInfinity(keyboardMultiplier) || keyboardMultiplier <= 0f)
            {
                if (!_warnedInvalidMultiplier)
                {
                    PushToToggleInputsPlugin.PluginLog.LogWarning(
                        "Sailwind provided an invalid winch keyboard multiplier; using 1.0.");
                    _warnedInvalidMultiplier = true;
                }

                keyboardMultiplier = MissingPointerMultiplier;
            }

            var inputMagnitude = Mathf.Clamp(keyboardMultiplier * NativeInputMultiplier, 0f, MaximumInput);
            currentInput = _direction == WinchDirection.PullIn ? inputMagnitude : -inputMagnitude;
        }

        internal static void ClearOnQuickRelease(GPButtonRopeWinch winch, float currentInput)
        {
            if (_activeWinch == winch && currentInput <= QuickReleaseInput)
            {
                ClearFor(winch);
            }
        }

        internal static void ClearWhenOperationEnds(GPButtonRopeWinch winch)
        {
            if (_activeWinch != winch)
            {
                return;
            }

            var isClicked = Traverse.Create(winch).Field("isClicked").GetValue<bool>();
            var stickyClickedBy = Traverse.Create(winch).Field("stickyClickedBy").GetValue<GoPointer>();
            var handleIsGrabbed = winch.rotHandle != null && winch.rotHandle.IsGrabbed();
            if (!isClicked && stickyClickedBy == null && !handleIsGrabbed)
            {
                ClearFor(winch);
            }
        }

        internal static void ClearIfDestroyed()
        {
            if (_activeWinch == null)
            {
                Clear();
            }
        }

        internal static void Clear()
        {
            _activeWinch = null;
            _direction = WinchDirection.None;
            _lastToggleFrame = -1;
        }

        private static void ClearFor(GPButtonRopeWinch winch)
        {
            if (_activeWinch == winch)
            {
                _activeWinch = null;
                _direction = WinchDirection.None;
            }
        }

        private static float GetKeyboardMultiplier(GoPointerMovement clickedBy, GoPointer stickyClickedBy)
        {
            var movement = stickyClickedBy != null ? stickyClickedBy.movement : clickedBy;
            if (movement == null)
            {
                movement = UnityEngine.Object.FindObjectOfType<GoPointerMovement>();
            }

            if (movement == null)
            {
                if (!_warnedMissingPointer)
                {
                    PushToToggleInputsPlugin.PluginLog.LogWarning(
                        "No GoPointerMovement was available for a winch toggle; using a keyboard multiplier of 1.0.");
                    _warnedMissingPointer = true;
                }

                return MissingPointerMultiplier;
            }

            return movement.keyboardMult;
        }

        private static bool IsBeingOperated(GPButtonRopeWinch winch)
        {
            var isClicked = Traverse.Create(winch).Field("isClicked").GetValue<bool>();
            var isClickedBy = Traverse.Create(winch).Field("isClickedBy").GetValue<GoPointerMovement>();
            var stickyClickedBy = Traverse.Create(winch).Field("stickyClickedBy").GetValue<GoPointer>();
            var handleIsGrabbed = winch.rotHandle != null && winch.rotHandle.IsGrabbed();

            return stickyClickedBy != null || (isClicked && isClickedBy != null) || handleIsGrabbed;
        }
    }

    [HarmonyPatch(typeof(GPButtonRopeWinch), "LimitInput")]
    internal static class WinchLimitInputPatch
    {
        [HarmonyPostfix]
        private static void Postfix(
            GPButtonRopeWinch __instance,
            ref float ___currentInput)
        {
            WinchToggleState.ApplyInput(__instance, ref ___currentInput);
        }
    }

    [HarmonyPatch(typeof(GPButtonRopeWinch), "AddQuickReleaseInput")]
    internal static class WinchQuickReleasePatch
    {
        [HarmonyPostfix]
        private static void Postfix(GPButtonRopeWinch __instance, float ___currentInput)
        {
            WinchToggleState.ClearOnQuickRelease(__instance, ___currentInput);
        }
    }

    [HarmonyPatch(typeof(GPButtonRopeWinch), "Update")]
    internal static class WinchOperationEndPatch
    {
        [HarmonyPostfix]
        private static void Postfix(GPButtonRopeWinch __instance)
        {
            WinchToggleState.ClearWhenOperationEnds(__instance);
        }
    }
}
