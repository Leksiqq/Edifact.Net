using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using static Net.Leksi.Edifact.Constants;

namespace Net.Leksi.Edifact;

internal class MessageParser(string hrChars) : Parser(hrChars)
{
    private enum State { None, SegmentGroup, Desc, Note };
    private enum Selector { None, Name, SegmentGroup, Desc, Note, Children };
    private enum Stage { Struct, WaitOccurs, Occurs, Done };

    private const string s_reStartStopOccursPatternFormat = "^\\s*\\d{{4,}}\\s+{0}\\s";
    private static readonly Regex s_reName = new("^\\s*(?<position>\\d{4,})(\\s*(?<change>[+*|#X-]+))?\\s*(?<code>[A-Z]{3}),(?<name>.+)$");
    private static readonly Regex s_reSG = new("^\\s*(?<position>\\d{4,})(\\s*(?<change>[+*|#X-]+))?\\s*Segment\\s+group\\s+(?<code>\\d+):\\s*(?<children>.+)$");
    private static readonly Regex s_reChildren = new("^\\s+(?<children>-?(?:(?:[A-Z]{3}|SG\\d+)-)*(?:[A-Z]{3}|SG\\d+)-?)$");
    private static readonly Regex s_reNote = new("^(?:\\s*[+*|#X-]+)?\\s+Note\\s*\\:(?<note>.*)$");
    private static readonly Regex s_reOccurs = new("^\\s*(?<position>\\d{4,})(?:\\s*[+*|#X-]+)?\\s+.+?\\s+(?<minOccurs>[CM])\\s+(?<maxOccurs>\\d+)[^0-9]*$");
    private static readonly char[] separator = ['-'];
    internal async IAsyncEnumerable<Segment> ParseAsync
    (
        TextReader reader, [EnumeratorCancellation] CancellationToken stoppingToken
    )
    {
        Regex? _reStartStopOccurs = null;
        StringBuilder sb = new();
        Segment? type = null;
        State state = State.None;
        Stage stage = Stage.Struct;
        void complete()
        {
            if (state is State.Desc)
            {
                type!.Description = sb.ToString();
                sb.Clear();
            }
            else if (state is State.Note)
            {
                type!.Note = sb.ToString();
                sb.Clear();
            }
            state = State.None;
        }
        _lineNumber = 0;
        string? startSegment = null;
        string? lastSegment = null;
        string? stopSegment = null;
        int childrenStart = 0;
        await foreach (IEnumerable<string> lines in SplitByNewLineAsync(reader, stoppingToken))
        {
            int pos = 0;
            Match m;
            Selector selector = Selector.None;
            if (
                stage is Stage.Struct 
                && stopSegment is { } 
                && lastSegment == stopSegment
            )
            {
                if (type is { })
                {
                    complete();
                    yield return type;
                    type = null;
                }
                stage = Stage.WaitOccurs;
                _reStartStopOccurs = new Regex(string.Format(s_reStartStopOccursPatternFormat, startSegment));
            }
            foreach (string line in lines)
            {
                selector = Selector.None;
                ++_lineNumber;
                if (stage is Stage.Struct)
                {
                    if (
                        selector is Selector.None
                        && (m = s_reName.Match(line)).Success
                        && (selector = Selector.Name) == selector
                    )
                    {
                        if (type is { })
                        {
                            complete();
                            yield return type!;
                            type = null;
                        }
                        lastSegment = m.Groups[s_code].Value;
                        if (startSegment is null)
                        {
                            startSegment = m.Groups[s_code].Value;
                            stopSegment = startSegment switch { s_unh => s_unt, s_uih => s_uit, _ => null };
                            if (stopSegment is null)
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
                            Position = m.Groups[s_position].Value,
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
                            complete();
                            yield return type!;
                            type = null;
                        }
                        if (type is { } || state is not State.None || sb.Length > 0)
                        {
                            ThrowUnexpectedLine(_lineNumber, line);
                        }
                        type = new Segment
                        {
                            Code = string.Format(s_segmentGroupNameFormat, m.Groups[s_code].Value),
                            Change = m.Groups[s_change].Value,
                            Position = m.Groups[s_position].Value,
                        };
                        sb.Append(m.Groups[s_children].Value.Trim());
                        childrenStart = m.Groups[s_children].Index;
                        state = State.SegmentGroup;
                    }
                    if (
                        selector is Selector.None
                        && state is State.SegmentGroup
                        && (m = s_reChildren.Match(line)).Success
                        && m.Groups[s_children].Index == childrenStart
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
                        if (type is null)
                        {
                            ThrowUnexpectedLine(_lineNumber, line);
                        }
                        complete();
                        if (state is State.Desc)
                        {
                            type!.Description = sb.ToString();
                            sb.Clear();
                        }
                        else
                        {
                            type!.Children = sb.ToString()
                                .Split(
                                    separator,
                                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                                )
                            ;
                            sb.Clear();
                        }
                        sb.Append(m.Groups[s_note].Value.Trim());
                        state = State.Note;
                    }
                    if (
                        selector is Selector.None
                        && (state is State.None || state is State.SegmentGroup || state is State.Desc || state is State.Note)
                        && startSegment is { }
                    )
                    {
                        if (state is State.None)
                        {
                            if (type is null || sb.Length > 0)
                            {
                                ThrowUnexpectedLine(_lineNumber, line);
                            }
                            state = State.Desc;
                            sb.Append(line.Trim());
                        }
                        else if (state is State.SegmentGroup)
                        {
                            if (type is null)
                            {
                                ThrowUnexpectedLine(_lineNumber, line);
                            }
                            type!.Children = sb.ToString()
                                .Split(
                                separator,
                                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                            ;
                            sb.Clear();
                            state = State.Desc;
                            sb.Append(line.Trim());
                        }
                        else
                        {
                            sb.AppendLine().Append(line.Trim());
                        }
                    }
                }
                else if (stage is Stage.WaitOccurs)
                {
                    if ((m = _reStartStopOccurs!.Match(line)).Success)
                    {
                        stage = Stage.Occurs;
                        _reStartStopOccurs = new Regex(string.Format(s_reStartStopOccursPatternFormat, stopSegment));
                    }
                }
                if (stage is Stage.Occurs)
                {
                    if((m = s_reOccurs.Match(line)).Success)
                    {
                        type = new Segment
                        {
                            Position = m.Groups[s_position].Value,
                            MinOccurs = m.Groups[s_minOccurs].Value,
                            MaxOccurs = m.Groups[s_maxOccurs].Value,
                        };
                        yield return type;
                    }
                    if ((m = _reStartStopOccurs!.Match(line)).Success)
                    {
                        stage = Stage.Done;
                    }
                }
                ++pos;
            }
        }
        if (type is { })
        {
            complete();
            yield return type!;
        }
    }
}
