using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using static Net.Leksi.Edifact.Constants;
using static Net.Leksi.Edifact.PartsParser;

namespace Net.Leksi.Edifact;

internal class SegmentParser(string hrChars, char nameFirstChar) : Parser(hrChars)
{
    private enum State { None, Note, Function }
    private enum Selector { None, Name, ItemName, Note, Function, Hr }
    private static readonly Regex s_reName = new("^(?:\\s*(?<change>[+*|#X-]+))?\\s*(?<code>[A-Z]{3})\\s+(?<name>.+)$");
    private static readonly Regex s_reFunction = new("\\s+Function\\s*\\:(?<function>.*)$");
    private static readonly Regex s_reNote = new("\\s+Note\\s*\\:(?<note>.*)$");
    private readonly Regex _reItemName = new($"^\\s*(?<position>\\d{{3}})(?:\\s+(?<change>[+*|#X-]+))?\\s*(?<code>[\\d{nameFirstChar}]\\d{{3}})\\s+(?<name>.+?)\\s+(?<minOccurs>[CM])\\s+(?<maxOccurs>\\d*)\\s*a?n?\\d*\\.?\\.?\\d*$");
    internal async IAsyncEnumerable<Segment> ParseAsync(
        TextReader reader, [EnumeratorCancellation] CancellationToken stoppingToken
    )
    {
        StringBuilder sb = new();
        Segment? type = null;
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
                if (pos == 0)
                {
                    if (hrs > 0)
                    {
                        if(
                            selector is Selector.None
                            && (m = s_reName.Match(line)).Success
                            && (selector = Selector.Name) == selector
                        )
                        {
                            if(type is { } || state is not State.None || sb.Length > 0)
                            {
                                ThrowUnexpectedLine(_lineNumber, line);
                            }
                            type = new Segment
                            {
                                Code = m.Groups[s_code].Value,
                                Name = m.Groups[s_name].Value.Trim(),
                                Change = m.Groups[s_change].Value.Trim(),
                                Components = [],
                            };
                        }
                        if (
                            selector is Selector.None
                            && (m = _reItemName.Match(line)).Success
                            && (selector = Selector.ItemName) == selector
                        )
                        {
                            if (type is null || state is not State.None || sb.Length > 0)
                            {
                                ThrowUnexpectedLine(_lineNumber, line);
                            }
                            type!.Components!.Add(new Component
                            {
                                Code = m.Groups[s_code].Value,
                                Name = m.Groups[s_name].Value.Trim(),
                                Change = m.Groups[s_change].Value.Trim(),
                                MinOccurs = m.Groups[s_minOccurs].Value,
                                MaxOccurs = m.Groups[s_maxOccurs].Value,
                                Position = m.Groups[s_position].Value,
                            });
                        }
                        if (
                            selector is Selector.None
                            && (
                                (m = s_reFunction.Match(line)).Success && (selector = Selector.Function) == selector
                                || (m = s_reNote.Match(line)).Success && (selector = Selector.Note) == selector
                            )
                        )
                        {
                            if (type is null || state is not State.None || sb.Length > 0)
                            {
                                ThrowUnexpectedLine(_lineNumber, line);
                            }
                            sb.Append(m.Groups[selector switch { Selector.Function => s_function, _ => s_note}].Value.Trim());
                            state = selector switch { Selector.Function => State.Function, _ => State.Note };
                        }
                    }
                    if (
                        selector is Selector.None 
                        && s_reHr.Match(line).Success 
                        && (selector = Selector.Hr) == selector
                    )
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
                            state is not State.Function
                            && state is not State.Note
                            && state is not State.None
                        )
                    )
                    {
                        ThrowUnexpectedLine(_lineNumber, line);
                    }
                    if (state is not State.None)
                    {
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
                    ThrowUnexpectedLine(_lineNumber, lastLine);
                }
                if (state is State.Note)
                {
                    type!.Note = sb.ToString();
                    sb.Clear();
                    state = State.None;
                }
                else if (state is State.Function)
                {
                    type!.Function = sb.ToString();
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
