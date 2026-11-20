namespace TrainerDeck.Core.Launch;

public sealed record StartupOptions(bool ElevatedRelaunch, bool NoElevate)
{
    public const string ElevatedFlag = "--elevated";
    public const string NoElevateFlag = "--no-elevate";

    public static StartupOptions Parse(IEnumerable<string> args)
    {
        bool elevated = false, noElevate = false;
        foreach (string a in args)
        {
            if (a.Equals(ElevatedFlag, StringComparison.OrdinalIgnoreCase))
            {
                elevated = true;
            }
            else if (a.Equals(NoElevateFlag, StringComparison.OrdinalIgnoreCase))
            {
                noElevate = true;
            }
        }

        return new StartupOptions(elevated, noElevate);
    }

    public bool ShouldRelaunchElevated(bool runAsAdministrator, bool isElevated) =>
        runAsAdministrator && !isElevated && !ElevatedRelaunch && !NoElevate;
}
