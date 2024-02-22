using System.Text;
using System.Text.RegularExpressions;

namespace Net.Leksi.Edifact;

internal class MParser
{
    internal delegate void EndMessage();
    internal delegate void BeginSG(int num, string desc);
    internal delegate void EndSG(int num);
    internal delegate void Segment(string name, string info, string desc, int sg);
    internal delegate void Occurs(int sgnum, string segment, int min_occurs, int max_occurs);
    private enum Stage { NONE, SEGORSG, SG_REST, DESC, OCCURS, DONE };
    private class Node
    {
        internal string Name { get; set; }
        internal int SgNum { get; set; }
        internal readonly List<int> SgEnd = [];
        internal Node(string name, int sgNum)
        {
            Name = name;
            SgNum = sgNum;
        }
    }

    internal event EndMessage? OnEndMessage;
    internal event BeginSG? OnBeginSG;
    internal event EndSG? OnEndSG;
    internal event Segment? OnSegment;
    internal event Occurs? OnOccurs;

    private const string s_sgNameFormat = "SG{0}";
    private const string s_unt = "UNT";
    private const string s_unh = "UNH";
    private const string s_reOccSegmentFormat = "^.{{{0}}}\\d{{4}}([-\\s\\*+#|X]{{{1}}})(\\w[\\w\\d]*)\\s.*$";
    private const string s_reOccSGFormat = "^.{{{0}}}\\d{{4}}([-\\s\\*+#|X]{{{1}}}).*?Segment\\s+group\\s+(\\d+).*$";
    private const string s_reSFormat = "^.{{{0}}}([CM]).*$";
    private const string s_reRFormat = "^.{{{0}}}(\\d+).*$";
    private const string s_notEqual = "{0} <> {1}";
    private static readonly Regex s_reSegment = new("^\\d{4,}\\s+([-\\*+#|X]*)\\s*(\\w[\\w\\d]*)\\s*,?\\s*(.*)$");
    private static readonly Regex s_reSG = new("^\\d{4,}\\s+([-\\*+#|X]*)\\s*Segment\\s+group\\s+(\\d+)\\s*:\\s*(?:(\\w[\\w\\d]*)\\s*-\\s*)*(\\w[\\w\\d]*)?[\\s.]*$");
    private static readonly Regex s_reSGRest = new("^\\s+(?:(\\w[\\w\\d]*)\\s*-\\s*)*(\\w[\\w\\d]*)?[\\s.]*$");
    private static readonly Regex s_reHeaders = new("^\\s*(\\d{4,})\\s+(UNH)\\s+(Message\\s+header)\\s+(M)\\s+(1)\\s*$", RegexOptions.IgnoreCase);

    private Stage _stage = Stage.NONE;
    private readonly List<int> _sgStack = [];
    private readonly StringBuilder _desc = new();

    private Regex _reOccSegment = null!;
    private Regex _reOccSG = null!;
    private Regex _reS = null!;
    private Regex _reR = null!;
    private string? _segment;
    private string _info = null!;
    private int _sgNum = -1;
    private int _nodePos = -1;
    private int _nodeOff = 0;
    private bool _deleted = false;

    private readonly List<Node> _nodes = [];
    internal int LineNumber { get; private set; } = 0;
    public void Run(string[] data)
    {
        _stage = Stage.SEGORSG;
        foreach (string line in data)
        {
            ++LineNumber;
            OnLine(line);
        }
    }
    
