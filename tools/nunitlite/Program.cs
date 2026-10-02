// Entry point for running the EditMode tests outside Unity via NUnitLite (see tools/check.sh).
// Unity never compiles this file; it lives outside Assets/.
using NUnitLite;

public static class Program
{
    public static int Main(string[] args) => new AutoRun(typeof(TwentyTons.Tests.TuningTableTests).Assembly).Execute(args);
}
