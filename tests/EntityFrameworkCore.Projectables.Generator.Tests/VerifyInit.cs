using System.Globalization;
using System.Runtime.CompilerServices;

namespace EntityFrameworkCore.Projectables.Generator.Tests;

public static class VerifyInit
{
    [ModuleInitializer]
    public static void Initialize()
    {
        // Auto-accept new/changed snapshots only when explicitly requested.
        // Set VERIFY_AUTO_APPROVE=true when adding new tests to generate initial .verified.txt files.
        // Do NOT set this for normal runs — snapshot mismatches must be visible as test failures.
        if (Environment.GetEnvironmentVariable("VERIFY_AUTO_APPROVE") == "true")
        {
            VerifierSettings.AutoVerify();
        }

        // Force English culture so that snapshot output is consistent
        // regardless of the developer's OS locale.
        CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US");
        CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("en-US");
    }
}