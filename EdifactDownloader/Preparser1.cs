using System.Text;
using System.Text.RegularExpressions;

namespace Net.Leksi.Edifact;

public class Preparser1 : PartsParser
{
    TextWriter tw;
    bool started = false;
    Regex reDesc = new Regex("^\\s*Desc:.*$");
    Regex reCont = new Regex("^(\\s*)(Cont|Repr):(.*)$");
    enum Stages { NONE, DESC, CONT };
    Stages stage = Stages.NONE;
    Regex reHeaders = new Regex("^\\s*(POS)\\s+(TAG)\\s+(NAME)\\s+(S)\\s+(R)\\s*$", RegexOptions.IgnoreCase);
    string savefile;

    public Preparser1(string filename)
    {
        OnPart += new Part(on_part);
        OnLine += new Line(on_line);
        tw = new StreamWriter(filename, false, Encoding.GetEncoding(866));
        savefile = Path.GetDirectoryName(filename) + "\\" + Path.GetFileName(filename) + ".1";
        if (File.Exists(savefile))
        {
            File.Delete(savefile);
        }
    }

    public override void Run(string[] data)
    {
        File.AppendAllLines(savefile, data, Encoding.GetEncoding(866));
        try
        {
            base.Run(data);
        }
        finally
        {
            tw.Close();
        }
    }

    void on_line(string line)
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

    void on_part()
    {
        started = true;
        tw.WriteLine("");
        tw.WriteLine("----------------------------------------------------------------------");
        stage = Stages.DESC;
    }
}
