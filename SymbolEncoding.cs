using System.Reflection.Metadata;
using System.Text;

public static class SymbolEncoding
{
    public static string Encode(Symbol[] symbols)
    {
        StringBuilder sb = new StringBuilder(100 * symbols.Length);
        for (int i = 0; i < symbols.Length; i++)
        {
            Symbol s = symbols[i];
            sb.AppendLine(s.name);
            sb.AppendLine($"\tSection: 0x{s.section:X}");
            sb.AppendLine($"\tOffset:  0x{s.offsetAddress:X}");
            sb.AppendLine($"\tLength:  0x{s.length:X}");
            sb.AppendLine($"\tIndex:   0x{i:X}");
        }

        return sb.ToString();
    }

    public static Symbol[] Decode(string path)
    {
        string[] lines = File.ReadAllLines(path);
        List<Symbol> symbols = new List<Symbol>();

        Symbol currentSymbol = new Symbol();
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];

            // Skip empty lines
            if (string.IsNullOrEmpty(line.Trim()))
                continue;

            // If new symbol
            if (!line.StartsWith('\t'))
            {
                if (!string.IsNullOrEmpty(currentSymbol.name))
                    symbols.Add(currentSymbol);

                currentSymbol = new Symbol
                {
                    name = line.Trim()
                };
                continue;
            }

            // if parameter
            if (line.StartsWith('\t'))
            {
                // Read parameter
                string[] segments = line.Trim().Split(':');
                string name = segments[0].Trim().ToLower();
                string value = segments[1].Trim().ToLower().Replace("0x", "");

                switch (name)
                {
                    case "section": currentSymbol.section = ushort.Parse(value, System.Globalization.NumberStyles.HexNumber); break;
                    case "offset": currentSymbol.offsetAddress = int.Parse(value, System.Globalization.NumberStyles.HexNumber); break;
                    case "length": currentSymbol.length = int.Parse(value, System.Globalization.NumberStyles.HexNumber); break;

                    default:
                        Console.WriteLine($"Unknown symbol value: {name}");
                        break;
                }

                continue;
            }
        }
        if (!string.IsNullOrEmpty(currentSymbol.name))
            symbols.Add(currentSymbol);

        return symbols.ToArray();
    }
}