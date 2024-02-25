using System.Text;
using System.Text.RegularExpressions;

namespace Net.Leksi.Edifact;

internal class DataElementParser(string hrChars): Parser(hrChars)
{
    private enum State { None, Name, Desc, Note }
    private enum Selector { None, NameBegin, NameEnd, Desc, Note, Repr, Hr }
    private static readonly Regex s_reNameBegin = new("^(?:\\s*(?<change>[+*|#X-]+))?\\s+(?<code>\\d{4})\\s+(?<name>[^[]+)");
    private static readonly Regex s_reNameEnd = new("^\\s*(?<name>[^[]+)\\[[BCI]?\\]$");
    private static readonly Regex s_reDescription = new("\\s+Desc\\s*\\:(?<description>.*)$");
    private static readonly Regex s_reNote = new("^(?:\\s*[+*|#X-]+)?\\s+Note\\s*\\:(?<note>.*)$");
    private static readonly Regex s_reRepr = new("^(?:\\s*[+*|#X-]+)?\\s+Repr\\s*\\:(?<representation>.*)$");
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
                selector = Selector.None;
                lastLine = line;
                ++_lineNumber;
                if (pos == 0)
                {
                    if (hrs > 0)
                    {
                        if (
                            selector is Selector.None 
                            && (m = s_reNameBegin.Match(line)).Success 
                            && (selector = Selector.NameBegin) == selector
                        )
                        {
                            if (type is { } || state is not State.None || sb.Length > 0)
                            {
                                throw new Exception($"Unexpected line ({_lineNumber}): {line}");
                            }
                            type = new DataElement
                            {
                                Code = m.Groups["code"].Value,
                                Change = m.Groups["change"].Value,
                            };
                            sb.Append(m.Groups["name"].Value.Trim());
                            state = State.Name;
                        }
                        if(
                            (selector is Selector.None || selector is Selector.NameBegin)
                            && (m = s_reNameEnd.Match(line)).Success
                            && (selector = Selector.NameEnd) == selector
                        )
                        {
                            if (type is null || state is not State.Name || sb.Length == 0)
                            {
                                throw new Exception($"Unexpected line ({_lineNumber}): {line}");
                            }
                            if(selector is Selector.None)
                            {
                                sb.Append(' ').Append(m.Groups["name"].Value.Trim());
                            }
                            type.Name = sb.ToString();
                            sb.Clear();
                            state = State.None;
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
                    if (type is null || (state is not State.Desc && state is not State.Note && state is not State.Name))
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
                else if (state is State.Note)
                {
                    type.Note = sb.ToString();
                    sb.Clear();
                    state = State.None;
                }
                else if (state is State.Name)
                {
                    type.Name = sb.ToString();
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