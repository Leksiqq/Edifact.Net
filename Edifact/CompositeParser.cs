using System.Text.RegularExpressions;
using System.Text;

namespace Net.Leksi.Edifact;

internal class CompositeParser(string hrChars) : Parser(hrChars)
{
    private static readonly Regex s_reName = new("^(?:\\s*(?<change>[+*|#X-]+))?\\s+(?<code>C\\d{3})\\s+(?<name>.+)$");
    private static readonly Regex s_reDescription = new("\\s+Desc\\s*\\:(?<description>.*)$");
    private static readonly Regex s_reNote = new("^(?:\\s*[+*|#X-]+)?\\s+Note\\s*\\:(?<note>.*)$");
    private static readonly Regex s_reItemNameBegin = new("^(?:\\s*(?<change>[+*|#X-]+))?\\s*\\d{3}\\s+(?<code>\\d{4})\\s+(?<name>.+?)(?:[CM]\\s+a?n?\\d*\\.?\\.?\\d*|$)");
    private static readonly Regex s_reItemNameEnd = new("^\\s*(?<name>.*?)(?<occurs>[CM])\\s+a?n?\\d*\\.?\\.?\\d*\\s*$");
    private enum State { None, Name, ItemName, Desc, Note }
    private enum Selector { None, Name, ItemNameBegin, ItemNameEnd, Desc, Note, Hr }
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
                selector = Selector.None;
                lastLine = line;
                ++_lineNumber;
                if(hrs > 0)
                {
                    if (
                        selector is Selector.None
                        && (m = s_reItemNameBegin.Match(line)).Success
                        && (selector = Selector.ItemNameBegin) == selector
                    )
                    {
                        if (type is null || state is not State.None || sb.Length > 0)
                        {
                            throw new Exception($"Unexpected line ({_lineNumber}): {line}");
                        }
                        type.Properties.Add(
                            new CompositeProperty
                            {
                                Code = m.Groups["code"].Value,
                            }
                        );
                        sb.Append(m.Groups["name"].Value.Trim());
                        state = State.ItemName;
                    }
                    if (
                        (selector is Selector.None || selector is Selector.ItemNameBegin)
                        && (m = s_reItemNameEnd.Match(line)).Success
                    )
                    {
                        if (
                            type is null || type.Properties.Count == 0
                            || type.Properties.Last().Occurs is { }
                            || state is not State.ItemName || sb.Length == 0
                        )
                        {
                            throw new Exception($"Unexpected line ({_lineNumber}): {line}");
                        }
                        if (selector is Selector.None)
                        {
                            sb.Append(m.Groups["name"].Value.Trim());
                        }
                        sb.Clear();
                        type.Properties.Last().Occurs = m.Groups["occurs"].Value;
                        state = State.None;
                        selector = Selector.ItemNameEnd;
                    }
                }
                if(selector is Selector.None)
                {
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
                                if (type is { } || state is not State.None || sb.Length > 0)
                                {
                                    throw new Exception($"Unexpected line ({_lineNumber}): {line}");
                                }
                                type = new Composite
                                {
                                    Code = m.Groups["code"].Value,
                                    Change = m.Groups["change"].Value,
                                };
                                state = State.Name;
                                sb.Append(m.Groups["name"].Value.Trim());
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
                    else if (hrs > 0)
                    {
                        if (type is null || (state is not State.Desc && state is not State.Note && state is not State.ItemName))
                        {
                            throw new Exception($"Unexpected line ({_lineNumber}): {line}");
                        }
                        if (sb.Length > 0)
                        {
                            sb.Append(' ');
                        }
                        sb.Append(line.Trim());
                    }
                }
                ++pos;
            }
            if (hrs > 0 && selector is not Selector.Hr)
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
        if (type is { })
        {
            yield return type;
        }
    }
}