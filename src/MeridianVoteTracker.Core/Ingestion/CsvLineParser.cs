using System.Text;

namespace MeridianVoteTracker.Core.Ingestion;

/// <summary>
/// Splits one CSV line into fields (RFC 4180 quoting: a field in double quotes may contain commas,
/// and "" inside quotes is a literal quote). Embedded line breaks inside quotes are not supported;
/// a row broken that way fails the column-count check and is reported as invalid.
/// </summary>
public static class CsvLineParser
{
    public static string[] Parse(string line)
    {
        if (line.IndexOf('"') < 0)
        {
            return line.Split(',');
        }

        var fields = new List<string>(8);
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c != '"')
                {
                    current.Append(c);
                }
                else if (i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = false;
                }
            }
            else if (c == '"' && current.Length == 0)
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return fields.ToArray();
    }
}
