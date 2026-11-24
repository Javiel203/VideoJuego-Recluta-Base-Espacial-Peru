using UnityEngine;

public enum PilotGender
{
    None = 0,
    Male = 1,
    Female = 2
}

// Datos del piloto elegido en la pantalla CharacterSelect (se guardan con PlayerPrefs).
public static class PilotData
{
    const string NameKey = "Pilot_Name";
    const string GenderKey = "Pilot_Gender";

    public const int MaxNameLength = 12;

    // Colores del cohete y de la interfaz para cada piloto.
    public static readonly Color MaleColor = new Color(0.25f, 0.55f, 1f);
    public static readonly Color FemaleColor = new Color(1f, 0.3f, 0.75f);

    public static string Name => PlayerPrefs.GetString(NameKey, "");
    public static PilotGender Gender => (PilotGender)PlayerPrefs.GetInt(GenderKey, (int)PilotGender.None);

    public static bool HasPilot =>
        !string.IsNullOrWhiteSpace(Name) && Gender != PilotGender.None;

    public static void Save(string pilotName, PilotGender gender)
    {
        PlayerPrefs.SetString(NameKey, pilotName.Trim());
        PlayerPrefs.SetInt(GenderKey, (int)gender);
        PlayerPrefs.Save();
    }

    public static Color ColorFor(PilotGender gender)
    {
        switch (gender)
        {
            case PilotGender.Male: return MaleColor;
            case PilotGender.Female: return FemaleColor;
            default: return Color.white;
        }
    }
}
