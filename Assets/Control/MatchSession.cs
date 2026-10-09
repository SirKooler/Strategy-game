/// <summary>
/// Hands a matched cloud snapshot from the home scene to the match scene.
/// </summary>
public static class MatchSession
{
    public static bool HasMatch => Pending != null;

    public static CloudMatchSnapshot Pending { get; private set; }

    public static void HandOff(CloudMatchSnapshot snapshot)
    {
        Pending = snapshot;
    }

    public static CloudMatchSnapshot Consume()
    {
        CloudMatchSnapshot snapshot = Pending;
        Pending = null;
        return snapshot;
    }
}