    private void OnLine(string line)
    {
        Match m;
        while (true)
        {
            switch (_stage)
            {
                case Stage.SEGORSG:
                    if (!string.IsNullOrEmpty(line.Trim()))
                    {
                        m = s_reSG.Match(line);
                        if (m.Success)
                        {
                            SegOrSgEvent();
                            _sgNum = int.Parse(m.Groups[2].Captures[0].Value.Trim());
                            if (m.Groups[1].Captures[0].Value.Contains('-'))
                            {
                                _deleted = true;
                            }
                            if (!_deleted)
                            {
                                ++_nodePos;
                                if (_nodePos >= _nodes.Count)
                                {
                                    _nodes.Add(new Node(string.Format(s_sgNameFormat, _sgNum), 0));
                                }
                                else
                                {
                                    CheckNodesOrder();
                                }
                                _nodeOff = _nodePos;
                                foreach (Capture c in m.Groups[3].Captures.Cast<Capture>())
                                {
                                    _nodes.Insert(++_nodeOff, new Node(c.Value.Trim(), _sgNum));
                                }
                            }
                            if (m.Groups[4].Captures.Count > 0)
                            {
                                if (!_deleted)
                                {
                                    _nodes.Insert(++_nodeOff, new Node(m.Groups[4].Captures[0].Value.Trim(), _sgNum));
                                    if (_nodes[_nodePos].SgEnd.Count > 0)
                                    {
                                        _nodes[_nodeOff].SgEnd.AddRange(_nodes[_nodePos].SgEnd);
                                    }
                                    _nodes[_nodeOff].SgEnd.Add(_sgNum);
                                }
                                _stage = Stage.DESC;
                                _desc.Clear();
                            }
                            else
                            {
                                _stage = Stage.SG_REST;
                            }
                        }
                        else
                        {
                            m = s_reSegment.Match(line);
                            if (m.Success)
                            {
                                SegOrSgEvent();
                                _segment = m.Groups[2].Captures[0].Value.Trim();
                                _info = m.Groups[3].Captures[0].Value.Trim();
                                if (s_unt.Equals(_segment))
                                {
                                    _stage = Stage.OCCURS;
                                    _nodePos = 0;
                                    _sgNum = 0;
                                    OnEndMessage?.Invoke();
                                }
                                else
                                {
                                    if (m.Groups[1].Captures[0].Value.Contains('-'))
                                    {
                                        _deleted = true;
                                    }
                                    if (!_deleted && !s_unh.Equals(_segment))
                                    {
                                        _nodePos++;
                                        if (_nodePos >= _nodes.Count)
                                        {
                                            _nodes.Add(new Node(_segment, 0));
                                        }
                                        else
                                        {
                                            CheckNodesOrder();
                                        }
                                    }
                                    _stage = Stage.DESC;
                                    _desc.Clear();
                                }
                            }
                            else
                            {
                                _stage = Stage.DESC;
                                continue;
                            }
                        }
                    }
                    break;
                case Stage.DESC:
                    if (string.IsNullOrEmpty(line.Trim()))
                    {
                        _stage = Stage.SEGORSG;
                    }
                    else
                    {
                        _desc.Append(' ').Append(line.Trim());
                    }
                    break;
                case Stage.SG_REST:
                    m = s_reSGRest.Match(line);
                    if (m.Success)
                    {
                        if (!_deleted)
                        {
                            foreach (Capture c in m.Groups[1].Captures.Cast<Capture>())
                            {
                                _nodes.Insert(++_nodeOff, new Node(c.Value.Trim(), _sgNum));
                            }
                        }
                        if (m.Groups[2].Captures.Count > 0)
                        {
                            if (!_deleted)
                            {
                                _nodes.Insert(++_nodeOff, new Node(m.Groups[2].Captures[0].Value.Trim(), _sgNum));
                                if (_nodes[_nodePos].SgEnd.Count > 0)
                                {
                                    _nodes[_nodeOff].SgEnd.AddRange(_nodes[_nodePos].SgEnd);
                                }
                                _nodes[_nodeOff].SgEnd.Add(_sgNum);
                            }
                            _stage = Stage.DESC;
                            _desc.Clear();
                        }
                    }
                    break;
                case Stage.OCCURS:
                    if (!string.IsNullOrEmpty(line.Trim()))
                    {
                        if (_reOccSegment == null)
                        {
                            m = s_reHeaders.Match(line);
                            if (m.Success)
                            {
                                int pos = m.Groups[1].Captures[0].Index;
                                int tag = m.Groups[2].Captures[0].Index;
                                int name = m.Groups[3].Captures[0].Index;
                                int s = m.Groups[4].Captures[0].Index;
                                int r = m.Groups[5].Captures[0].Index;
                                _reOccSegment = new Regex(string.Format(s_reOccSegmentFormat, pos, tag - pos - 4));
                                _reOccSG = new Regex(string.Format(s_reOccSGFormat, pos, name - pos - 4));
                                _reS = new Regex(string.Format(s_reSFormat, s));
                                _reR = new Regex(string.Format(s_reRFormat, r));
                            }
                        }
                        else
                        {
                            bool found = false;
                            _segment = null!;
                            _sgNum = 0;
                            m = _reOccSegment.Match(line);
                            if (m.Success)
                            {
                                if (!m.Groups[1].Captures[0].Value.Trim().Contains('-'))
                                {
                                    _segment = m.Groups[2].Captures[0].Value.Trim();
                                    if (!s_unh.Equals(_segment) && !s_unt.Equals(_segment))
                                    {
                                        if (!_nodes[_nodePos].Name.Equals(_segment))
                                        {
                                            throw new Exception(string.Format(s_notEqual, _nodes[_nodePos].Name, _segment));
                                        }
                                        found = true;
                                        _sgNum = _nodes[_nodePos].SgNum;
                                    }
                                }
                                if (s_unt.Equals(_segment))
                                {
                                    _stage = Stage.DONE;
                                }
                            }
                            else
                            {
                                m = _reOccSG.Match(line);
                                if (m.Success)
                                {
                                    if (!m.Groups[1].Captures[0].Value.Trim().Contains('-'))
                                    {
                                        _sgNum = int.Parse(m.Groups[2].Captures[0].Value.Trim());
                                        string name = string.Format(s_sgNameFormat, _sgNum);
                                        if (!_nodes[_nodePos].Name.Equals(name))
                                        {
                                            throw new Exception(string.Format(s_notEqual, _nodes[_nodePos].Name, name));
                                        }
                                        found = true;
                                    }
                                }
                            }
                            if (found)
                            {
                                int min_occurs = 1;
                                int max_occurs = 1;
                                m = _reS.Match(line);
                                if (m.Success)
                                {
                                    if ("C".Equals(m.Groups[1].Captures[0].Value.Trim()))
                                    {
                                        min_occurs = 0;
                                    }
                                }
                                m = _reR.Match(line);
                                if (m.Success)
                                {
                                    max_occurs = int.Parse(m.Groups[1].Captures[0].Value.Trim());
                                }
                                OnOccurs?.Invoke(_sgNum, _segment!, min_occurs, max_occurs);
                                _nodePos++;
                            }
                        }
                    }
                    break;
            }
            break;
        }
    }

