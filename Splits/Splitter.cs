using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;

public class Splitter
{

    public static void WriteSplitsToFile(string path, Split[] splits, string[] sectionNames)
    {
        var orderedSplits = (from x in splits orderby x.Section ascending select x).ToArray();

        StringBuilder sb = new StringBuilder(splits.Length * 100);
        for (int i = 0; i < orderedSplits.Length; i++)
        {
            Split split = orderedSplits[i];
            sb.AppendLine(split.Name);
            sb.AppendLine($"\tStart:   0x{split.StartAddress:X}");
            if (split.End > 0)
                sb.AppendLine($"\tEnd:     0x{split.End:X}");
            sb.AppendLine($"\tSection: {sectionNames[split.Section]}");

            if (!string.IsNullOrEmpty(split.Type))
                sb.AppendLine($"\tType:    {split.Type}");
        }

        // Create the needed directory for the file
        FileInfo info = new FileInfo(path);
        info.Directory?.Create();

        // Then write the file
        File.WriteAllText(path, sb.ToString());
    }

    public static Split[]? LoadSplitsFromFile(string path, string[] sectionNames, out Dictionary<string, FunctionDefinition> functionDefinitions)
    {
        // Check if the splits exost
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            Debug.LogCritical($"Failed to get split file \"{path}\"");
            functionDefinitions = new Dictionary<string, FunctionDefinition>();
            return null;
        }

        // Load all the lines
        Debug.LogInfo($"Reading splits file {path}");
        string[] lines = File.ReadAllLines(path);

        // Create output variables
        List<Split> splits = [];
        functionDefinitions = [];

        // Go through each line of split file
        for (int i = 0; i < lines.Length;)
        {
            int oldIndex = i;

            // Read and save each split
            Split split = ReadSplit(lines, ref i, sectionNames, out FunctionDefinition? function);
            splits.Add(split);

            if (functionDefinitions.ContainsKey(split.Name))
            {
                Debug.LogWarn($"Skipping duplicate function name '{split.Name}'");
                continue;
            }

            if (function != null)
                functionDefinitions.Add(split.Name, function);

            if (oldIndex == i)
            {
                Debug.LogWarn("Failed to read split");
                break;
            }
        }

