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
            if (_position == -1)
            {
                throw new InvalidOperationException("TODO: Move() first.");
            }
            if (_position < _sequence.Items.Count)
            {
                return (XmlSchemaParticle)_sequence.Items[_position];
            }
            return null;
        }
    }
    internal int Occurs => _occurs;
    internal int MinOccurs => _minOccurs;
    internal int MaxOccurs => _maxOccurs;
    internal string MaxOccursString => _maxOccurs == -1 ? "unbounded" : _maxOccurs.ToString();
    internal Sequence(XmlSchemaSequence sequence)
    {
        _sequence = sequence;
    }
    internal bool Move()
    {
        ++_position;
        _occurs = 0;
        if(_position < _sequence.Items.Count)
        {
            _minOccurs = (int)((XmlSchemaParticle)_sequence.Items[_position]).MinOccurs;
            _maxOccurs = ((XmlSchemaParticle)_sequence.Items[_position]).MaxOccursString == "unbounded"
                ? -1 : (int)((XmlSchemaParticle)_sequence.Items[_position]).MaxOccurs;
            return true;
        }
        return false;
    }
    internal void Reset()
    {
        _position = 0;
    }
    internal bool IncrementOccurs()
    {
        ++_occurs;
        return _maxOccurs == -1 || _maxOccurs >= _occurs;
    }
}
