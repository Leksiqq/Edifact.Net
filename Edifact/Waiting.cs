﻿namespace Net.Leksi.Edifact;

internal enum Waiting
{
    None,
    Unknown,
    Message,
    Directories,
    Namespace,
    Proxy,
    TargetFolder,
    TmpFolder,
    ExternalUnzipCommandLineFormat,
    SchemaDocument,
    OutputUri,
    Width,
    Help,
    ConnectionTimeout
}
