﻿using System.Xml;

namespace Net.Leksi.Edifact;

public class MessageEventArgs : EdifactEventArgs
{
    public MessageHeader Header { get; internal set; } = null!;
    public Stream? Stream { get; set; }
}