        return splits.ToArray();
    }

    private static Split ReadSplit(string[] lines, ref int lineIndex, string[] sectionNames, out FunctionDefinition? function)
    {
        function = null;
        Split split = new Split
        {
            functionDefinition = new FunctionDefinition(),
        };
        string line = lines[lineIndex];
        split.Name = "";

        // Load name
        while (string.IsNullOrEmpty(line.Trim()) || line.StartsWith("//"))
        {
            lineIndex++;
            line = lines[lineIndex];
        }

        // Default the split name to the current line
        split.Name = line;

        // Check if the split is a function
        if (line.Contains('(') && line.Contains(')'))
        {
            function = ReadFunctionDefinition(line);
            split.functionDefinition = function;
            Debug.LogDebug($"Read function {function.name} from splits");

            // Override the split name to the function name
            split.Name = function.name;
        }

        lineIndex++;

        while (true) // danger
        {
            if (string.IsNullOrEmpty(line.Trim()) || line.StartsWith("//"))
            {
                lineIndex++;
                continue;
            }


            if (lineIndex >= lines.Length)
                break;

            line = lines[lineIndex];
            if (!line.StartsWith('\t'))
                break;

            string[] data = line.Trim().Split(':');
            string name = data[0].Trim();
            string value = data[1].Trim();

            switch (name.ToLower())
            {
                case "start":
                    split.StartAddress = ParseInt(value);
                    break;
                case "length":
                    int newLength = ParseInt(value);
                    if (split.End > 0 && split.Length != newLength)
                    {
                        Debug.LogWarn($"Split {name} has conflicting length and endpoint");
                        split.End = split.StartAddress + newLength;
                    }
                    split.End = split.StartAddress + newLength;
                    break;
                case "section":

                    if (value.StartsWith("0x"))
                    {
                        split.Section = ParseInt(value);
                        break;
                    }

                    split.Section = -1;
                    for (int i = 0; i < sectionNames.Length; i++)
                    {
                        if (sectionNames[i] == value)
                        {
                            split.Section = i;
                            break;
                        }
                    }

                    if (split.Section == -1)
                    {
                        Debug.LogError("Failed to get section index");
                    }
                    break;
                case "type":
                    split.Type = value.Trim();
                    break;
                case "end":
                    newLength = split.StartAddress - ParseInt(value);
                    if (split.Length > 0 && split.Length != newLength)
                    {
                        Debug.LogError($"Split {name} has conflicting length and endpoint");
                    }
                    split.End = ParseInt(value);
                    break;
                case "generate":
                    split.functionDefinition.GenerateC = value.Trim() == "true";
                    break;
                case "compile":
                    split.functionDefinition.Compile = value.Trim() == "true";
                    break;
                case "link":
                    split.functionDefinition.Link = value.Trim() == "true";
                    break;
                case "includecontext":
                    split.functionDefinition.IncludeContext = value.Trim() == "true";
                    break;
                case "includes":
                    split.functionDefinition.Includes = value.Trim().Split(' ');
                    break;
                case "usenativepointers":

                    // This is a list of parameter indices that need to be converted to a native pointer
                    // Wildcard means all of them
                    if (value.Trim() == "*")
                    {
                        for (int i = 0; i < split.functionDefinition.parameters.Length; i++)
                        {
                            split.functionDefinition.parameters[i].ConvertToNativePointer = true;
                        }
                        break;
                    }

                    string[] indices = value.Trim().Split(',');
                    for (int i = 0; i < indices.Length; i++)
                    {
                        if (int.TryParse(indices[i].Trim(), out int index))
                        {
                            if (index > 0 && index < split.functionDefinition.parameters.Length)
                                split.functionDefinition.parameters[index].ConvertToNativePointer = true;
                        }
                    }

                    break;
            }

            lineIndex++;
        }

        return split;
    }

    private static FunctionDefinition ReadFunctionDefinition(string text)
    {
        // Its a function
        // int getHp(int baseHp, int maxHp)
        string[] textSegments = text.Split(' ');

        // Read the parameters 
        string parameterText = text.Split('(')[1].Trim().TrimEnd(')');

        FunctionParameter[] parameters = [];
        if (!string.IsNullOrEmpty(parameterText))
        {

            string[] parameterParts = parameterText.Split(',');
            parameters = new FunctionParameter[parameterParts.Length];

            for (int i = 0; i < parameterParts.Length; i++)
            {
                parameters[i] = new FunctionParameter
                {
                    type = parameterParts[i].Trim().Split(' ')[0],
                    name = parameterParts[i].Trim().Split(' ')[1],
                };
            }

        }
        Register returnRegister = new Register((int)Register._Register.v0);
        if (textSegments[0].ToLower().Trim() == "void")
            returnRegister = new Register(int.MaxValue);

        return new FunctionDefinition
        {
            name = textSegments[1].Split('(')[0],
            ReturnType = textSegments[0].Trim(),
            parameters = parameters,
            ReturnRegister = returnRegister,
            Compile = true,
            Link = true,
            GenerateC = true,
        };
    }

    public static int ParseInt(string data)
    {
        if (data.StartsWith("0x"))
        {
            string newData = data.TrimStart('0', 'x', 'X');
            if (newData == "")
                return 0;

            return int.Parse(newData, System.Globalization.NumberStyles.HexNumber);
        }

        return int.Parse(data);
    }
}

public class SplitCreator(IExecutableInfo executable)
{
    private IExecutableInfo Executable { get; set; } = executable;
    private ISectionHeader[] SectionHeaders { get; set; } = [];

    public Split[] CreateXFFSplits(XFF xff)
    {
        // Save the executable info
        SectionHeaders = new ISectionHeader[xff.SectionHeaders.Length];
        for (int i = 0; i < SectionHeaders.Length; i++)
        {
            SectionHeaders[i] = xff.SectionHeaders[i];
        }
        Executable = xff;

        List<Split> splits = new List<Split>();
        for (int i = 0; i < xff.SectionHeaders.Length; i++)
        {
            XFF.SectionHeader header = xff.SectionHeaders[i];
            if (header.Type == ISectionHeader.SH_Type.NULL || header.Type == ISectionHeader.SH_Type.NoBits)
                continue;

            splits.AddRange(SplitSection(xff.Symbols, header.Length, i));
        }

        return splits.ToArray();
    }

