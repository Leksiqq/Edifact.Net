using System.Text.RegularExpressions;
using System.Text;

namespace Net.Leksi.Edifact;

internal class EnumerationParser : Parser
{
    private enum State { None, TypeName, Name, Desc, Note }
    private enum Selector { None, TypeName, Name, Desc, Note, Hr, DescRepr }
    private static readonly Regex s_reTypeName = new("^(?:\\s*(?<change>[+*|#X-]+))?\\s+(?<code>\\d{4})\\s+(?<name>[^\\[]+)\\[[BCI]?\\]$");
    private static readonly Regex s_reName = new("^(\\s*(?<change>[+*|#X-]+))?\\s+(?<code>[^\\s]+)\\s+(?<name>.+)$");
    private static readonly Regex s_reNote = new("^\\s+(?<rest>Note\\s*\\:(?<note>.*))$");
    private static readonly Regex s_reRest = new("^\\s*(?<rest>.+)$");
    private static readonly Regex s_reDescRepr = new("\\s+(?:Desc|Repr)\\s*\\:");
    internal async IAsyncEnumerable<Enumeration> ParseAsync(TextReader reader)
    {
        int nameOffset = -1;
        int descOffset = -1;
        StringBuilder sb = new();
        Enumeration? type = null;
        int hrs = 0;
        string lastLine = string.Empty;
        string? typeCode = null;
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
                            && (m = s_reTypeName.Match(line)).Success 
                            && (selector = Selector.TypeName) == selector
                        )
                        {
                            if (type is { } || state is not State.None || typeCode is { } || sb.Length > 0)
                            {
                                throw new Exception($"Unexpected line ({_lineNumber}): {line}");
                            }
                            typeCode = m.Groups["code"].Value;
                            state = State.TypeName;
                        }
                        if (
                            selector is Selector.None
                            && s_reDescRepr.Match(line).Success 
                            && (selector = Selector.DescRepr) == selector
                        )
                        {
                            if (type is { } || state is not State.TypeName || typeCode is null || sb.Length > 0)
                            {
                                throw new Exception($"Unexpected line ({_lineNumber}): {line}");
                            }
                        }
                        if(
                            selector is Selector.None
                            && (m = s_reName.Match(line)).Success
                            && (selector = Selector.Name) == selector
                        )
                        {
                            if(nameOffset == -1)
                            {
                                nameOffset = m.Groups["name"].Index;
                            }
                            if (nameOffset != m.Groups["name"].Index)
                            {
                                selector = Selector.None;
                            }
                            else
                            {
                                if(state is State.TypeName)
                                {
                                    state = State.None;
                                }
                                if(type is { })
                                {
                                    yield return type;
                                }
                                if (state is not State.None || typeCode is null || sb.Length > 0)
                                {
                                    throw new Exception($"Unexpected line ({_lineNumber}): {line}");
                                }
                                type = new Enumeration
                                {
                                    Change = m.Groups["change"].Value,
                                    TypeCode = typeCode,
                                    Code = m.Groups["code"].Value,
                                };
                                sb.Append(m.Groups["name"].Value.Trim());
                                state = State.Name;
                            }
                        }
                        if(
                            selector is Selector.None 
                            && (m = s_reNote.Match(line)).Success 
                            && (selector = Selector.Note) == selector
                        )
                        {
                            if(type is null || state is not State.None || nameOffset != m.Groups["rest"].Index || sb.Length > 0)
                            {
                                throw new Exception($"Unexpected line ({_lineNumber}): {line}");
                            }
                            sb.Append(m.Groups["note"].Value.Trim());
                            state = State.Note;
                        }
                    }
                    if (selector is Selector.None && s_reHr.Match(line).Success && (selector = Selector.Hr) == selector)
                    {
                        if (hrs > 0)
                        {
                            if (typeCode is null || type is null || state is not State.None || sb.Length > 0)
                            {
                                throw new Exception($"Unexpected line ({_lineNumber}): {line}");
                            }
                            yield return type;
                            type = null;
                            typeCode = null;
                        }
                        ++hrs;
                    }
                }
                else if(hrs > 0)
                {
                    if((m = s_reRest.Match(line)).Success)
                    {
                        if(selector is Selector.Name)
                        {
                            if(m.Groups["rest"].Index == nameOffset)
                            {
                                sb.Append(' ').Append(m.Groups["rest"].Value.Trim());
                            }
                            else if(m.Groups["rest"].Index > nameOffset)
                            {
                                if(descOffset == -1)
                                {
                                    descOffset = m.Groups["rest"].Index;
                                }
                                else if(m.Groups["rest"].Index < descOffset)
                                {
                                    throw new Exception($"Unexpected line ({_lineNumber}): {line}");
                                }
                                if (type is null || sb.Length == 0)
                                {
                                    throw new Exception($"Unexpected line ({_lineNumber}): {line}");
                                }
                                type.Name = sb.ToString();
                                sb.Clear();
                                selector = Selector.Desc;
                                state = State.Desc;
                            }
                        }
                    }
                }
                ++pos;
            }
            if (hrs > 0 && selector is not Selector.Hr && state is not State.TypeName)
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
                else if (state is State.Note)
                {
                    type.Note = sb.ToString();
                    sb.Clear();
                    state = State.None;
                }
            }
        }
        if (type is { })
        {
            yield return type;
        }
    }
}