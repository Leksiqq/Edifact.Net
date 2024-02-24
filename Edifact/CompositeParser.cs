using System.Text.RegularExpressions;
using System.Text;

namespace Net.Leksi.Edifact;

internal class CompositeParser : Parser
{
    private enum State { None }
    private enum Selector { None, Hr }
    internal async IAsyncEnumerable<Composite> ParseAsync(TextReader reader)
    {
        StringBuilder sb = new();
        Composite? type = null;
        int hrs = 0;
        string lastLine = string.Empty;
        _lineNumber = 0;
        State state = State.None;
        await foreach (IEnumerable<string> lines in SplitByNewLineAsync(reader))
        {
            int pos = 0;
            Match m;
            Selector selector = Selector.None;
            foreach (string line in lines)
            {
                lastLine = line;
                ++_lineNumber;
                if (pos == 0)
                {
                    if (hrs > 0)
                    {
                    }
                    if (selector is Selector.None && s_reHr.Match(line).Success && (selector = Selector.Hr) == selector)
                    {
                        if (hrs > 0)
                        {
                            if (type is null || state is not State.None || sb.Length > 0)
                            {
                                throw new Exception($"Unexpected line ({_lineNumber}): {line}");
                            }
                            yield return type;
                            type = null;
                        }
                        ++hrs;
                    }
                }
                else
                {

                }
                ++pos;
            }
            if (hrs > 0 && selector is not Selector.Hr)
            {
                if (type is null)
                {
                    throw new Exception($"Unexpected line ({_lineNumber}): {lastLine}");
                }
            }
        }
        if (type is { })
        {
            yield return type;
        }
    }
}