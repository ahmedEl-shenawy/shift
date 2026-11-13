using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Keyboard + gamepad input wrapper (Input System package).</summary>
public static class GameInput
{
    // ---- automated playtest hooks (unused in normal play) ----
    public static bool SimActive;
    public static Vector3 SimWorldMove;
    public static bool SimJumpHeld;
    static bool simJump, simShift;
    public static void SimPressJump() => simJump = true;
    public static void SimPressShift() => simShift = true;
    static bool Take(ref bool b) { if (!b) return false; b = false; return true; }

    static Keyboard K => Keyboard.current;
    static Gamepad G => Gamepad.current;

    public static Vector2 Move
    {
        get
        {
            Vector2 v = Vector2.zero;
            var k = K;
            if (k != null)
            {
                if (k.aKey.isPressed || k.leftArrowKey.isPressed) v.x -= 1;
                if (k.dKey.isPressed || k.rightArrowKey.isPressed) v.x += 1;
                if (k.sKey.isPressed || k.downArrowKey.isPressed) v.y -= 1;
                if (k.wKey.isPressed || k.upArrowKey.isPressed) v.y += 1;
            }
            var g = G;
            if (g != null)
            {
                Vector2 s = g.leftStick.ReadValue();
                if (s.sqrMagnitude > 0.04f) v += s;
                v += g.dpad.ReadValue();
            }
            return Vector2.ClampMagnitude(v, 1f);
        }
    }

    public static bool JumpPressed => Take(ref simJump) ||
        (K != null && K.spaceKey.wasPressedThisFrame) ||
        (G != null && G.buttonSouth.wasPressedThisFrame);

    public static bool JumpHeld => SimJumpHeld ||
        (K != null && K.spaceKey.isPressed) ||
        (G != null && G.buttonSouth.isPressed);

    public static bool ShiftPressed => Take(ref simShift) ||
        (K != null && (K.leftShiftKey.wasPressedThisFrame || K.rightShiftKey.wasPressedThisFrame ||
                       K.eKey.wasPressedThisFrame || K.jKey.wasPressedThisFrame)) ||
        (G != null && (G.buttonWest.wasPressedThisFrame || G.rightShoulder.wasPressedThisFrame ||
                       G.leftShoulder.wasPressedThisFrame || G.rightTrigger.wasPressedThisFrame));

    public static bool ConfirmPressed =>
        (K != null && (K.enterKey.wasPressedThisFrame || K.numpadEnterKey.wasPressedThisFrame || K.spaceKey.wasPressedThisFrame)) ||
        (G != null && (G.buttonSouth.wasPressedThisFrame || G.startButton.wasPressedThisFrame));

    public static bool PausePressed =>
        (K != null && (K.escapeKey.wasPressedThisFrame || K.pKey.wasPressedThisFrame)) ||
        (G != null && G.startButton.wasPressedThisFrame);

    public static bool QuitToMenuPressed =>
        (K != null && (K.qKey.wasPressedThisFrame || K.backspaceKey.wasPressedThisFrame)) ||
        (G != null && G.buttonEast.wasPressedThisFrame);

    public static bool RestartPressed =>
        (K != null && K.rKey.wasPressedThisFrame) ||
        (G != null && G.selectButton.wasPressedThisFrame);

    public static bool LeftPressed =>
        (K != null && (K.aKey.wasPressedThisFrame || K.leftArrowKey.wasPressedThisFrame)) ||
        (G != null && G.dpad.left.wasPressedThisFrame);

    public static bool RightPressed =>
        (K != null && (K.dKey.wasPressedThisFrame || K.rightArrowKey.wasPressedThisFrame)) ||
        (G != null && G.dpad.right.wasPressedThisFrame);
}
