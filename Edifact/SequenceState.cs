namespace Net.Leksi.Edifact;

internal enum SequenceState
{
    Reset,
    ShouldOccur,
    CanOccur,
    CannotOccur
}
