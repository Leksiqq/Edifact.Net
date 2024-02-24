using System.Text.RegularExpressions;

namespace Net.Leksi.Edifact;

internal class Parser
{
    protected static readonly Regex s_reHr = new("^[-]{5,}");
    protected int _lineNumber;
    protected async IAsyncEnumerable<IEnumerable<string>> SplitByNewLineAsync(TextReader reader)
    {
        List<string> lines = [];
        while (await reader.ReadLineAsync() is string line)
        {
            if (string.IsNullOrWhiteSpace(line.Trim()))
            {
                ++_lineNumber;
                yield return lines.ToArray();
                lines.Clear();
            }
            else
            {
                lines.Add(line);
            }
        }
    }

}
