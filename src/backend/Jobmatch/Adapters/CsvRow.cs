namespace Jobmatch.Adapters;

internal static class CsvRow
{
    // Parses CSV content as a sequence of records. Honors quoted fields that
    // span multiple lines (\n or \r\n inside quotes is part of the field).
    // Doubled quotes ("") inside a quoted field decode to a literal ".
    public static IEnumerable<List<string>> ParseCsvRecords(string content)
    {
        var record = new List<string>();
        var field = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < content.Length; i++)
        {
            var ch = content[i];
            if (inQuotes)
            {
                if (ch == '"' && i + 1 < content.Length && content[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else if (ch == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    field.Append(ch);
                }
            }
            else if (ch == '"' && field.Length == 0)
            {
                inQuotes = true;
            }
            else if (ch == ',')
            {
                record.Add(field.ToString());
                field.Clear();
            }
            else if (ch == '\n' || ch == '\r')
            {
                if (ch == '\r' && i + 1 < content.Length && content[i + 1] == '\n') i++;
                record.Add(field.ToString());
                field.Clear();
                yield return record;
                record = new List<string>();
            }
            else
            {
                field.Append(ch);
            }
        }

        if (field.Length > 0 || record.Count > 0)
        {
            record.Add(field.ToString());
            yield return record;
        }
    }
}
