namespace Net.Leksi.Edifact;

internal enum Waiting
{
    None,
    Unknown,
    Message,
    Directory,
    Namespace,
    Proxy,
    TargetFolder,
    TmpFolder,
    ExternalUnzipCommandLineFormat,
    SchemaDocument,
    Output,
    PaddingLength,
    Help
}