    private void CheckNodesOrder()
    {
        if (!_nodes[_nodePos].Name.Equals(_sgNum == -1 ? _segment : string.Format(s_sgNameFormat, _sgNum)))
        {
            throw new Exception(string.Format(s_notEqual, (_sgNum == -1 ? _segment : string.Format(s_sgNameFormat, _sgNum)), _nodes[_nodePos].Name));
        }
    }

    private void CheckEndSg()
    {
        for (int i = _nodes[_nodePos].SgEnd.Count - 1; i >= 0; i--)
        {
            OnEndSG?.Invoke(_nodes[_nodePos].SgEnd[i]);
            _sgStack.RemoveAt(_sgStack.Count - 1);
        }
    }

    private void SegOrSgEvent()
    {
        if (_sgNum != -1 || (_segment != null && !s_unh.Equals(_segment)))
        {
            if (!_deleted)
            {
                if (_sgNum != -1)
                {
                    _sgStack.Add(_sgNum);
                    OnBeginSG?.Invoke(_sgNum, _desc.ToString());
                }
                else if (_segment != null && !s_unh.Equals(_segment))
                {
                    OnSegment?.Invoke(_segment, _info, _desc.ToString(), _sgStack.Count > 0 ? _sgStack[^1] : 0);
                    CheckEndSg();
                }
            }
            _sgNum = -1;
            _segment = null!;
            _deleted = false;
        }
    }
}
