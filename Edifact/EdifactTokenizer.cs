using System.Text;

namespace Net.Leksi.Edifact;

internal class EdifactTokenizer
{
    private static object _beginEdifact = new();
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
            0xFF,
            new Dictionary<byte, object>()
            {
                {
                    0xFE,
                    BOM.Utf16Le
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
                    new Dictionary<byte, object>()
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
                        }
                    }
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
    private char _segmentPartsSeparator = '+';
    private char _componentPartsSeparator = ':';
    private char _decimalMark = '.';
    private char _segmentTerminator = '\'';
    private char _releaseCharacter = '?';
    private Encoding _encoding = Encoding.ASCII;
    internal async IAsyncEnumerable<SegmentToken> Tokenize(Stream stream)
    {
        Dictionary<byte, object>? node = null;
        bool spaces = false;
        while (true)
        {
            int b = stream.ReadByte();
            if (b == -1)
            {
                yield break;
            }
            object? obj = null;
            if (stream.Position == 0)
            {
                if (s_bomTree.TryGetValue((byte)b, out _))
                {
                    node = s_bomTree;
                }
                else if (s_begin.TryGetValue((byte)b, out _))
                {
                    node = s_begin;
                }
                else if (b == ' ' || b == '\t' || b == '\n' || b == '\n')
                {
                    spaces = true;
                }
            }
            if (node is { } && node.TryGetValue((byte)b, out obj))
            {
                if(obj is BOM bom)
                {

                }
                else if (obj == _beginEdifact)
                {

                }
                else if(obj is Dictionary<byte, object> nextNode){
                    node = nextNode;
                }
            }
        }
        byte[] buffer = new byte[9];
        int n = await stream.ReadAsync(buffer, 0, 9);
        if (n == 9)
        {

        }
    }
}
