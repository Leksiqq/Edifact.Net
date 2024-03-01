using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using static Net.Leksi.Edifact.Constants;

namespace Net.Leksi.Edifact;

internal class EnumerationParser : Parser
{
    private enum State { None, TypeName, Name, Desc, Note, DescRepr }
    private enum Selector { None, TypeNameBegin, TypeNameEnd, Name, Desc, Note, Hr, DescRepr }

    private static readonly Regex s_reTypeNameBegin = new("^(?:\\s*(?<change>[+*|#X-]+))?\\s+(?<code>\\d{4})\\s+(?<name>[^[]+)");
    private static readonly Regex s_reName = new("^(\\s*(?<change>[+*|#X-]+))?\\s+(?<code>[^\\s]+)\\s+(?<name>.+)$");
    private static readonly Regex s_reNote = new("^(\\s*(?<change>[+*|#X-]+))?\\s+(?<rest>Note\\s*\\:(?<note>.*))$");
    private static readonly Regex s_reRest = new("^\\s*(?<rest>.+)$");
    private static readonly Regex s_reDescRepr = new("\\s+(?:Desc|Repr|Note)\\s*\\:");
    internal async IAsyncEnumerable<Enumeration> ParseAsync(
        TextReader reader, [EnumeratorCancellation] CancellationToken stoppingToken
    )
    {
        int nameOffset = -1;
        int descOffset = -1;
        StringBuilder sb = new();
        Enumeration? type = null;
        int hrs = 0;
        string lastLine = string.Empty;
        string? typeCode = null;
        int numTypes = 0;
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
                //if (pos == 0)
                //{
                    if (hrs > 0)
                    {
                        if(pos == 0)
                        {
                        if (
                            selector is Selector.None
                            && state is not State.DescRepr
                            && (m = s_reTypeNameBegin.Match(line)).Success
                            && (selector = Selector.TypeNameBegin) == selector
                        )
                        {
                            if (type is { } || state is not State.None || typeCode is { } || sb.Length > 0)
                            {
                                ThrowUnexpectedLine(_lineNumber, line);
                            }
                            typeCode = m.Groups[s_code].Value;
                            numTypes = 0;
                            state = State.TypeName;
                        }
                        if (
                            (selector is Selector.None || selector is Selector.TypeNameBegin)
                            && state is not State.DescRepr
                            && (m = s_reTypeNameBegin.Match(line)).Success
                            && (selector = Selector.TypeNameEnd) == selector
                        )
                        {
                            if (type is { } || state is not State.TypeName || typeCode is null)
                            {
                                ThrowUnexpectedLine(_lineNumber, line);
                            }
                        }
                        if (
                            selector is Selector.None
                            && state is not State.DescRepr
                            && numTypes == 0
                            && s_reDescRepr.Match(line).Success
                            && (selector = Selector.DescRepr) == selector
                        )
                        {
                            if (
                                type is { } 
                                || (state is not State.TypeName && state is not State.None)
                                || typeCode is null 
                                || sb.Length > 0
                            )
                            {
                                ThrowUnexpectedLine(_lineNumber, line);
                            }
                            state = State.DescRepr;
                        }
                    }
                    if (
                            selector is Selector.None
                            && state is not State.DescRepr
                            && state is not State.Note
                            && (m = s_reName.Match(line)).Success
                            && (selector = Selector.Name) == selector
                        )
                        {
                            if(nameOffset == -1)
                            {
                                nameOffset = m.Groups[s_name].Index;
                            }
                            if (nameOffset != m.Groups[s_name].Index)
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
                                    if (state is State.Desc)
                                    {
                                        type.Description = sb.ToString();
                                        sb.Clear();
                                        state = State.None; 
                                    }
                                    yield return type;
                                }
                                if (state is not State.None || typeCode is null || sb.Length > 0)
                                {
                                    ThrowUnexpectedLine(_lineNumber, line);
                                }
                                type = new Enumeration
                                {
                                    Change = m.Groups[s_change].Value,
                                    TypeCode = typeCode,
                                    Code = m.Groups[s_code].Value,
                                };
                                ++numTypes;
                                sb.Append(m.Groups[s_name].Value.Trim());
                                state = State.Name;
                            }
                        }
                        if(
                            selector is Selector.None
                            && state is not State.DescRepr
                            && (m = s_reNote.Match(line)).Success 
                            && (selector = Selector.Note) == selector
                        )
                        {
                            if(type is null || state is not State.None || nameOffset != m.Groups[s_rest].Index || sb.Length > 0)
                            {
                                ThrowUnexpectedLine(_lineNumber, line);
                            }
                            sb.Append(m.Groups[s_note].Value.Trim());
                            state = State.Note;
                        }
                    }
                    if (selector is Selector.None && s_reHr.Match(line).Success && (selector = Selector.Hr) == selector)
                    {
                        if (hrs > 0)
                        {
                            if (typeCode is null || type is null || state is not State.None || sb.Length > 0)
                            {
                                ThrowUnexpectedLine(_lineNumber, line);
                            }
                            yield return type!;
                            type = null;
                            typeCode = null;
                        }
                        ++hrs;
                    }
                //}
                //else
                if(hrs > 0 && state is not State.DescRepr)
                {
                    if((m = s_reRest.Match(line)).Success)
                    {
                        if(state is State.Name)
                        {
                            if(m.Groups[s_rest].Index == nameOffset)
                            {
                                sb.Append(' ').Append(m.Groups[s_rest].Value.Trim());
                            }
                            else if(m.Groups[s_rest].Index > nameOffset)
                            {
                                if(descOffset == -1)
                                {
                                    descOffset = m.Groups[s_rest].Index;
                                }
                                else if(m.Groups[s_rest].Index < descOffset)
                                {
                                    ThrowUnexpectedLine(_lineNumber, line);
                                }
                                if (type is null || sb.Length == 0)
                                {
                                    ThrowUnexpectedLine(_lineNumber, line);
                                }
                                type!.Name = sb.ToString();
                                sb.Clear();
                                sb.Append(m.Groups[s_rest].Value);
                                selector = Selector.Desc;
                                state = State.Desc;
                            }
                        }
                        else if(state is State.Desc)
                        {
                            sb.AppendLine().Append(m.Groups[s_rest].Value.Trim());
                        }
                    }
                }
                ++pos;
            }
            if (hrs > 0 && selector is not Selector.Hr && state is not State.TypeName && state is not State.DescRepr)
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
            }
            if(state is State.DescRepr)
            {
                state = State.None;
            }
        }
        if (type is { })
        {
            yield return type;
        }
    }
}