    private bool TryGetRelativeSymbolAddress(int memoryLocation, out int relativeAddress, out int section)
    {
        for (int i = 0; i < SectionHeaders.Length; i++)
        {
            var header = SectionHeaders[i];

            if (memoryLocation >= header.MemoryAddress && memoryLocation <= header.MemoryAddress + header.Length)
            {
                section = i;
                relativeAddress = memoryLocation - header.MemoryAddress;
                return true;
            }
        }

        relativeAddress = 0;
        section = -1;
        Debug.LogError($"Failed to create split for address {memoryLocation:X}");
        return false;
    }

    public Split[] CreateELFSplits(XFF[] xffs, ELF elf)
    {
        // Load elf info
        SectionHeaders = new ISectionHeader[elf.SectionHeaders.Length];
        for (int i = 0; i < SectionHeaders.Length; i++)
        {
            SectionHeaders[i] = elf.SectionHeaders[i];   
        }

        // Create main elf splits
        List<Symbol> symbols = new List<Symbol>();
        for (int i = 0; i < xffs.Length; i++)
        {
            XFF xff = xffs[i];

            for (int s = 0; s < xff.Symbols.Length; s++)
            {
                Symbol symbol = xff.Symbols[s];
                if (symbol.section != 0xfff1)
                    continue;

                int fileLocation = symbol.offsetAddress;

                // Relocate to the elf's section and offset
                // Or be lazy and just manually set it
                if (TryGetRelativeSymbolAddress(fileLocation, out int relativeAddress, out int section))
                {
                    symbol.offsetAddress = relativeAddress;
                    symbol.section = (ushort)section;
                    symbols.Add(symbol);
                }
                else
                {
                    Debug.LogError("Failed to get section and offset in main elf");
                }
            }
        }

        List<Split> splits = new List<Split>(symbols.Count);
        for (int i = 0; i < elf.SectionHeaders.Length; i++)
        {
            var header = elf.SectionHeaders[i];
            splits.AddRange(SplitSection(symbols.ToArray(), header.Length, i));
        }

        return splits.ToArray();
    }

    private Split[] SplitSection(Symbol[] symbols, int sectionLength, int sectionIndex)
    {
        // Order all symbols by their offsets
        var orderedSymbols = (from x in symbols orderby x.offsetAddress where x.section == sectionIndex select x).ToArray();

        int lastAddress = 0;

        List<Split> splits = new List<Split>();
        for (int i = 0; i < orderedSymbols.Length; i++)
        {
            Symbol symbol = orderedSymbols[i];

            if (symbol.flags == Symbol.Flags.Section)
                continue;

            int symbolAddress = symbol.offsetAddress;
            int gapSize = symbol.offsetAddress - lastAddress;

            if (gapSize > 0x4)
            {
                Debug.LogWarn("Found gap in section sizeof " + gapSize);
                AddSplit(splits, lastAddress, gapSize, sectionIndex);
            }

            AddSplit(splits, symbol.offsetAddress, symbol.length, sectionIndex, symbol.name);
            lastAddress = symbol.offsetAddress + symbol.length;
        }

        if (lastAddress < sectionLength)
            AddSplit(splits, lastAddress, sectionLength - lastAddress, sectionIndex);

        return splits.ToArray();
    }

    private void AddSplit(List<Split> splits, int startAddress, int length, int sectionIndex, string name = "")
    {
        if (name == "")
            name = $"Undefined_Gap_0x{startAddress:X}-0x{startAddress + length:X}";

        // Try to make split a function
        if (sectionIndex == Executable.TextSectionIndex)
            name = $"undefined {name}()";

        // Create and add split
        Split split = new Split
        {
            Name = name,
            StartAddress = startAddress,
            End = startAddress + length,
            Section = sectionIndex,
        };
        splits.Add(split);
    }

