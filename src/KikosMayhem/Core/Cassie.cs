namespace KikosMayhem.Core;

/// <summary>CASSIE announcements via LabAPI's Announcer.</summary>
public static class Cassie
{
    public static void Say(string message, string subtitles = "") =>
        LabApi.Features.Wrappers.Announcer.Message(message, subtitles);
}
