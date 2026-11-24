using UnityEngine;
using UnityEngine.InputSystem;

// Armas del jugador 2 (solo teclado). Ver CoopKeyboardMovement.
public class CoopKeyboardWeapons : WeaponControlsBase
{
    public const string Help =
        "U o ENTER  —  disparar láser\n" +
        "O o SHIFT DERECHO  —  lanzar misil\n" +
        "8 / 9 / 0  —  cámara";

    static Keyboard Kb => Keyboard.current;

    public override bool PrimaryFired =>
        Kb != null && (Kb.uKey.isPressed || Kb.enterKey.isPressed || Kb.numpadEnterKey.isPressed);

    public override bool SecondaryFired =>
        Kb != null && (Kb.oKey.isPressed || Kb.rightShiftKey.isPressed);
}
