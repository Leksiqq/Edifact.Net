using System.Text;
using System.Text.RegularExpressions;

namespace Net.Leksi.Edifact;

internal class Compiler96B
{
    private string dir = null!;
    private string release = null!;
    private readonly Regex reHR = new("^.*?<hr\\s*/?>.*$", RegexOptions.IgnoreCase);
    private readonly Regex reTag = new("^(?:.*?(</?\\w+[^>]*>).*?)*$");
    private readonly Regex reNoCode = new("^.*?There are no codes for the Data Element .*?!.*$");
    private readonly Regex reRepr = new("^\\s*Repr:.*$");

    internal void Run(string dir, string release, string? message = null)
    {
        this.dir = dir;
        this.release = release;
        Process(["UNCL"], "TRED", 4, false, false);
        Process(["TRCD"], "TRCD", 4, false, false);
        Process(["TRCD"], "TRCD", 4, false, false);
        Process(["TRSD"], "TRSD", 3, false, false);
        Process(["UNCL"], "UNCL", 4, true, false);
        Process(["UNSL"], "UNSL", 0, true, true);
        if (message is { })
        {
            Process1(message);
        }
    }

    private void Process(string[] prefixes, string filename, int name_size, bool skip_nocode, bool add_hr)
    {
        TextWriter tw = new StreamWriter(dir + "\\" + filename.ToUpper() + "." + release.Substring(1), false, Encoding.ASCII);
        List<string> lines = [];
        foreach (string prefix in prefixes)
        {
            string[] files = Directory.GetFiles(dir, prefix.ToUpper() + "*.HTM");
            foreach (string file in files)
            {
                if (Path.GetFileNameWithoutExtension(file).Length == prefix.Length + name_size)
                {
                    lines.Clear();
                    lines.AddRange(File.ReadAllLines(file, Encoding.GetEncoding(866)));
                    if (add_hr)
                    {
                        lines.Insert(0, "<hr/>");
                        lines.Add("<hr/>");
                    }
                    string[] data = new string[lines.Count];
                    lines.CopyTo(data);
                    Copy1(data, tw, skip_nocode);
                }
            }
        }
        tw.Close();
    }

    private void Process1(string message)
    {
        TextWriter tw = new StreamWriter(dir + "\\" + message.ToUpper() + "_d." + release.Substring(1), false, Encoding.ASCII);
        List<string> lines = [];
        foreach (string postfix in new string[]{"_d", "_s"})
        {
            if (File.Exists(dir + "\\" + message + postfix + ".HTM"))
            {
                lines.Clear();
                lines.Add("<hr/>");
                lines.AddRange(File.ReadAllLines(dir + "\\" + message.ToUpper() + postfix + ".HTM", Encoding.GetEncoding(866)));
                string[] data = new string[lines.Count];
                lines.CopyTo(data);
                Copy1(data, tw, false);
            }
        }
        tw.Close();
    }

    private void Copy1(string[] data, TextWriter tw, bool skip_nocode)
    {
        int step = 0;
        Match m;
        StringBuilder sb = new();
        List<string> lines = [];
        bool has_repr = false;
        foreach (string line in data)
        {
            switch (step)
            {
                case 0:
                    if (reHR.IsMatch(line))
                    {
                        step = 1;
                        lines.Add("----------------------------------------------------------------------");
                    }
                    break;
                case 1:
                    if (
                        reHR.IsMatch(line) 
                        || reNoCode.IsMatch(line) 
                        || !skip_nocode && "".Equals(line.Trim()) && has_repr
                    )
                    {
                        bool output = reHR.IsMatch(line) || !skip_nocode;
                        if (output)
                        {
                            foreach (string line1 in lines)
                            {
                                tw.WriteLine(line1);
                            }
                        }
                        return;
                    }
                    has_repr = reRepr.IsMatch(line);
                    sb.Clear();
                    sb.Append(line);
                    m = reTag.Match(line);
                    if (m.Success)
                    {
                        for (int i = m.Groups[1].Captures.Count - 1; i >= 0; i--)
                        {
                            sb.Remove(m.Groups[1].Captures[i].Index, m.Groups[1].Captures[i].Length);
                        }
                    }
                    lines.Add(sb.ToString());
                    break;
            }
        }
    }
}
