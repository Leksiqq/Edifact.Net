using System.Text.RegularExpressions;

namespace Net.Leksi.Edifact;

internal class Preparser1 : PartsParser
{
    private enum Stages { NONE, DESC, CONT };

    private const string s_manyDashes = "----------------------------------------------------------------------";
    private static readonly Regex s_reDesc = new("^\\s*Desc:.*$");
    private static readonly Regex s_reCont = new("^(\\s*)(Cont|Repr):(.*)$");
    private readonly TextWriter _tw;
    private readonly string _savefile;
    private Stages _stage = Stages.NONE;
    private bool _started = false;

    internal Preparser1(string filename)
    {
        OnPart += Preparser1_OnPart;
        OnLine += Preparser1_OnLine;
        _tw = new StreamWriter(filename, false);
        _savefile = Path.GetDirectoryName(filename) + "\\" + Path.GetFileName(filename) + ".1";
        if (File.Exists(_savefile))
        {
            File.Delete(_savefile);
        }
    }

    protected internal override void Run(string[] data)
    {
        File.AppendAllLines(_savefile, data);
        try
        {
            base.Run(data);
        }
        finally
        {
            _tw.Close();
        }
    }

    private void Preparser1_OnLine(string line)
    {
        if (_started)
        {
            Match m;
            switch (_stage)
            {
                case Stages.DESC:
                    m = s_reDesc.Match(line);
                    if (m.Success)
                    {
                        _tw.WriteLine("");
                        _stage = Stages.CONT;
                    }
                    break;
                case Stages.CONT:
                    m = s_reCont.Match(line);
                    if (m.Success)
                    {
                        _tw.WriteLine("");
                        _stage = Stages.NONE;
                        if ("Cont".Equals(m.Groups[2].Captures[0].Value))
                        {
                            line = m.Groups[1].Captures[0].Value + m.Groups[3].Captures[0].Value;
                        }
                    }
                    break;
            }
        }
        _tw.WriteLine(line);
    }

    private void Preparser1_OnPart()
    {
        _started = true;
        _tw.WriteLine();
        _tw.WriteLine(s_manyDashes);
        _stage = Stages.DESC;
    }
}
