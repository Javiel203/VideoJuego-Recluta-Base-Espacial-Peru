using System;
using UnityEngine.InputSystem;

// Clic izquierdo / derecho, las órdenes de voz "fuego" / "misil" (ShipVoiceInput)
// o los botones táctiles FUEGO / MISIL (ShipTouchInput). En modo táctil se ignora el ratón.
[Serializable]
public class DesktopWeaponControls : WeaponControlsBase
{
    static bool MouseAllowed => !ShipTouchInput.Active && Mouse.current != null;

    public override bool PrimaryFired =>
        ShipVoiceInput.Primary || ShipTouchInput.Primary || (MouseAllowed && Mouse.current.leftButton.isPressed);
    public override bool SecondaryFired =>
        ShipVoiceInput.Secondary || ShipTouchInput.Secondary || (MouseAllowed && Mouse.current.rightButton.isPressed);
}
