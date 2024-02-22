using System.Text;
using System.Text.RegularExpressions;

namespace Net.Leksi.Edifact;

internal class Compiler96B
{
    private const string s_fileNameFormat = "{0}.{1}";
    private const string s_fileDNameFormat = "{0}_d.{1}";
    private const string s_htmPatternFormat = "{0}*.HTM";
    private const string s_hr = "<hr/>";
    private const string s_dPostfix = "_d";
    private const string s_sPostfix = "_s";
    private const string s_htmPostfixNameFormat = "{0}{1}.HTM";
    private const string s_longDashedLine = "----------------------------------------------------------------------";
    private const string s_uncl = "UNCL";
    private const string s_trcd = "TRCD";
    private const string s_trsd = "TRSD";
    private const string s_unsl = "UNSL";
    private const string s_tred = "TRED";
    private static readonly Regex reHR = new("^.*?<hr\\s*/?>.*$", RegexOptions.IgnoreCase);
    private static readonly Regex reTag = new("^(?:.*?(</?\\w+[^>]*>).*?)*$");
    private static readonly Regex reNoCode = new("^.*?There\\s+are\\s+no\\s+codes\\s+for\\s+the\\s+Data\\s+Element .*?!.*$");
    private static readonly Regex reRepr = new("^\\s*Repr:.*$");

    private string dir = null!;
    private string release = null!;

    internal void Run(string dir, string release, string? message = null)
    {
        this.dir = dir;
        this.release = release;
        Process([s_uncl], s_tred, 4, false, false);
        Process([s_trcd], s_trcd, 4, false, false);
        Process([s_trcd], s_trcd, 4, false, false);
        Process([s_trsd], s_trsd, 3, false, false);
        Process([s_uncl], s_uncl, 4, true, false);
        Process([s_unsl], s_unsl, 0, true, true);
        if (message is { })
        {
            Process1(message);
        }
    }

    private void Process(string[] prefixes, string filename, int name_size, bool skip_nocode, bool add_hr)
    {
        TextWriter tw = new StreamWriter(Path.Combine(dir, string.Format(s_fileNameFormat, filename.ToUpper(), release[1..])), false);
        List<string> lines = [];
        foreach (string prefix in prefixes)
        {
            string[] files = Directory.GetFiles(dir, string.Format(s_htmPatternFormat, prefix.ToUpper()));
            foreach (string file in files)
            {
                if (Path.GetFileNameWithoutExtension(file).Length == prefix.Length + name_size)
                {
                    lines.Clear();
                    lines.AddRange(File.ReadAllLines(file, Encoding.GetEncoding(866)));
                    if (add_hr)
                    {
                        lines.Insert(0, s_hr);
                        lines.Add(s_hr);
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
        TextWriter tw = new StreamWriter(Path.Combine(dir, string.Format(s_fileDNameFormat, message.ToUpper(), release[1..])), false);
        List<string> lines = [];
        foreach (string postfix in new string[]{ s_dPostfix, s_sPostfix })
        {
            if (File.Exists(Path.Combine(dir, string.Format(s_htmPostfixNameFormat, message, postfix))))
            {
                lines.Clear();
                lines.Add(s_hr);
                lines.AddRange(File.ReadAllLines(Path.Combine(dir, string.Format(s_htmPostfixNameFormat, message.ToUpper(), postfix))));
                string[] data = new string[lines.Count];
                lines.CopyTo(data);
                Copy1(data, tw, false);
            }
        }
        tw.Close();
    }

    private static void Copy1(string[] data, TextWriter tw, bool skip_nocode)
    {
        int step = 0;
        Match m;
        StringBuilder sb = new();
        List<string> lines = [];
        bool hasRepr = false;
        foreach (string line in data)
        {
            switch (step)
            {
                case 0:
                    if (reHR.IsMatch(line))
                    {
                        step = 1;
                        lines.Add(s_longDashedLine);
                    }
                    break;
                case 1:
                    if (
                        reHR.IsMatch(line) 
                        || reNoCode.IsMatch(line) 
                        || !skip_nocode && "".Equals(line.Trim()) && hasRepr
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
                    hasRepr = reRepr.IsMatch(line);
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
