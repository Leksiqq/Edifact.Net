using System.Text.RegularExpressions;

namespace Net.Leksi.Edifact;

internal class Preparser1 : PartsParser
{
    private enum Stages { NONE, DESC, CONT };

    private const string s_manyDashes = "----------------------------------------------------------------------";
    private static readonly Regex reDesc = new("^\\s*Desc:.*$");
    private static readonly Regex reCont = new("^(\\s*)(Cont|Repr):(.*)$");
    private readonly TextWriter tw;
    private readonly string savefile;
    private Stages stage = Stages.NONE;
    private bool started = false;

    internal Preparser1(string filename)
    {
        OnPart += OnPartHandler;
        OnLine += OnLineHandler;
        tw = new StreamWriter(filename, false);
        savefile = Path.GetDirectoryName(filename) + "\\" + Path.GetFileName(filename) + ".1";
        if (File.Exists(savefile))
        {
            File.Delete(savefile);
        }
    }

    protected internal override void Run(string[] data)
    {
        File.AppendAllLines(savefile, data);
        try
        {
            base.Run(data);
        }
        finally
        {
            tw.Close();
        }
    }

    private void OnLineHandler(string line)
    {
        if (started)
        {
            Match m;
            switch (stage)
            {
                case Stages.DESC:
                    m = reDesc.Match(line);
                    if (m.Success)
                    {
                        tw.WriteLine("");
                        stage = Stages.CONT;
                    }
                    break;
                case Stages.CONT:
                    m = reCont.Match(line);
                    if (m.Success)
                    {
                        tw.WriteLine("");
                        stage = Stages.NONE;
                        if ("Cont".Equals(m.Groups[2].Captures[0].Value))
                        {
                            line = m.Groups[1].Captures[0].Value + m.Groups[3].Captures[0].Value;
                        }
                    }
                    break;
            }
        }
        tw.WriteLine(line);
    }

    private void OnPartHandler()
    {
        started = true;
        tw.WriteLine();
        tw.WriteLine(s_manyDashes);
        stage = Stages.DESC;
    }
}
