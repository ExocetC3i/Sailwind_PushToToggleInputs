using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace PushToToggleInputs
{
    [HarmonyPatch(typeof(OVRPlayerController), "UpdateMovement")]
    internal static class MovementInputPatch
    {
        private const int ExpectedGameInputCalls = 5;

        private static readonly MethodInfo GameInputGetKey =
            AccessTools.Method(typeof(GameInput), nameof(GameInput.GetKey), new[] { typeof(InputName) });
        private static readonly MethodInfo MovementGetKey =
            AccessTools.Method(typeof(MovementInputPatch), nameof(GetMovementKey));

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            if (GameInputGetKey == null || MovementGetKey == null)
            {
                throw new MissingMethodException("Could not resolve Sailwind's movement input methods.");
            }

            var replacedCalls = 0;
            foreach (var instruction in instructions)
            {
                if ((instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) &&
                    Equals(instruction.operand, GameInputGetKey))
                {
                    instruction.operand = MovementGetKey;
                    replacedCalls++;
                }

                yield return instruction;
            }

            if (replacedCalls != ExpectedGameInputCalls)
            {
                throw new InvalidOperationException(
                    $"Expected {ExpectedGameInputCalls} GameInput.GetKey calls in OVRPlayerController.UpdateMovement, found {replacedCalls}.");
            }
        }

        internal static bool GetMovementKey(InputName inputName)
        {
            var nativeInput = GameInput.GetKey(inputName);
            if (!PushToToggleInputsPlugin.AutoForwardEnabled || inputName != InputName.MoveUp)
            {
                return nativeInput;
            }

            if (KeyboardMovementInput.IsForwardOrBackwardHeld())
            {
                PushToToggleInputsPlugin.AutoForwardEnabled = false;
                PushToToggleInputsPlugin.PluginLog.LogInfo("Auto-forward cancelled by forward/backward input.");
                return nativeInput;
            }

            return true;
        }
    }

    internal static class KeyboardMovementInput
    {
        internal static bool IsForwardOrBackwardHeld()
        {
            return IsBindingHeld(InputName.MoveUp, false) ||
                   IsBindingHeld(InputName.MoveUp, true) ||
                   IsBindingHeld(InputName.MoveDown, false) ||
                   IsBindingHeld(InputName.MoveDown, true);
        }

        private static bool IsBindingHeld(InputName inputName, bool secondary)
        {
            var key = GameInput.GetKeyCode(inputName, secondary, false);
            return key != KeyCode.None && Input.GetKey(key);
        }
    }
}
