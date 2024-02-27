using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using static Net.Leksi.Edifact.Constants;

namespace Net.Leksi.Edifact;

internal class Parser(string hrChars)
{
    protected readonly Regex s_reHr = new($"^[{hrChars}]{{5,}}");
    protected int _lineNumber;
    protected async IAsyncEnumerable<IEnumerable<string>> SplitByNewLineAsync(
        TextReader reader, [EnumeratorCancellation]CancellationToken stoppingToken
    )
    {
        List<string> lines = [];
        while (await reader.ReadLineAsync(stoppingToken) is string line)
        {
            stoppingToken.ThrowIfCancellationRequested();
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
        if(lines.Count > 0)
        {
            yield return lines.ToArray();
        }
    }
    protected void ThrowUnexpectedLine(int lineNumber, string line)
    {
        throw new Exception(string.Format(s_rmLabels.GetString(s_unexpectedLine)!, _lineNumber, line));
    }
}
