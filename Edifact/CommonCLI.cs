using System.Reflection;
using System.Resources;

namespace Net.Leksi.Edifact;

internal static class CommonCLI
{
    private const string s_rmLabelsName = "Net.Leksi.Edifact.Properties.labels";
    private const string s_keyAlreadyUsed = "KEY_ALREADY_USED";
    private const string s_missedKeyValue = "MISSED_KEY_VALUE";
    internal static ResourceManager LabelsResourceManager { get; private set; }
    static CommonCLI()
    {
        LabelsResourceManager = new ResourceManager(
            s_rmLabelsName,
            Assembly.GetExecutingAssembly()
        );
    }
    internal static void UnknownArgumentError(string arg)
    {
        Console.WriteLine(string.Format(
                LabelsResourceManager.GetString("UNKNOWN_KEY")!,
                arg
            )
        );
    }
    internal static void MissedArgumentError(string arg)
    {
        Console.WriteLine(string.Format(
                LabelsResourceManager.GetString(s_missedKeyValue)!,
                arg
            )
        );
    }
    internal static void AlreadyUsed(string arg)
    {
        Console.WriteLine(string.Format(
                LabelsResourceManager.GetString(s_keyAlreadyUsed)!,
                arg
            )
        );
    }

}
