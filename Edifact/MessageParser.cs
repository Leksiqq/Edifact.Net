using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Text;
using static Net.Leksi.Edifact.Constants;

namespace Net.Leksi.Edifact;

internal class MessageParser(string hrChars) : Parser(hrChars)
{
    private enum State { None, SegmentGroup, Desc, Note };
    private enum Selector { None, Name, SegmentGroup, Desc, Note, Children };

    private static readonly Regex s_reName = new("^\\d{4}(\\s*(?<change>[+*|#X-]+))?\\s*(?<code>[A-Z]{3}),(?<name>.+)$");
    private static readonly Regex s_reSG = new("^\\d{4}(\\s*(?<change>[+*|#X-]+))?\\s*Segment\\s+group\\s+(?<code>\\d+):(?<children>.+)$");
    private static readonly Regex s_reChildren = new("^\\s+(?<children>-?(?:(?:[A-F]{3}|SG\\d+)-)*(?:[A-F]{3}|SG\\d+))$");
    private static readonly Regex s_reNote = new("^(?:\\s*[+*|#X-]+)?\\s+Note\\s*\\:(?<note>.*)$");
    internal async IAsyncEnumerable<Segment> ParseAsync
    (
        TextReader reader, [EnumeratorCancellation] CancellationToken stoppingToken
    )
    {
        StringBuilder sb = new();
        Segment? type = null;
        string lastLine = string.Empty;
        _lineNumber = 0;
        State state = State.None;
        string? startSegment = null;
        string? lastSegment = null;
        string? finishSegment = null;
        await foreach (IEnumerable<string> lines in SplitByNewLineAsync(reader, stoppingToken))
        {
            if (finishSegment is { } && lastSegment == finishSegment)
            {
                _running = false;
                yield break;
            }
            int pos = 0;
            Match m;
            Selector selector = Selector.None;
            foreach (string line in lines)
            {
                Console.WriteLine(line);
                selector = Selector.None;
                lastLine = line;
                ++_lineNumber;
                if (pos == 0)
                {
                    if (
                        selector is Selector.None
                        && (m = s_reName.Match(line)).Success
                        && (selector = Selector.Name) == selector
                    )
                    {
                        if (type is { })
                        {
                            if (state is State.Desc)
                            {
                                type.Description = sb.ToString();
                                sb.Clear();
                            }
                            else if (state is State.Note)
                            {
                                type.Note = sb.ToString();
                                sb.Clear();
                            }
                            yield return type!;
                            state = State.None;
                            type = null;
                        }
                        lastSegment = m.Groups[s_code].Value;
                        if (startSegment is null)
                        {
                            startSegment = m.Groups[s_code].Value;
                            finishSegment = startSegment switch { s_unh => s_unt, s_uih => s_uit, _ => null };
                            if (finishSegment is null)
                            {
                                ThrowUnexpectedLine(_lineNumber, line);
                            }
                        }
                        if (type is { } || state is not State.None || sb.Length > 0)
                        {
                            ThrowUnexpectedLine(_lineNumber, line);
                        }
                        type = new Segment
                        {
                            Code = m.Groups[s_code].Value,
                            Change = m.Groups[s_change].Value,
                            Name = m.Groups[s_name].Value.Trim(),
                        };
                    }
                    if (
                        selector is Selector.None
                        && (m = s_reSG.Match(line)).Success
                        && (selector = Selector.SegmentGroup) == selector
                    )
                    {
                        if (type is { })
                        {
                            if (state is State.Desc)
                            {
                                type.Description = sb.ToString();
                                sb.Clear();
                            }
                            else if (state is State.Note)
                            {
                                type.Note = sb.ToString();
                                sb.Clear();
                            }
                            yield return type!;
                            type = null;
                            state = State.None;
                        }
                        if (type is { } || state is not State.None || sb.Length > 0)
                        {
                            ThrowUnexpectedLine(_lineNumber, line);
                        }
                        type = new Segment
                        {
                            Code = string.Format("SG-{0}", m.Groups[s_code].Value),
                            Change = m.Groups[s_change].Value,
                        };
                        sb.Append(m.Groups[s_children].Value.Trim());
                        state = State.SegmentGroup;
                    }
                    if (
                        selector is Selector.None
                        && state is State.SegmentGroup
                        && (m = s_reChildren.Match(line)).Success
                        && (selector = Selector.Children) == selector
                    )
                    {
                        sb.Append(m.Groups[s_children].Value.Trim());
                    }
                    if (
                        selector is Selector.None
                        && (state is State.Desc || state is State.SegmentGroup)
                        && (m = s_reNote.Match(line)).Success
                        && (selector = Selector.Note) == selector
                    )
                    {
                        if(type is null)
                        {
                            ThrowUnexpectedLine(_lineNumber, line);
                        }
                        if(state is State.Desc)
                        {
                            type!.Description = sb.ToString();
                            sb.Clear();
                        }
                        else
                        {
                            type!.Name = sb.ToString();
                            sb.Clear();
                        }
                        sb.Append(m.Groups[s_note].Value.Trim());
                        state = State.Note;
                    }
                    if(
                        selector is Selector.None
                        && (state is State.None || state is State.SegmentGroup || state is State.Desc || state is State.Note)
                        && startSegment is { }
                    )
                    {
                        if(state is State.None)
                        {
                            if(type is null || sb.Length > 0)
                            {
                                ThrowUnexpectedLine(_lineNumber, line);
                            }
                            state = State.Desc;
                            sb.Append(line.Trim());
                        }
                        else if(state is State.SegmentGroup)
                        {
                            if (type is null)
                            {
                                ThrowUnexpectedLine(_lineNumber, line);
                            }
                            type!.Name = sb.ToString();
                            sb.Clear();
                            state = State.Desc;
                            sb.Append(line.Trim());
                        }
                        else
                        {
                            sb.Append(' ').Append(line.Trim());
                        }
                    }
                }
                ++pos;
            }
        }
        if (type is { })
        {
            if (state is State.Desc)
            {
                type.Description = sb.ToString();
            }
            else if (state is State.Note)
            {
                type.Note = sb.ToString();
            }
            yield return type!;
        }
    }
}
