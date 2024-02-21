namespace Net.Leksi.Edifact;

internal class BaseEventArgs : EventArgs
{
    internal int ControlCount { get; set; }
    internal string? ControlReference { get; set; }
    internal DateTime PreparationDateTime { get; set; }
    internal List<string>? TmpMessageFiles { get; set; }
    internal List<ParseError>? Errors { get; set; }
}
