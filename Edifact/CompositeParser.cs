using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using static Net.Leksi.Edifact.Constants;

namespace Net.Leksi.Edifact;

internal class CompositeParser(string hrChars, char nameFirstChar) : Parser(hrChars)
{
    private enum State { None, Name, ItemName, Desc, Note }
    private enum Selector { None, Name, ItemNameBegin, ItemNameEnd, Desc, Note, Hr }
    private static readonly Regex s_reDescription = new("\\s+Desc\\s*\\:(?<description>.*)$");
    private static readonly Regex s_reNote = new("^(?:\\s*[+*|#X-]+)?\\s+Note\\s*\\:(?<note>.*)$");
    private static readonly Regex s_reItemNameBegin = new("^\\s*(?<position>\\d{3})(?:\\s+(?<change>[+*|#X-]+))?\\s*(?<code>\\d{4})\\s+(?<name>.+?)(?:[CM]\\s+a?n?\\d*\\.?\\.?\\d*|$)");
    private static readonly Regex s_reItemNameEnd = new("^\\s*(?<name>.*?)(?<minOccurs>[CM])\\s+a?n?\\d*\\.?\\.?\\d*\\s*$");
    private readonly Regex _reName = new($"^(?:\\s*(?<change>[+*|#X-]+))?\\s*(?<code>{nameFirstChar}\\d{{3}})\\s+(?<name>.+)$");
    internal async IAsyncEnumerable<Composite> ParseAsync(
        TextReader reader, [EnumeratorCancellation] CancellationToken stoppingToken
    )
    {
        StringBuilder sb = new();
        Composite? type = null;
        int hrs = 0;
        string lastLine = string.Empty;
        _lineNumber = 0;
        State state = State.None;
        await foreach (IEnumerable<string> lines in SplitByNewLineAsync(reader, stoppingToken))
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
                        if (type is null)
                        {
                            ThrowUnexpectedLine(_lineNumber, line);
                        }
                        if (state is State.ItemName)
                        {
                            type!.Elements.Last().Name = sb.ToString();
                            sb.Clear();
                            state = State.None;
                        }
                        if (state is not State.None || sb.Length > 0)
                        {
                            ThrowUnexpectedLine(_lineNumber, line);
                        }
                        type!.Elements.Add(
                            new Element
                            {
                                Code = m.Groups[s_code].Value,
                                Position = m.Groups[s_position].Value,
                            }
                        );
                        sb.Append(m.Groups[s_name].Value.Trim());
                        state = State.ItemName;
                    }
                    if (
                        (selector is Selector.None || selector is Selector.ItemNameBegin)
                        && (m = s_reItemNameEnd.Match(line)).Success
                    )
                    {
                        if (
                            type is null 
                            || type.Elements.Count == 0
                            || type.Elements.Last().MinOccurs is { }
                            || state is not State.ItemName || sb.Length == 0
                        )
                        {
                            ThrowUnexpectedLine(_lineNumber, line);
                        }
                        if (selector is Selector.None)
                        {
                            sb.Append(m.Groups[s_name].Value.Trim());
                        }
                        type!.Elements.Last().MinOccurs = m.Groups[s_minOccurs].Value;
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
                                && (m = _reName.Match(line)).Success
                                && (selector = Selector.Name) == selector
                            )
                            {
                                if (type is { } || state is not State.None || sb.Length > 0)
                                {
                                    ThrowUnexpectedLine(_lineNumber, line);
                                }
                                type = new Composite
                                {
                                    Code = m.Groups[s_code].Value,
                                    Change = m.Groups[s_change].Value,
                                };
                                state = State.Name;
                                sb.Append(m.Groups[s_name].Value.Trim());
                            }
                            if (
                               selector is Selector.None
                               && (m = s_reDescription.Match(line)).Success && (selector = Selector.Desc) == selector
                               || (m = s_reNote.Match(line)).Success && (selector = Selector.Note) == selector
                            )
                            {
                                if (type is null || sb.Length > 0 || state is not State.None)
                                {
                                    ThrowUnexpectedLine(_lineNumber, line);
                                }
                                state = selector switch { Selector.Desc => State.Desc, _ => State.Note };
                                string group = selector switch { Selector.Desc => s_description, _ => s_note };
                                sb.Append(m.Groups[group].Value.Trim());
                            }
                        }
                        if (selector is Selector.None && s_reHr.Match(line).Success && (selector = Selector.Hr) == selector)
                        {
                            if (hrs > 0)
                            {
                                if (type is null || state is not State.None || sb.Length > 0)
                                {
                                    ThrowUnexpectedLine(_lineNumber, line);
                                }
                                yield return type!;
                                type = null;
                            }
                            ++hrs;
                        }
                    }
                    else if (hrs > 0)
                    {
                        if (
                            type is null 
                            || (
                                state is not State.Name
                                && state is not State.Desc
                                && state is not State.Note 
                                && state is not State.ItemName
                            )
                        )
                        {
                            ThrowUnexpectedLine(_lineNumber, line);
                        }
                        if (sb.Length > 0)
                        {
                            if(state is State.ItemName)
                            {
                                sb.Append(' ');
                            }
                            else
                            {
                                sb.AppendLine();
                            }
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
                    ThrowUnexpectedLine(_lineNumber, lastLine);
                }
                if (state is State.Desc)
                {
                    type!.Description = sb.ToString();
                    sb.Clear();
                    state = State.None;
                }
                else if (state is State.Note)
                {
                    type!.Note = sb.ToString();
                    sb.Clear();
                    state = State.None;
                }
                else if (state is State.Name)
                {
                    type!.Name = sb.ToString();
                    sb.Clear();
                    state = State.None;
                }
                else if (state is State.ItemName)
                {
                    type!.Elements.Last().Name = sb.ToString();
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
