using System.Text.RegularExpressions;

namespace Net.Leksi.Edifact;

public class PartsParser
{
    Regex reLine = new Regex("^(.*?)[-─]{5,}\\s*$");

    public delegate void Part();
    public delegate void Line(string line);

    public event Part OnPart;
    public event Line OnLine;

    public PartsParser()
    {
        OnPart += new Part(delegate() { });
        OnLine += new Line(delegate(string line) { });
    }

    public virtual void Run(string[] data)
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
