using System.Text.RegularExpressions;

namespace Net.Leksi.Edifact;

internal class PartsParser
{
    private static readonly Regex reLine = new Regex("^(.*?)[-─]{5,}\\s*$");

    internal delegate void Part();
    internal delegate void Line(string line);

    internal event Part OnPart;
    internal event Line OnLine;

    internal PartsParser()
    {
        OnPart += new Part(delegate() { });
        OnLine += new Line(delegate(string line) { });
    }

    protected internal virtual void Run(string[] data)
    {
        foreach (string line in data)
        {
            Match m;
            m = reLine.Match(line);
            if (m.Success)
            {
                if (m.Groups[1].Captures[0].Length > 0)
                {
                    OnLine(m.Groups[1].Captures[0].Value);
                }
                OnPart();
            }
            else if (line.Trim().Length > 0 && line.Trim()[0] == '→')
            {
                break;
            }
            else
            {
                OnLine(line);
            }
        }

    }
}
