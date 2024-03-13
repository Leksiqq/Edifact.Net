using System.Xml.Schema;

namespace Net.Leksi.Edifact;

internal class Sequence
{
    private XmlSchemaSequence _sequence;
    private int _position = -1;
    private int _occurs = 0;
    private int _minOccurs = 0;
    private int _maxOccurs = 0;
    internal XmlSchemaParticle? Item
    {
        get
        {
            EnsureState();
            if (_position < _sequence.Items.Count)
            {
                return (XmlSchemaParticle)_sequence.Items[_position];
            }
            return null;
        }
    }
    internal int Occurs
    {
        get
        {
            EnsureState();
            return _occurs;
        }
    }
    internal int MinOccurs
    {
        get
        {
            EnsureState();
            return _minOccurs;
        }
    }
    internal int MaxOccurs
    {
        get
        {
            EnsureState();
            return _maxOccurs;
        }
    }
    internal string MaxOccursString
    {
        get
        {
            EnsureState();
            return _maxOccurs == -1 ? "unbounded" : _maxOccurs.ToString();
        }
    }
    internal SequenceState State
    {
        get
        {
            if (_position == -1)
            {
                return SequenceState.Reset;

            }
            if (_occurs < _minOccurs)
            {
                return SequenceState.ShouldOccur;
            }
            if (_maxOccurs == -1 || _occurs < _maxOccurs)
            {
                return SequenceState.CanOccur;
            }
            return SequenceState.CannotOccur;
        }
    }
    internal bool IsLast => _position == _sequence.Items.Count - 1;
    internal bool IsFirst => _position == 0;
    internal string Name { get; private init; }
    internal Sequence(string name, XmlSchemaSequence sequence)
    {
        Name = name;
        _sequence = sequence;
    }
    internal bool Move()
    {
        if (_position < _sequence.Items.Count - 1)
        {
            ++_position;
            _occurs = 0;
            _minOccurs = (int)((XmlSchemaParticle)_sequence.Items[_position]).MinOccurs;
            _maxOccurs = ((XmlSchemaParticle)_sequence.Items[_position]).MaxOccursString == "unbounded"
                ? -1 : (int)((XmlSchemaParticle)_sequence.Items[_position]).MaxOccurs;
            return true;
        }
        return false;
    }
    internal bool MoveIfNeed()
    {
        return State is SequenceState.CanOccur
            || State is SequenceState.ShouldOccur
            || (
                (
                    State is SequenceState.Reset
                    || State is SequenceState.CannotOccur
                )
                && Move()
            );
    }
    internal void Reset()
    {
        _position = -1;
    }
    internal bool IncrementOccurs()
    {
        EnsureState();
        if (_maxOccurs == -1 || _maxOccurs >= _occurs)
        {
            ++_occurs;
            return true;
        }
        return false;
    }
    private void EnsureState()
    {
        if (_position == -1)
        {
            throw new InvalidOperationException("TODO: Move() first.");
        }
    }
}
