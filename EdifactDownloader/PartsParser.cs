using System.Text.RegularExpressions;

namespace Net.Leksi.Edifact;

internal class PartsParser
{

    internal delegate void Part();
    internal delegate void Line(string line);

    internal event Part? OnPart;
    internal event Line? OnLine;

    private static readonly Regex s_reLine = new("^(.*?)[-─]{5,}\\s*$");
    protected internal virtual void Run(string[] data)
    {
        foreach (string line in data)
        {
            Match m;
            m = s_reLine.Match(line);
            if (m.Success)
            {
                if (m.Groups[1].Captures[0].Length > 0)
                {
                    OnLine?.Invoke(m.Groups[1].Captures[0].Value);
                }
                OnPart?.Invoke();
            }
            else if (line.Trim().Length > 0 && line.Trim()[0] == '→')
            {
                break;
            }
            else
            {
                OnLine?.Invoke(line);
            }
        }

    }
}
