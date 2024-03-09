using System.Formats.Asn1;
using System.Text;

namespace Net.Leksi.Edifact;

internal class EdifactTokenizer
{
    private static object _beginEdifact = new();
    private static Dictionary<byte, object> s_bomLE = new()
    {
        {
            0x00,
            new Dictionary<byte, object>()
            {
                {
                    0x00,
                    BOM.Utf32Le
                }
            }
        },
    };
    private static Dictionary<byte, object> s_bomTree = new()
    {
        {
            0xEF,
            new Dictionary<byte, object>()
            {
                {
                    0xBB,
                    new Dictionary<byte, object>()
                    {
                        {
                            0xBF,
                            BOM.Utf8
                        }
                    }
                }
            }
        },
        {
            0xFE,
            new Dictionary<byte, object>()
            {
                {
                    0xFF,
                    BOM.Utf16Be
                }
            }
        },
        {
            0x00,
            new Dictionary<byte, object>()
            {
                {
                    0x00,
                    new Dictionary<byte, object>()
                    {
                        {
                            0xFE,
                            new Dictionary<byte, object>()
                            {
                                {
                                    0xFF,
                                    BOM.Utf32Be
                                }
                            }
                        }
                    }
                },
            }
        },
        {
            0xFF,
            new Dictionary<byte, object>()
            {
                {
                    0xFE,
                    s_bomLE
                },
            }
        },
        {
            0x2B,
            new Dictionary<byte, object>()
            {
                {
                    0x2F,
                    new Dictionary<byte, object>()
                    {
                        {
                            0x76,
                            new Dictionary<byte, object>()
                            {
                                {
                                    0x38,
                                    BOM.Utf7
                                },
                                {
                                    0x39,
                                    BOM.Utf7
                                },
                                {
                                    0x2B,
                                    BOM.Utf7
                                },
                                {
                                    0x2F,
                                    BOM.Utf7
                                },
                            }
                        }
                    }
                },
            }
        },
        {
            0xF7,
            new Dictionary<byte, object>()
            {
                {
                    0x64,
                    new Dictionary<byte, object>()
                    {
                        {
                            0x4C,
                            BOM.Utf1
                        }
                    }
                }
            }
        },
        {
            0xDD,
            new Dictionary<byte, object>()
            {
                {
                    0x73,
                    new Dictionary<byte, object>()
                    {
                        {
                            0x66,
                            new Dictionary<byte, object>()
                            {
                                {
                                    0x73,
                                    BOM.UtfEbcdic
                                }
                            }
                        }
                    }
                },
            }
        },
        {
            0x0E,
            new Dictionary<byte, object>()
            {
                {
                    0xFE,
                    new Dictionary<byte, object>()
                    {
                        {
                            0xFF,
                            BOM.Scsu
                        }
                    }
                }
            }
        },
        {
            0xFB,
            new Dictionary<byte, object>()
            {
                {
                    0xEE,
                    new Dictionary<byte, object>()
                    {
                        {
                            0x28,
                            BOM.Bocu1
                        }
                    }
                }
            }
        },
        {
            0x84,
            new Dictionary<byte, object>()
            {
                {
                    0x31,
                    new Dictionary<byte, object>()
                    {
                        {
                            0x95,
                            new Dictionary<byte, object>()
                            {
                                {
                                    0x33,
                                    BOM.Gb18030
                                }
                            }
                        }
                    }
                },
            }
        },
    };
    private static Dictionary<byte, object> s_begin = new()
    {
        {
            (byte)'U',
            new Dictionary<byte, object>()
            {
                {
                    (byte)'N',
                    new Dictionary<byte, object>()
                    {
                        {
                            (byte)'A',
                            _beginEdifact
                        }
                    }
                }
            }
        }
    };
    private static HashSet<char> s_levelAChars = [
        'A','B','C','D','E','F','G','H','I','J','K','L','M','N','O','P','Q','R','S','T','U','V','W','X','Y','Z',
        '0','1','2','3','4','5','6','7','8','9',
        ' ','.',',','-','(',')','/','=','\'','+',':','?','!','"','%','&','*',';','<','>'
    ];
    private static HashSet<char> s_levelBChars = [
        'A','B','C','D','E','F','G','H','I','J','K','L','M','N','O','P','Q','R','S','T','U','V','W','X','Y','Z',
        'a','b','c','d','e','f','g','h','i','j','k','l','m','n','o','p','q','r','s','t','u','v','w','x','y','z',
        '0','1','2','3','4','5','6','7','8','9',
        ' ','.',',','-','(',')','/','=','\'','+',':','?','!','"','%','&','*',';','<','>'
    ];
    private char _segmentPartsSeparator = '+';
    private char _componentPartsSeparator = ':';
    private char _decimalMark = '.';
    private char _releaseCharacter = '?';
    private char _segmentTerminator = '\'';
    private bool _isInteractive = false;
    private char _syntaxLevel = '\0';
    private int _syntaxVersion = 0;
    private BOM? _bom = null;
    private int _line = 0;
    private int _col = 0;
    private int _newLine = 0;
    internal Encoding? Encoding {  get; set; }
    internal char DecimalMark => _decimalMark;
    internal bool IsInteractive => _isInteractive;
    internal bool IsStrict { get; set; } = true;
    internal int BufferLength { get; set; } = 2048;
    internal async IAsyncEnumerable<SegmentToken> Tokenize(Stream stream)
    {
        if (!ReadBOM(stream))
        {
            yield break;
        }
        _line = 1;
        _col = 0;
        ReadSeparators(stream);
        SegmentToken? token = ReadInterchangeHeaderStart(stream);

        if (Encoding is null)
        {
            ThrowUndefinedEncoding();
        }
        TextReader reader = new StreamReader(stream, Encoding!);
        bool escaped = false;
        StringBuilder sb = new();
        char[] buffer = new char[BufferLength];
        int n;
        while ((n = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            ++_col;
            for(int i = 0; i < n; ++i)
            {
                char ch = buffer[i];

                if (token is { } || !IsWhitespace(ch))
                {
                    if (
                        IsStrict
                        && (
                            (_syntaxLevel == 'A' && !s_levelAChars.Contains(ch))
                            || (_syntaxLevel == 'B' && !s_levelBChars.Contains(ch))
                        )
                    )
                    {
                        throw new Exception($"TODO: invalid char '{ch}' for syntax level {_syntaxLevel} at {_line}:{_col}");
                    }
                    token ??= new SegmentToken();
                    if (escaped)
                    {
                        if(
                            IsStrict
                            && (
                                ch != _componentPartsSeparator && ch != _decimalMark && ch != _segmentPartsSeparator 
                                && ch != _segmentTerminator && ch != _releaseCharacter
                            )
                        )
                        {
                            throw new Exception($"TODO: invalid escaping at {_line}:{_col - 1}");
                        }
                        sb.Append(ch);
                        escaped = false;
                    }
                    else
                    {
                        if (ch == _releaseCharacter)
                        {
                            escaped = true;
                        }
                        else if (ch == _segmentTerminator)
                        {
                            if (sb.Length > 0)
                            {
                                AddValue(token!, sb);
                            }
                            yield return token!;
                            token = null;
                        }
                        else if (ch == _segmentPartsSeparator)
                        {
                            if (sb.Length > 0)
                            {
                                AddValue(token, sb);
                            }
                            if (token.Elements is null)
                            {
                                token.Elements = [];
                            }
                            token.Elements!.Add(new ElementToken());
                        }
                        else if (ch == _componentPartsSeparator)
                        {
                            AddValue(token, sb);
                        }
                        else
                        {
                            sb.Append(ch);
                        }
                    }
                }

            }
        }
        if(sb.Length > 0)
        {
            throw new Exception("TODO: unclosed last segment found.");
        }

    }
    private void AddValue(SegmentToken token, StringBuilder sb)
    {
        if(token.Tag is null)
        {
            token.Tag = sb.ToString();
        }
        else if(token.Elements is null)
        {
            int nesting = 0;
            if (sb.Length > 0 && !int.TryParse(sb.ToString(), out nesting))
            {
                ThrowUnexpectedExplicitNestingValue(sb.ToString(), _line, _col);
            }
            token.ExplcitNestingIndication ??= [];
            token.ExplcitNestingIndication.Add(nesting);
        }
        else
        {
            if (token.Elements!.Count == 0)
            {
                token.Elements.Add(new ElementToken());
            }
            token.Elements.Last().Components ??= [];
            token.Elements.Last().Components!.Add(sb.ToString());
        }
        sb.Clear();
    }
    private static void ThrowUnexpectedExplicitNestingValue(string v, int line, int col)
    {
        throw new Exception($"TODO: explicit indicator of nesting must be a number, but '{v}' received at {line}{col}.");
    }
    private static void ThrowUndefinedEncoding()
    {
        throw new Exception("TODO: Undefined encoding.");
    }
    private SegmentToken ReadInterchangeHeaderStart(Stream stream)
    {
        int b = SkipByteWhitespaces(stream);
        byte[] buf = [(byte)b, 0, 0, 0];
        if (stream.Read(buf, 1, 2) != 2)
        {
            ThrowUnexpectedEoF(_line, _col);
        }
        _col += 2;
        bool interchangeHeaderStarted = false;
        if (buf[0] == 'U' && (buf[1] == 'I' || buf[1] == 'N') && buf[2] == 'B')
        {
            interchangeHeaderStarted = true;
            if (buf[1] == 'I')
            {
                _isInteractive = true;
            }
        }
        if (!interchangeHeaderStarted)
        {
            throw new Exception($"TODO: \"UNB\" or \"UIB\" expected, but {(char)buf[0]}{(char)buf[1]}{(char)buf[2]} found at {_line}:{_col}.");
        }
        _col += 2;
        if ((char)(b = stream.ReadByte()) != _segmentPartsSeparator)
        {
            ThrowUnexpectedChar(b, $"'{_segmentPartsSeparator}'", _line, _col);
        }
        ++_col;
        if (stream.Read(buf, 0, 4) != 4)
        {
            ThrowUnexpectedEoF(_line, _col);
        }
        if (buf[0] == 'U' && buf[1] == 'N' && buf[2] == 'O')
        {
            _syntaxLevel = (char)buf[3];
            switch (_syntaxLevel)
            {
                case 'A' or 'B':
                    Encoding = Encoding.ASCII;
                    break;
                case 'C':
                    Encoding = Encoding.Latin1;
                    break;
                case 'D':
                    Encoding = Encoding.GetEncoding("ISO-8859-2");
                    break;
                case 'E':
                    Encoding = Encoding.GetEncoding("ISO-8859-5");
                    break;
                case 'F':
                    Encoding = Encoding.GetEncoding("ISO-8859-7");
                    break;
                case 'G':
                    Encoding = Encoding.GetEncoding("ISO-8859-3");
                    break;
                case 'H':
                    Encoding = Encoding.GetEncoding("ISO-8859-4");
                    break;
                case 'I':
                    Encoding = Encoding.GetEncoding("ISO-8859-6");
                    break;
                case 'J':
                    Encoding = Encoding.GetEncoding("ISO-8859-8");
                    break;
                case 'K':
                    Encoding = Encoding.GetEncoding("ISO-8859-9");
                    break;
                case 'X' or 'Y':
                    throw new NotSupportedException();
            }
        }
        else
        {
            throw new Exception($"TODO: \"UNOx\" expected, but {(char)buf[0]}{(char)buf[1]}{(char)buf[2]}{(char)buf[3]} found at {_line}:{_col}.");
        }
        _col += 4;
        b = stream.ReadByte();
        if (b == -1)
        {
            ThrowUnexpectedEoF(_line, _col);
        }
        if (b != _componentPartsSeparator)
        {
            ThrowUnexpectedChar(b, $"'{_componentPartsSeparator}'", _line, _col);
        }
        ++_col;
        b = stream.ReadByte();
        if (b == -1)
        {
            ThrowUnexpectedEoF(_line, _col);
        }
        if (b < '1' || b > '4')
        {
            ThrowUnexpectedChar(b, "'1'-'4'", _line, _col);
        }
        ++_col;
        _syntaxVersion = (char)b - '0';
        SegmentToken token = new()
        {
            Tag = _isInteractive ? "UIB" : "UNB",
            Elements = [
                new ElementToken 
                {
                    Components = [Encoding.ASCII.GetString(buf), _syntaxVersion.ToString()]
                }    
            ],
        };
        return token;
    }
    private static void ThrowUnexpectedEoF(int line, int col)
    {
        throw new Exception($"TODO: unexpected end of file at {line}:{col}.");
    }
    private static void ThrowUnexpectedChar(int b, string expected, int line, int col)
    {
        throw new Exception($"TODO: {expected} expected, but '{(char)b}' found at {line}:{col}.");
    }
    private void ReadSeparators(Stream stream)
    {
        _componentPartsSeparator = (char)stream.ReadByte();
        _segmentPartsSeparator = (char)stream.ReadByte();
        _decimalMark = (char)stream.ReadByte();
        _releaseCharacter = (char)stream.ReadByte();
        _ = (char)stream.ReadByte();
        _segmentTerminator = (char)stream.ReadByte();
    }
    private bool ReadBOM(Stream stream)
    {
        Dictionary<byte, object>? node = null;
        while (true)
        {
            int b = stream.ReadByte();
            if (b == -1)
            {
                return false;
            }
            if (stream.Position == 1)
            {
                if (s_bomTree.TryGetValue((byte)b, out _))
                {
                    node = s_bomTree;
                }
                else if (s_begin.TryGetValue((byte)b, out _))
                {
                    node = s_begin;
                }
                if (node is null)
                {
                    b = SkipByteWhitespaces(stream);
                    if (s_begin.TryGetValue((byte)b, out _))
                    {
                        node = s_begin;
                    }
                    else
                    {
                        throw new Exception($"TODO: \"UNA\" expected, but '{(char)b}' received at {stream.Position}.");
                    }
                }
            }
            if (node!.TryGetValue((byte)b, out object? obj))
            {
                if (obj is BOM bom)
                {
                    _bom = bom;
                    b = SkipByteWhitespaces(stream);
                    if (s_begin.TryGetValue((byte)b, out _))
                    {
                        node = s_begin;
                    }
                    else
                    {
                        ThrowUnexpectedChar(b, "'U'", _line, _col - 1);
                    }
                }
                else if (obj == _beginEdifact)
                {
                    break;
                }
                else if (obj == s_bomLE)
                {
                    node = s_bomLE;
                }
                else if (obj is Dictionary<byte, object> nextNode)
                {
                    node = nextNode;
                }
            }
            else if(node == s_bomLE)
            {
                _bom = BOM.Utf16Le;
                b = SkipByteWhitespaces(stream);
                if (s_begin.TryGetValue((byte)b, out _))
                {
                    node = s_begin;
                }
                else
                {
                    ThrowUnexpectedChar(b, "'U'", _line, _col - 1);
                }
            }
            else
            {
                throw new Exception($"TODO: unexpected 0x{b:X} received at {stream.Position}.");
            }
        }
        return true;
    }
    private bool IsWhitespace(int b)
    {
        bool result = false;
        if(b == (char)'\r' || b == (char)'\n')
        {
            result = true;
            if(_newLine == 0)
            {
                _newLine = b;
            }
            if(b == _newLine)
            {
                ++_line;
                _col = 0;
            }
        }
        else
        {
            if(b == (char)' ' || b == (char)'\t')
            {
                result = true;
            }
            ++_col;
        }
        return result;
    }
    private int SkipByteWhitespaces(Stream stream)
    {
        int b;
        do
        {
            b = stream.ReadByte();
        }
        while (IsWhitespace(b));
        return b;
    }
    private int SkipCharWhitespaces(TextReader reader)
    {
        int b;
        do
        {
            b = reader.Read();
        }
        while (IsWhitespace(b));
        return b;
    }
}