    public Split[] UpdateSplits(Split[] currentSplits, ELF elf)
    {
        List<Split> newSplits = new List<Split>();
        for (int i = 0; i < currentSplits.Length; i++)
        {
            Split split = currentSplits[i];
            if (split.Section == 1 && split.Length <= 4)
            {
                Debug.LogWarn($"Trashing split {split.Name} {(from x in currentSplits where x.Name != split.Name where x.StartAddress == split.StartAddress select x).Count()}");
                continue;
                // I doubt theres a function thats only 1 instruction long
                uint instData = BitConverter.ToUInt32(elf.SectionHeaders[1].Data.AsSpan(split.StartAddress, 4));
                Instruction instr = Dissassembler.GetInstruction(instData);
                if (instr.Name != "addiu")
                    continue;

                ImmediateInstruction imm = (ImmediateInstruction)instr;
                if ((int)imm.RS != (int)Register._Register.sp)
                    continue;

                if (imm.Immediate > 0)
                    continue;

                // Found something creating new data on stack
                // Find the next thing that removes the data
                int instructionLength = 0;
                while (true)
                {
                    instData = BitConverter.ToUInt32(elf.SectionHeaders[1].Data.AsSpan(split.StartAddress + instructionLength, 4));
                    instr = Dissassembler.GetInstruction(instData);
                    instructionLength += 4;

                    if (instr.Name != "addiu")
                        continue;

                    imm = (ImmediateInstruction)instr;
                    if ((int)imm.RS != (int)Register._Register.sp)
                        continue;

                    if (imm.Immediate > 0)
                    {
                        split.End = instructionLength + split.StartAddress;
                        break;
                    }
                }
            }

            // Add good splits to list
            if (!split.Name.ToLower().Contains("gap"))
            {
                Split newSplit = split;
                if ((newSplit.Length == 0 || newSplit.End == 0) && newSplit.Type == "int")
                    newSplit.End = newSplit.StartAddress + 4;

                newSplits.Add(newSplit);
                continue;
            }

            for (int j = 0; j < split.Length / 4; j++)
            {
                newSplits.Add(new Split
                {
                    Section = split.Section,
                    StartAddress = split.StartAddress + j * 4,
                    End = split.StartAddress + j * 4 + 4,
                    Type = "int",
                    // length = 0x4,
                    Name = $"Section_0x{split.Section}_field_0x{split.StartAddress + j * 0x4:X}"
                });
            }


            // Leftover bytes
            for (int j = 0; j < split.Length % 4; j++)
            {
                newSplits.Add(new Split
                {
                    Section = split.Section,
                    StartAddress = split.StartAddress + split.Length / 4 * 4 + j,
                    End = split.StartAddress + split.Length / 4 * 4 + j + 1,
                    // length = 0x4,
                    Type = "char",
                    Name = $"Section_0x{split.Section}_field_0x{split.StartAddress + split.Length / 4 * 4 + j:X}"
                });
            }
        }

        // Return new splits
        return newSplits.ToArray();
    }

    public Split[] MergeSplits(Split[] a, Split[] b)
    {
        List<Split> newSplits = new List<Split>();


        // Trash all bad splits
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i].Length <= 4)
                continue;

            newSplits.Add(a[i]);
        }


        for (int i = 0; i < b.Length; i++)
        {
            bool found = false;
            if (b[i].Length <= 4)
                continue;


            for (int j = 0; j < newSplits.Count; j++)
            {
                if (newSplits[j].StartAddress == b[i].StartAddress)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
                newSplits.Add(b[i]);

        }

        // Find all gaps in new splits
        var ordered = (from x in newSplits orderby x.StartAddress ascending select x).ToArray();

        int lastAddress = 0;
        for (int i = 0; i < ordered.Length; i++)
        {
            if (ordered[i].Section != 1)
                continue;

            int gap = ordered[i].StartAddress - lastAddress;

            if (gap > 0x8)
            {
                Debug.LogWarn($"Found gap size of 0x{gap:X} after 0x{lastAddress:X}");
            }

            lastAddress = ordered[i].End;
        }

        return newSplits.ToArray();
    }

    public Split[] FromGhidra(string path)
    {
        string[] lines = File.ReadAllLines(path);
        Split[] splits = new Split[lines.Length];

        for (int i = 0; i < lines.Length; i++)
        {
            string[] segs = lines[i].Split('@');
            string name = segs[0].Trim();
            int offset = Splitter.ParseInt(segs[1].Trim()) - 0x100000;
            int length = int.Parse(segs[2].Trim());

            splits[i] = new Split
            {
                Name = name,
                StartAddress = offset,
                End = offset + length,
                Section = 0x1,
            };
        }

        return splits;
    }
}
