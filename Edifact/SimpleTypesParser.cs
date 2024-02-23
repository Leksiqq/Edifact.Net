
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using System.Text.RegularExpressions;

namespace Net.Leksi.Edifact;

internal class SimpleTypesParser
{
    private enum State { None, Desc, Note}
    private static readonly Regex s_reName = new("^\\s*(?<change>[+*|#X-])?\\s+(?<code>\\d{4})\\s+(?<name>[^\\[]+).*$");
    private static readonly Regex s_reDescription = new("^\\s*Desc\\s*\\:(?<description>.*)$");
    private static readonly Regex s_reNote = new("^\\s*Note\\s*\\:(?<note>.*)$");
    private static readonly Regex s_reHr = new("^[-]{5,}");
    private static readonly Regex s_reRepr = new("^\\s*Repr\\s*\\:(?<representation>.*)$");
    internal async IAsyncEnumerable<SimpleType> ParseAsync(TextReader reader)
    {
        State state = State.None;
        SimpleType? type = null;
        StringBuilder sb = new();
        while (await reader.ReadLineAsync() is string line)
        {
            Match m;
            if ((m = s_reName.Match(line)).Success)
            {
                if(type is { })
                {
                    yield return type;
                }
                type = new()
                {
                    Code = m.Groups["code"].Value,
                    Name = m.Groups["name"].Value.Trim(),
                    Change = m.Groups["change"].Value,
                };
                state = State.None;
            }
            else if((m = s_reDescription.Match(line)).Success)
            {
                FlushBUilder(state, type, sb);
                state = State.Desc;
                sb.Append(m.Groups["description"].Value.Trim());
            }
            else if((m = s_reNote.Match(line)).Success)
            {
                FlushBUilder(state, type, sb);
                state = State.Note;
                sb.Append(m.Groups["note"].Value.Trim());
            }
            else if ((m = s_reRepr.Match(line)).Success)
            {
                FlushBUilder(state, type, sb);
                state = State.None;
                type!.Representation = m.Groups["representation"].Value.Trim();
            }
            else if (s_reHr.Match(line).Success)
            {
                FlushBUilder(state, type, sb);
                state = State.None;
            }
            else if(state is State.Desc || state is State.Note)
            {
                string data = line.Trim();
                if(!string.IsNullOrEmpty(data))
                {
                    sb.AppendLine();
                    sb.Append(line.Trim());
                }
            }
        }
        FlushBUilder(state, type, sb);
        if (type is { })
        {
            yield return type;
        }
    }

    private static void FlushBUilder(State state, SimpleType? type, StringBuilder sb)
    {
        if (state is not State.None && sb.Length > 0)
        {
            switch (state)
            {
                case State.Desc:
                    type!.Description = sb.ToString();
                    break;
                case State.Note:
                    type!.Note = sb.ToString();
                    break;
            }
            sb.Clear();
        }
    }
}