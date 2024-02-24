using System.Text;
using System.Text.RegularExpressions;
using static Net.Leksi.Edifact.PartsParser;

namespace Net.Leksi.Edifact;

internal class DataElementParser: Parser
{
    private enum State { None, Desc, Note }
    private enum Selector { None, Name, Desc, Note, Repr, Hr }
    private static readonly Regex s_reName = new("^(?:\\s*(?<change>[+*|#X-]+))?\\s+(?<code>\\d{4})\\s+(?<name>[^\\[]+)\\[[BCI]?\\]$");
    private static readonly Regex s_reDescription = new("\\s+Desc\\s*\\:(?<description>.*)$");
    private static readonly Regex s_reNote = new("^\\s+Note\\s*\\:(?<note>.*)$");
    private static readonly Regex s_reRepr = new("^\\s+Repr\\s*\\:(?<representation>.*)$");
    internal async IAsyncEnumerable<DataElement> ParseAsync(TextReader reader)
    {
        StringBuilder sb = new();
        DataElement? type = null;
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
                        if (
                            selector is Selector.None 
                            && (m = s_reName.Match(line)).Success 
                            && (selector = Selector.Name) == selector
                        )
                        {
                            if (type is { } || state is not State.None)
                            {
                                throw new Exception($"Unexpected line ({_lineNumber}): {line}");
                            }
                            type = new DataElement
                            {
                                Code = m.Groups["code"].Value,
                                Name = m.Groups["name"].Value.Trim(),
                                Change = m.Groups["change"].Value,
                            };
                        }
                        if (
                            selector is Selector.None
                            && (m = s_reDescription.Match(line)).Success && (selector = Selector.Desc) == selector
                            || (m = s_reNote.Match(line)).Success && (selector = Selector.Note) == selector
                        )
                        {
                            if (type is null || sb.Length > 0 || state is not State.None)
                            {
                                throw new Exception($"Unexpected line ({_lineNumber}): {line}");
                            }
                            state = selector switch { Selector.Desc => State.Desc, _ => State.Note };
                            string group = selector switch { Selector.Desc => "description", _ => "note" };
                            sb.Append(m.Groups[group].Value.Trim());
                        }
                        if (
                            selector is Selector.None
                            && (m = s_reRepr.Match(line)).Success 
                            && (selector = Selector.Repr) == selector
                        )
                        {
                            if (type is null || sb.Length > 0 || state is not State.None)
                            {
                                throw new Exception($"Unexpected line ({_lineNumber}): {line}");
                            }
                            type.Representation = m.Groups["representation"].Value;
                        }
                    }
                    if (selector is Selector.None && s_reHr.Match(line).Success && (selector = Selector.Hr) == selector)
                    {
                        if(hrs > 0)
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
                else if(hrs > 0)
                {
                    if (type is null || (state is not State.Desc && state is not State.Note))
                    {
                        throw new Exception($"Unexpected line ({_lineNumber}): {line}");
                    }
                    if(sb.Length > 0)
                    {
                        sb.Append(' ');
                    }
                    sb.Append(line.Trim());
                }
                ++pos;
            }
            if(hrs > 0 && selector is not Selector.Hr)
            {
                if (type is null)
                {
                    throw new Exception($"Unexpected line ({_lineNumber}): {lastLine}");
                }
                if (state is State.Desc)
                {
                    type.Description = sb.ToString();
                    sb.Clear();
                    state = State.None;
                }
                if (state is State.Note)
                {
                    type.Note = sb.ToString();
                    sb.Clear();
                    state = State.None;
                }
            }
        }
        if(type is { })
        {
            yield return type;
        }
    }
}