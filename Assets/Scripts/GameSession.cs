/// <summary>
/// Settings carried from the StartScreen lobby into GameScreen.
/// Reset by the lobby each time it opens.
/// </summary>
public static class GameSession
{
    /// <summary>True when P1 plays against the CPU because no second player joined.</summary>
    public static bool VsCpu;
}
