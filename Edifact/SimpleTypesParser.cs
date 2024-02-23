
using System.Text.RegularExpressions;

namespace Net.Leksi.Edifact;

internal class SimpleTypesParser
{
    private static readonly Regex s_reName = new("^\\s*(?<change>[+*|#X-])?\\s+(?<code>\\d{4})\\s+(?<name>.+?)(?:\\[B|C\\])?\\s*$");
    internal async IAsyncEnumerable<SimpleType> ParseAsync(TextReader reader)
    {
        while(await reader.ReadLineAsync() is string line)
        {
            Match m = s_reName.Match(line);
            if(m.Success)
            {
                SimpleType type = new() 
                {
                    Code = m.Groups["code"].Value,
                    Name = m.Groups["name"].Value,
                    Change = m.Groups["change"].Value,
                };
                yield return type;
            }
        }
    }
}