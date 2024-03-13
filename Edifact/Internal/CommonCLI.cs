using static Net.Leksi.Edifact.Constants;

namespace Net.Leksi.Edifact;

internal static class CommonCLI
{
    internal static void UnknownArgumentError(string arg)
    {
        Console.WriteLine(string.Format(
                s_rmLabels.GetString(s_unknownKey)!,
                arg
            )
        );
    }
    internal static void MissedArgumentError(string arg)
    {
        Console.WriteLine(string.Format(
                s_rmLabels.GetString(s_missedKeyValue)!,
                arg
            )
        );
    }
    internal static void AlreadyUsed(string arg)
    {
        Console.WriteLine(string.Format(
                s_rmLabels.GetString(s_keyAlreadyUsed)!,
                arg
            )
        );
    }

    internal static void MissedMandatoryKeyError(string key)
    {
        Console.WriteLine(string.Format(
                s_rmLabels.GetString(s_missedMandatryKey)!,
                key
            )
        );
    }
}
