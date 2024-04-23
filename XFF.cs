using System.IO.Compression;
using System.Text;
using System.Text.Json;

public struct Symbol
{
    public enum Flags
    {
        NoneType = 0x0,
        Object = 0x1,
        Function = 0x2,
        Section = 0x3,
        External = 0x10
    };

    public enum Binding { Local = 0, Global = 1, Weak = 2 };

    public int nameOffset;
    public int offsetAddress;
    public int length;
    public Flags flags; // 0x17 seems to be .rodata and 0x18 is code(?). 0x10 is unallocated? 0x11 is variable 0x12 is code
    public ushort section;

    // Added by me
    public string name { get; set; }

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"Symbol: {name}");
        sb.AppendLine($"\tName Offset:      {nameOffset:X}");
        sb.AppendLine($"\tOffset Address:   {offsetAddress:X}");
        sb.AppendLine($"\tLength:           {length:X}");
        sb.AppendLine($"\tFlags:            {flags}");
        sb.AppendLine($"\tSection:          {section:X}");
        return sb.ToString();
    }
}

public struct SectionHeader
{
    public string Name { get; set; }
    public int Padding { get; set; }
    public int MemoryAddress { get; set; }
    public int Length { get; set; }
    public int Alignment { get; set; }
    public ELF.SectionHeader.SH_Type Type { get; set; }
    public int Unk1 { get; set; }
    public int Unk2 { get; set; }
    public int FileOffset { get; set; }

    public byte[] Data { get; set; }

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"Name:            {Name}");
        sb.AppendLine($"\tPadding?:        {Padding:X}");
        sb.AppendLine($"\tMemory Address:  {MemoryAddress:X}");
        sb.AppendLine($"\tLength:          {Length:X}");
        sb.AppendLine($"\tAlignment:       {Alignment:X}");
        sb.AppendLine($"\tType:            {Type}");
        sb.AppendLine($"\tUnk1:            {Unk1:X}");
        sb.AppendLine($"\tUnk2:            {Unk2:X}");
        sb.AppendLine($"\tFile Offset:     {FileOffset:X}");
        return sb.ToString();
    }
}

public struct Relocation
{
    public int offset;
    public int packedSymbolIndex; // First byte is the relocation type, the last 3 bytes are the symbol index 

    public int SymbolIndex => packedSymbolIndex >> 8;
    public int Type => packedSymbolIndex & 0xff;

    // Stuff I added
    public int fileLocation;

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"Offset:       {offset}");
        sb.AppendLine($"Symbol Index: {packedSymbolIndex >> 8}");
        sb.AppendLine($"Type:         {packedSymbolIndex & 0xff}");
        return sb.ToString();
    }
}

public struct RelocationHeader
{
    public int type;
    public int relocationCount;
    public int sectionIndex;
    public int relocationPointer;
    public int virtmemPtr;
    public int fileOffset;
    public int length;

    public Relocation[] relocations;
}

public struct XFF
{
    public XFF(string path)
    {
        byte[] data = File.ReadAllBytes(path);
        using (MemoryStream ms = new MemoryStream(data))
        {
            Read(ms);
        }
    }

    public XFF(Stream stream)
    {
        Read(stream);
    }

    public Symbol[] Symbols { get; set; }
    public SectionHeader[] SectionHeaders { get; set; }
    public RelocationHeader[] RelocationHeaders { get; set; }
    public int[] SymbolLocations { get; set; }
    public byte[] Data { get; private set; }

    public int bssSectionIndex { get; set; }

    int magic;
    int unk_1;
    int unk_2;
    int unk_sections;
    int unk_3;
    int length;
    int endOffset;
    int stringCount;
    int VmemUnkCount3;
    int symbolStrTabVMem;
    int sectionCount;
    int sectionNameOffsetVMem;
    int sectionStrTabVmem;
    int unkCount;
    int stringOffsetPtr;
    int symbolPtr;

    public void Read(Stream stream)
    {
        var startPos = stream.Position;
        BinaryReader br = new BinaryReader(stream);

        magic = br.ReadInt32();
        if (magic != 0x32666678)
        {
            Console.WriteLine("Not an xff file");
            return;
        }

        unk_1 = br.ReadInt32();
        unk_2 = br.ReadInt32();
        unk_sections = br.ReadInt32();

        // 0x10
        unk_3 = br.ReadInt32();
        length = br.ReadInt32();
        endOffset = br.ReadInt32();
        stringCount = br.ReadInt32();

        // 0x20
        VmemUnkCount3 = br.ReadInt32();
        int symbolCount = br.ReadInt32();
        br.BaseStream.Position += 4; // a pointer to the symbol array. This just got converted to the 'Symbols' array
        symbolStrTabVMem = br.ReadInt32();

        // 0x30
        int sectionHeaderPtr = br.ReadInt32();
        int symbolToAddressListPtr = br.ReadInt32();
        int relocationHeaderCount = br.ReadInt32();
        int relocationHeaderPtr = br.ReadInt32();

        // 0x40
        sectionCount = br.ReadInt32();
        sectionNameOffsetVMem = br.ReadInt32();
        sectionStrTabVmem = br.ReadInt32();
        unkCount = br.ReadInt32();

        // 0x50
        stringOffsetPtr = br.ReadInt32();
        symbolPtr = br.ReadInt32();
        int stringTablePtr = br.ReadInt32();
        int sectionHeadersPtr = br.ReadInt32();

        // 0x60
        int symbols2 = br.ReadInt32();
        int relocationHeaderFileOffset = br.ReadInt32();
        int sectionNameOffsetsPtr = br.ReadInt32();
        int sectionStringTablePtr = br.ReadInt32();

        // Start translating this data into usable structures
        // Section Headers
        SectionHeaders = ReadSections(br, sectionHeadersPtr, sectionNameOffsetsPtr, sectionStringTablePtr);

        // Symbols
        Dictionary<int, string> symbolNames = ReadStringArray(br, stringTablePtr, symbolCount);
        SymbolLocations = ReadSymbolLocations(br, symbols2, symbolCount);
        Symbols = ReadSymbols(br, symbolPtr, symbolCount, SymbolLocations, symbolNames);


        // Relocations
        RelocationHeaders = ReadRelocationHeaders(br, relocationHeaderFileOffset, relocationHeaderCount);

        ExternalFunctionReferences = new Dictionary<int, Symbol>();
        int totalRelocations = 0;
        for (int i = 0; i < RelocationHeaders.Length; i++)
        {
            totalRelocations += RelocationHeaders[i].relocationCount;
            RelocationHeaders[i].relocations = ReadRelocations(br, RelocationHeaders[i]);
        }

        // Set certan section offsets
        MapFileOffsetToSection(".shstrtab", sectionStringTablePtr, br);
        MapFileOffsetToSection(".strtab", stringTablePtr, br);

        // Read all the data
        stream.Position = startPos;
        Data = br.ReadBytes(length);
    }

    private void MapFileOffsetToSection(string sectionName, int offset, BinaryReader br)
    {
        for (int i = 0; i < SectionHeaders.Length; i++)
        {
            if (SectionHeaders[i].Name == sectionName)
            {
                SectionHeaders[i].FileOffset = offset;

                // Re-read data
                br.BaseStream.Position = offset;
                SectionHeaders[i].Data = br.ReadBytes(SectionHeaders[i].Length);
                return;
            }
        }

        Console.WriteLine($"Failed to set section offset: {sectionName}");
    }

    Dictionary<int, string> ReadStringArray(BinaryReader br, int offset, int numStrings)
    {
        br.BaseStream.Position = offset;
        Dictionary<int, string> map = new Dictionary<int, string>(numStrings);
        for (int i = 0; i < numStrings; i++)
        {
            int position = (int)br.BaseStream.Position - offset;
            string text = ReadString(br);
            map.Add(position, text);
        }

        return map;
    }

    SectionHeader[] ReadSections(BinaryReader br, int sectionHeadersPtr, int sectionNameOffsetsPtr, int sectionNameTablePtr)
    {
        SectionHeader[] sections = new SectionHeader[sectionCount];
        string[] sectionNames = GetSectionNames(br, sectionNameOffsetsPtr, sectionNameTablePtr);

        br.BaseStream.Position = sectionHeadersPtr;
        for (int i = 0; i < sectionCount; i++)
        {
            sections[i] = new SectionHeader
            {
                Name = sectionNames[i],
            };
            sections[i].Padding = br.ReadInt32();
            sections[i].MemoryAddress = br.ReadInt32();
            sections[i].Length = br.ReadInt32();
            sections[i].Alignment = br.ReadInt32();
            sections[i].Type = (ELF.SectionHeader.SH_Type)br.ReadInt32();
            sections[i].Unk1 = br.ReadInt32();
            sections[i].Unk2 = br.ReadInt32();
            sections[i].FileOffset = br.ReadInt32();

            if (sections[i].Name == ".bss")
                bssSectionIndex = i;
        }

        // Read all the dat
        for (int i = 0; i < sectionCount; i++)
        {
            br.BaseStream.Position = sections[i].FileOffset;
            sections[i].Data = br.ReadBytes(sections[i].Length);
        }

        return sections;
    }

    Relocation[] ReadRelocations(BinaryReader br, RelocationHeader header)
    {
        br.BaseStream.Position = header.fileOffset;

        List<Relocation> relocations = new List<Relocation>(header.relocationCount);
        for (int i = 0; i < header.relocationCount; i++)
        {
            Relocation relocation = new Relocation
            {
                fileLocation = (int)br.BaseStream.Position,
                offset = br.ReadInt32(),
                packedSymbolIndex = br.ReadInt32(),
            };

            Symbol symbol = Symbols[relocation.packedSymbolIndex >> 8];

            relocations.Add(relocation);
        }

        return relocations.ToArray();
    }

    int externalSymbols;
    Symbol[] ReadSymbols(BinaryReader br, int offset, int symbolCount, int[] symbolLocations, Dictionary<int, string> names)
    {
        br.BaseStream.Position = offset;

        Symbol[] symbols = new Symbol[symbolCount];

        // Read all basedata
        for (int i = 0; i < symbolCount; i++)
        {
            Symbol symbol = new Symbol
            {
                nameOffset = br.ReadInt32(),
                offsetAddress = br.ReadInt32(),
                length = br.ReadInt32(),
                flags = (Symbol.Flags)br.ReadInt16(),
                section = br.ReadUInt16(),
            };

            symbol.name = names[symbol.nameOffset];

            if (symbol.flags == Symbol.Flags.Section && symbol.section < SectionHeaders.Length)
            {
                symbol.name = SectionHeaders[symbol.section].Name;
                symbol.offsetAddress = SectionHeaders[symbol.section].FileOffset;
            }

            symbols[i] = symbol;

        }

        return symbols;
    }

    int[] ReadSymbolLocations(BinaryReader br, int offset, int symbolCount)
    {
        br.BaseStream.Position = offset;
        int[] locations = new int[symbolCount];

        for (int i = 0; i < symbolCount; i++)
        {
            locations[i] = br.ReadInt32();
        }

        return locations;
    }

    RelocationHeader[] ReadRelocationHeaders(BinaryReader br, int offset, int count)
    {
        br.BaseStream.Position = offset;
        RelocationHeader[] relocationHeaders = new RelocationHeader[count];
        for (int i = 0; i < count; i++)
        {
            RelocationHeader header = new RelocationHeader
            {
                type = br.ReadInt32(),
                relocationCount = br.ReadInt32(),
                sectionIndex = br.ReadInt32(),
                relocationPointer = br.ReadInt32(),
                virtmemPtr = br.ReadInt32(),
                fileOffset = br.ReadInt32(),
                length = br.ReadInt32(),
            };

            relocationHeaders[i] = header;
        }

        return relocationHeaders;
    }

    public void PatchXFF(Stream outputStream, string mainELFPath) // Applys relocations
    {
        // ELF main;
        // using (FileStream fs = new FileStream(mainELFPath, FileMode.Open))
        //     main = new ELF(fs);
        // bw.BaseStream.Position = 0x300010;
        // bw.Write(main.TextSection.Data);

        BinaryWriter bw = new BinaryWriter(outputStream);
        BinaryReader br = new BinaryReader(outputStream);

        // Create the .bss section at the end of the file
        // Get the bss section
        int bssSectionIndex = -1;
        Console.WriteLine(SectionHeaders == null);
        for (int i = 0; i < SectionHeaders.Length; i++)
        {
            if (SectionHeaders[i].Name == ".bss")
            {
                bssSectionIndex = i;
                SectionHeaders[i].FileOffset = 0x200000;
                Console.WriteLine($"Found bss section at {SectionHeaders[i].FileOffset:X}");
                break;
            }
        }

        for (int i = 0; i < Symbols.Length; i++)
        {
            Symbol symbol = Symbols[i];
            if (symbol.flags == Symbol.Flags.Section && symbol.section == bssSectionIndex)
            {
                symbol.offsetAddress = 0x200000;
            }
        }

        if (bssSectionIndex == -1)
        {
            Console.WriteLine("No bss section found");
            return;
        }

        for (int i = 0; i < RelocationHeaders.Length; i++)
        {
            ApplyRelocation(bw, br, RelocationHeaders[i]);
        }

    }

    public Dictionary<int, Symbol> ExternalFunctionReferences;
    void ApplyRelocation(BinaryWriter bw, BinaryReader br, RelocationHeader header)
    {
        for (int i = 0; i < header.relocations.Length; i++)
        {
            bw.BaseStream.Position = header.relocations[i].offset + SectionHeaders[header.sectionIndex].FileOffset;

            int baseInstruction = br.ReadInt32();
            uint baseUpper = (uint)baseInstruction & 0xffff0000;
            uint baseLower = (uint)baseInstruction & 0xffff;
            br.BaseStream.Position -= 4;

            int packedData = header.relocations[i].packedSymbolIndex;
            int relocationType = packedData & 0xff;
            int symbolIndex = packedData >> 8;
            Symbol symbol = Symbols[symbolIndex];


            int symbolFileAddress = 0;

            if (symbol.section == 0xfff1) // CSPX_150.97 function
            {
                symbolFileAddress = SymbolLocations[symbolIndex];
                // We're just going to write a nop then add a comment to what it was meant to jump to
                // Code starts at 0x100010
                // We want to remap that to 0x300010 

                bw.Write((uint)symbolFileAddress + 0x300000);
            }
            else if (symbol.section < 0xff00) // Local function
                symbolFileAddress = symbol.offsetAddress + SectionHeaders[symbol.section].FileOffset;
            else if (symbol.section == 0) // Error
                Console.WriteLine("Fuck");

            if (symbolFileAddress == 0)
            {
                Console.WriteLine("Dont know where this symbol is for relocation");
                Console.WriteLine($"\tName:    {symbol.name}");
                Console.WriteLine($"\tSection: {symbol.section}");
                Console.WriteLine($"\tOffset:  {symbol.offsetAddress}");
                return;
            }

            switch (relocationType)
            {
                default:

                    if (symbol.name == "gcGlobalVar")
                        break;

                    Console.WriteLine("Unknown relocation type: " + relocationType);
                    Console.WriteLine($"\tSymbol Name:      {symbol.name}");
                    Console.WriteLine($"\tSymbol Section:   {SectionHeaders[symbol.section].Name}");
                    break;

                case 0:
                    break;

                case 2: // I think
                    bw.Write(symbolFileAddress + baseInstruction);
                    break;

                case 4:
                    bw.Write((int)((symbolFileAddress >> 2 & 0x3ffffff) + baseInstruction));
                    break;

                case 5:  // upper half, Probably implemented wrong
                         // bw.Write((int)((symbolFileAddress >> 16) + baseInstruction));
                    int nextIndex = i + 1;
                    int nextType = header.relocations[nextIndex].packedSymbolIndex & 0xff;
                    while (nextType == 0x5)
                    {
                        if (nextIndex >= header.relocations.Length)
                            break;

                        nextIndex++;
                        nextType = header.relocations[nextIndex].packedSymbolIndex & 0xff;
                    }
                    if ((header.relocations[nextIndex].packedSymbolIndex & 0xff) == 0x6)
                    {
                        // *realocationAddress =
                        //     instructionBase & 0xffff0000 |
                        //     ((int)((int)_symbolLocation + (int)sVar1 + instructionBase * 0x10000) >> 0xf) + 1 >> 1 &
                        //     0xffffU;
                        Relocation relocation = header.relocations[nextIndex];
                        uint targetAddress = (uint)(baseLower + (uint)symbolFileAddress);

                        if ((targetAddress & 0xffffu) > short.MaxValue)
                            targetAddress += 0x10000;

                        int targetWrite = (int)(baseUpper | (targetAddress >> 0x10 & 0xffff));
                        bw.Write(targetWrite);
                        // bw.Write(0xffffffff);

                    }
                    else
                    {
                        Console.WriteLine("Cant find low16 for hi16");
                        // LoaderSysPrintf("ld:\t\x1b[31mWarning! Can\'t find low16 for hi16(relid:%d).\x1b[m\n",
                        //                 relocationIndexCpy,virtmemCpy,6,(long)(int)relocations,(long)(int)symbols,
                        //                 relocationIndexCpy,virtmemCpy);
                    }


                    break;

                case 6:  // Lower half
                    // bw.Write((int)((symbolFileAddress & 0xffff) + baseInstruction));
                    uint lowTargetAddress = ((uint)symbolFileAddress + baseLower);
                    bw.Write((int)(baseUpper | (lowTargetAddress & 0xffff)));
                    break;
            }

        }
    }

    public void WriteSymbolsToText(string outputName)
    {
        StringBuilder sb = new StringBuilder(Symbols.Length * 100);
        int longestName = 0;
        foreach (var field in typeof(SectionHeader).GetFields())
        {
            if (field.Name.Length > longestName)
                longestName = field.Name.Length;
        }

        for (int i = 0; i < Symbols.Length; i++)
        {
            sb.Append($"[{i:X}] {Symbols[i].name}\n");

            foreach (var field in typeof(Symbol).GetFields())
            {
                string valueText = field.GetValue(Symbols[i]).ToString();

                if (field.FieldType != typeof(string))
                    valueText = $"{field.GetValue(Symbols[i]):X}";

                sb.AppendLine($"\t{field.Name.PadRight(longestName)}: 0x{valueText}");
            }
        }


        File.WriteAllText(outputName, sb.ToString());
    }

    public void WriteSCPSFunctionReferencesForGhidra(Stream stream, int fileOffset = 0)
    {
        BinaryWriter bw = new BinaryWriter(stream);

        foreach (var location in ExternalFunctionReferences.Keys)
        {
            Symbol symbol = ExternalFunctionReferences[location];
            bw.Write(Encoding.UTF8.GetBytes($"SCPS-{symbol.name};0x{location + fileOffset:X}\n"));
        }
    }

    string ReadString(BinaryReader br)
    {
        StringBuilder sb = new StringBuilder(16);
        char c = br.ReadChar();

        while (c != 0x0)
        {
            sb.Append(c);
            c = br.ReadChar();
        }

        return sb.ToString();
    }

    string[] GetSectionNames(BinaryReader br, int sectionNameOffsetsPtr, int sectionStringsOffset)
    {
        br.BaseStream.Position = sectionNameOffsetsPtr;
        int[] nameOffsets = new int[sectionCount];
        for (int i = 0; i < sectionCount; i++)
        {
            nameOffsets[i] = br.ReadInt32();
        }

        string[] names = new string[sectionCount];
        for (int i = 0; i < sectionCount; i++)
        {
            br.BaseStream.Position = sectionStringsOffset + nameOffsets[i];
            names[i] = ReadString(br);
        }

        return names;
    }

    public void WriteSections(string outputPath)
    {
        StringBuilder sb = new StringBuilder(100 * SectionHeaders.Length);
        for (int i = 0; i < SectionHeaders.Length; i++)
        {
            SectionHeader header = SectionHeaders[i];
            sb.AppendLine($"[{i:X}]            {header.Name}");
            sb.AppendLine($"\tType:            {header.Padding}");
            sb.AppendLine($"\tVirtual Address: {header.MemoryAddress:X}");
            sb.AppendLine($"\tLength:          {header.Length:X}");
            sb.AppendLine($"\tAlignment:       {header.Alignment:X}");
            sb.AppendLine($"\t_Type:           {header.Type}");
            sb.AppendLine($"\tUnk 1:           {header.Unk1:X}");
            sb.AppendLine($"\tUnk 2:           {header.Unk2:X}");
            sb.AppendLine($"\tFile Offset:     {header.FileOffset:X}");
            sb.AppendLine();
        }

        File.WriteAllText(outputPath, sb.ToString());
    }

    private void WriteRelocationHeader(StringBuilder sb, RelocationHeader header)
    {
        sb.AppendLine($"\tRelocation Count: {header.relocationCount}");
        sb.AppendLine($"\tType:             {header.type}");
        sb.AppendLine($"\tLength:           {header.length}");
        sb.AppendLine($"\tSection:          {header.sectionIndex}");


        for (int i = 0; i < header.relocations.Length; i++)
        {
            int symbolIndex = header.relocations[i].packedSymbolIndex >> 8;
            int relocationType = header.relocations[i].packedSymbolIndex & 0xff;
            Symbol symbol = Symbols[symbolIndex];
            sb.AppendLine($"\tRelocation {i}");
            sb.AppendLine($"\t\tReloc offset:            (0x{header.relocations[i].offset:X})");
            sb.AppendLine($"\t\tReloc Symbol Index:      (0x{header.relocations[i].packedSymbolIndex:X})");
            sb.AppendLine($"\t\tReloc Type:              (0x{relocationType:X})");
            sb.AppendLine($"\t\tReloc File Location:     (0x{header.relocations[i].fileLocation:X})");
            sb.AppendLine($"\t\tSymbol Name:             ({symbol.name})");
            sb.AppendLine($"\t\tSymbol Location:         (0x{symbol.offsetAddress + SectionHeaders[symbol.section].FileOffset:X})");
            sb.AppendLine($"\t\tSymbol Flags:            (0x{symbol.flags:X})");
            if (symbol.section < 0xff00)
                sb.AppendLine($"\t\tSymbol Section:          {SectionHeaders[symbol.section].Name}");
            else
                sb.AppendLine($"\t\tSymbol Section:          {symbol.section:X}");
        }
    }

    public void CreateSplits(Stream inputStream, string outputName)
    {
        // Order symbols by the start address
        List<Tuple<int, Symbol>> allSymbols = new List<Tuple<int, Symbol>>(Symbols.Length);
        for (int i = 0; i < Symbols.Length; i++)
        {
            int fileLocation = Symbols[i].offsetAddress + SectionHeaders[Symbols[i].section].FileOffset;
            allSymbols.Add(new Tuple<int, Symbol>(fileLocation, Symbols[i]));
        }

        // Get the .text section and some other data
        int lastAddr = 0;
        int totalData = 0;
        int totalText = 0;
        int targetDataAmount = 0;

        int textSectionIndex = -1;
        int textLength = 0;
        for (int i = 0; i < SectionHeaders.Length; i++)
        {
            targetDataAmount += SectionHeaders[i].Length;
            if (SectionHeaders[i].Name == ".text")
            {
                textLength = SectionHeaders[i].Length;
                textSectionIndex = i;
            }
        }

        // // Brute force literally every single instruction, check if it is a jal instruction, then find what function it references
        // inputStream.Position = SectionHeaders[textSectionIndex].FileOffset;
        // BinaryReader br = new BinaryReader(inputStream);
        // List<int> foundSymbols = new List<int>();
        // for (int i = 0; i < SectionHeaders[textSectionIndex].Length / 4; i++)
        // {
        //     int rawInstruction = br.ReadInt32();
        //     Instruction instruction = Dissasembler.GetInstruction(rawInstruction);

        //     if (instruction is not JumpInstruction)
        //         continue;

        //     if (instruction.Name != "jal")
        //         continue;

        //     JumpInstruction jump = (JumpInstruction)instruction;

        //     int targetAddress = SearchForUndefinedFunctions(instruction, new Symbol { section = (ushort)textSectionIndex, length = -1 }, foundSymbols);
        //     if (targetAddress == -1)
        //         continue;

        //     // Its new
        //     allSymbols.Add(new Tuple<int, Symbol>(targetAddress, new Symbol
        //     {
        //         name = $"FUNC_{targetAddress.ToString("X").PadLeft(8, '0')}",
        //         length = -1,
        //         section = (ushort)textSectionIndex,
        //         offsetAddress = targetAddress - SectionHeaders[textSectionIndex].FileOffset,
        //     }));

        //     Console.WriteLine("Found undefined symbol");
        // }

        // Order all symbols by where they occur
        Tuple<int, Symbol>[] orderedPairs = (from x in allSymbols orderby x.Item1 ascending select x).ToArray();

        // Try to calculate the newly found function lengths
        // for (int i = 0; i < orderedPairs.Length - 1; i++)
        // {
        //     int address = orderedPairs[i].Item1;
        //     Symbol s = orderedPairs[i].Item2;
        //     // Is a new function
        //     if (s.length == -1)
        //     {
        //         s.length = orderedPairs[i + 1].Item1 - address;
        //         Console.WriteLine($"Set undefined symbol length: {s.length:X} Address: {address:X} Next address: {orderedPairs[i + 1].Item1:X}");

        //         orderedPairs[i] = new Tuple<int, Symbol>(address, s);
        //     }
        // }

        // Get the symbol array
        Symbol[] ordered = (from x in orderedPairs select x.Item2).ToArray();

        // Get all relocations ordered by file offset
        List<Tuple<int, Relocation>> orderedRelocations = new List<Tuple<int, Relocation>>();
        for (int i = 0; i < RelocationHeaders.Length; i++)
        {
            for (int r = 0; r < RelocationHeaders[i].relocations.Length; r++)
            {
                Relocation relocation = RelocationHeaders[i].relocations[r];
                int offset = SectionHeaders[RelocationHeaders[i].sectionIndex].FileOffset + relocation.offset;

                orderedRelocations.Add(new Tuple<int, Relocation>(offset, relocation));
            }
        }

        orderedRelocations = (from x in orderedRelocations orderby x.Item1 select x).ToList();

        StringBuilder sb = new StringBuilder();
        List<Split> splits = new List<Split>();


        // Go through each relocation and add it to the split if applicable
        for (int i = 0; i < ordered.Length - 1; i++)
        {
            Symbol symbol = ordered[i];
            if (symbol.section == 0 || symbol.length == 0)
                continue;

            int fileLocation = ordered[i].offsetAddress + SectionHeaders[ordered[i].section].FileOffset;
            int distance = fileLocation - lastAddr;


            if (distance > 0x10)
            {
                splits.Add(new Split
                {
                    globalFileOffset = lastAddr,
                    length = distance,
                    relocations = new Relocation[0],
                    targetSymbol = new Symbol { name = $"UNDEFINED SECTION (0x{lastAddr:X} - 0x{fileLocation:X}) (Len: 0x{distance:X})" },
                });
                Console.WriteLine("Gap: " + distance);
                sb.AppendLine($"Gap: 0x{distance:X}");
            }
            totalData += symbol.length;

            if (symbol.section == textSectionIndex)
                totalText += symbol.length;

            sb.AppendLine($"{symbol.name}");
            string initText = (((int)symbol.flags & 0xf0) == 0x10) ? "Initialized" : "Uninitialized";
            sb.AppendLine($"\t{initText}");

            if (symbol.section == 0xfff1)
                sb.AppendLine("\tExternal");
            if (symbol.section < 0xff00)
                sb.AppendLine($"\tSection:   {SectionHeaders[symbol.section].Name}");
            sb.AppendLine($"\tStart:     0x{fileLocation:X}");
            sb.AppendLine($"\tLength:    0x{symbol.length:X}");
            sb.AppendLine($"\tEnd:       0x{fileLocation + symbol.length:X}");
            sb.AppendLine($"\tFlags:     {(Symbol.Flags)((int)symbol.flags & 0xf)}");

            // Check for any relocations on this symbol
            List<Relocation> relocations = new List<Relocation>();
            List<int> relocationsToInstruction = new List<int>();
            for (int r = 0; r < orderedRelocations.Count; r++)
            {
                int relocStart = orderedRelocations[r].Item1;
                Relocation reloc = orderedRelocations[r].Item2;
                // Ignore once after symbol

                if (relocStart >= fileLocation && relocStart < fileLocation + symbol.length)
                {
                    Symbol relocSym = Symbols[reloc.packedSymbolIndex >> 8];
                    sb.AppendLine($"\t\tReloc:                {relocSym.name}");
                    sb.AppendLine($"\t\tReloc Symbol Index:   {reloc.packedSymbolIndex >> 8:X}");
                    sb.AppendLine($"\t\tLocation In Function: 0x{relocStart - fileLocation:X}");
                    sb.AppendLine($"\t\tLocation In File: 0x{reloc.fileLocation:X}");
                    sb.AppendLine($"\t\tType: 0x{reloc.packedSymbolIndex & 0xff:X}");
                    sb.AppendLine();
                    relocations.Add(reloc);
                    relocationsToInstruction.Add((relocStart - fileLocation) / 4);
                }

                if (relocStart > fileLocation + symbol.length)
                    break;
            }

            splits.Add(new Split
            {
                globalFileOffset = fileLocation,
                length = symbol.length,
                targetSymbol = symbol,
                relocations = relocations.ToArray(),
                relocationToInstructionsIndex = relocationsToInstruction.ToArray(),
            });

            lastAddr = fileLocation + symbol.length;
        }
        float percentFound = (float)totalData / (float)targetDataAmount * 100;
        float textPercent = (float)totalText / textLength * 100;
        Console.WriteLine($"Found 0x{totalData:X} / 0x{targetDataAmount} ({percentFound}%) bytes of data and {textPercent}% of text");

        string data = JsonSerializer.Serialize(splits.ToArray(), typeof(Split[]), new JsonSerializerOptions
        {
            IncludeFields = true,

        });

        // File.WriteAllText($"{outputName}.txt", sb.ToString());
        File.WriteAllText($"{outputName}.json", data);
    }

    public void SplitSymbols(string splitPath, string userSymbolsPath, Stream input, string outputDirectory)
    {
        string json = File.ReadAllText(splitPath);
        Split[] splits = (Split[])JsonSerializer.Deserialize(json, typeof(Split[]), new JsonSerializerOptions { IncludeFields = true });

        if (splits == null)
            return;

        // Get the user defined symbols
        Symbol[] userSymbols = SymbolEncoding.Decode(userSymbolsPath);

        BinaryReader br = new BinaryReader(input);

        List<int> foundFunctions = new List<int>();

        if (!Directory.Exists(outputDirectory))
            Directory.CreateDirectory(outputDirectory);
        StringBuilder undefinedFunctions = new StringBuilder();

        for (int i = 0; i < splits.Length; i++)
        {
            Split split = splits[i];

            string outputName = split.targetSymbol.name;

            if (split.targetSymbol.name == "NO SYMBOL" || string.IsNullOrEmpty(split.targetSymbol.name))
                outputName = $"func_{split.globalFileOffset.ToString("X").PadLeft(8, '0')}";

            input.Position = split.globalFileOffset;

            uint[] data = new uint[split.length / 4];
            for (int a = 0; a < split.length / 4; a++)
            {
                data[a] = br.ReadUInt32();
            }

            StringBuilder debugBuilder = new StringBuilder();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(".section .text");
            sb.AppendLine();
            sb.AppendLine($"{split.targetSymbol.name}:");
            for (int a = 0; a < data.Length; a++)
            {
                Instruction instruction = Dissasembler.GetInstruction(data[a]);
                bool overriden = false;
                int relocationIndex = -1;
                for (int r = 0; r < split.relocations.Length; r++)
                {
                    if (split.relocationToInstructionsIndex[r] == a)
                    {
                        // sb.Append(Symbols[split.relocations[r].packedSymbolIndex >> 8].name);
                        // sb.Append("\t");
                        Symbol targetSymbol = Symbols[split.relocations[r].packedSymbolIndex >> 8];
                        // Console.WriteLine($"Patching symbol{split.relocations[r].packedSymbolIndex >> 8:X}");

                        if (TryApplySymbolToInstruction(instruction, userSymbols, ref targetSymbol, out string text))
                        {
                            sb.AppendLine(text);
                            debugBuilder.AppendLine($"Symbol Index: {split.relocations[r].packedSymbolIndex >> 8:X} Instruction Index {split.relocationToInstructionsIndex[r]}: {targetSymbol.name}; Section: ({SectionHeaders[targetSymbol.section].Name}, {targetSymbol.section})");
                            overriden = true;
                        }
                        else
                        {
                            Console.WriteLine("Failed patching symbol ");
                            debugBuilder.AppendLine($"Symbol Index: {split.relocations[r].packedSymbolIndex >> 8:X} Instruction Index {split.relocationToInstructionsIndex[r]}: {targetSymbol.name}; Section: ({SectionHeaders[targetSymbol.section].Name}, {targetSymbol.section})");
                            relocationIndex = r;
                        }

                        break;
                    }
                }

                if (!overriden)
                    sb.AppendLine($"\t{instruction}");

                SearchForUndefinedFunctions(instruction, split.targetSymbol, foundFunctions, undefinedFunctions);

                if (!overriden && relocationIndex != -1)
                {
                    Symbol targetSymbol = Symbols[split.relocations[relocationIndex].packedSymbolIndex >> 8];
                    sb.AppendLine($"\t\tRelocation Type:     {split.relocations[relocationIndex].packedSymbolIndex & 0xff:X}");
                    sb.AppendLine($"\t\tName:                {targetSymbol.name}");
                    sb.AppendLine($"\t\tSymbol Index:        {split.relocations[relocationIndex].packedSymbolIndex >> 8:X}");
                    sb.AppendLine($"\t\tSection:             {targetSymbol.section:X}");
                    sb.AppendLine($"\t\tSection Name:        {SectionHeaders[targetSymbol.section].Name:X}");
                    sb.AppendLine($"\t\tOffset:              {targetSymbol.offsetAddress:X}");
                    sb.AppendLine($"\t\tLocation:            {SectionHeaders[targetSymbol.section].FileOffset + targetSymbol.offsetAddress:X}");
                    sb.AppendLine($"\t\tFlags:               {targetSymbol.flags:X}");
                    sb.AppendLine($"\t\tName Offset:         {targetSymbol.nameOffset:X}");
                    sb.AppendLine($"\t\tLength:              {targetSymbol.length:X}");
                }
            }

            string outputPath = $"{outputDirectory}/{outputName}.txt";
            string relocationOutputPath = $"{outputDirectory}/{outputName}_relocations.txt";

            File.WriteAllText(relocationOutputPath, debugBuilder.ToString());
            File.WriteAllText(outputPath, sb.ToString());
        }

        File.WriteAllText("Undefined Functions.txt", undefinedFunctions.ToString());
    }

    private bool TryApplySymbolToInstruction(Instruction instruction, Symbol[] userSymbols, ref Symbol symbol, out string text)
    {
        text = null;

        bool unfoundSymbol = false;
        if (string.IsNullOrEmpty(symbol.name))
        {
            unfoundSymbol = true;
        }

        if (instruction is JumpInstruction)
        {
            JumpInstruction jump = (JumpInstruction)instruction;
            if (unfoundSymbol)
                if (!TryResolveUnfoundSymbol(userSymbols, (int)jump.jumpAddress << 2, symbol.section, out symbol))
                    return false;

            text = $"\t{instruction.Name} {symbol.name}";
            return true;
        }

        if (instruction is ImmediateInstruction)
        {
            ImmediateInstruction inst = (ImmediateInstruction)instruction;

            if (unfoundSymbol)
                if (!TryResolveUnfoundSymbol(userSymbols, inst.Immediate, symbol.section, out symbol))
                    return false;

            text = $"\t{inst.ToString(symbol.name)}";
            return true;
        }

        if (instruction is WC1Instruction)
        {
            WC1Instruction inst = (WC1Instruction)instruction;

            if (unfoundSymbol)
                if (!TryResolveUnfoundSymbol(userSymbols, (int)inst.Offset, symbol.section, out symbol))
                    return false;

            text = $"\t{inst.ToString(symbol.name)}";
            return true;
        }

        return false;
    }

    private bool TryResolveUnfoundSymbol(Symbol[] userSymbols, int offset, int section, out Symbol symbol)
    {

        for (int i = 0; i < Symbols.Length; i++)
        {
            symbol = Symbols[i];
            if (symbol.section == section && symbol.offsetAddress == offset && !string.IsNullOrEmpty(symbol.name))
                return true;
        }

        for (int i = 0; i < userSymbols.Length; i++)
        {
            symbol = userSymbols[i];
            if (symbol.section == section && symbol.offsetAddress == offset && !string.IsNullOrEmpty(symbol.name))
                return true;
        }

        symbol = new Symbol();
        return false;
    }

    private int SearchForUndefinedFunctions(Instruction instruction, Symbol symbol, List<int> foundFunctions, StringBuilder sb = null)
    {
        if (instruction is not JumpInstruction)
            return -1;

        if (instruction.Name != "jal")
            return -1;

        JumpInstruction jump = (JumpInstruction)instruction;
        int targetAddress = (int)(jump.jumpAddress << 2) + SectionHeaders[symbol.section].FileOffset;

        if (foundFunctions.Contains(targetAddress))
            return -1;

        for (int i = 0; i < Symbols.Length; i++)
        {
            if (SectionHeaders[Symbols[i].section].Name == ".bss")
            {
                // Console.WriteLine($"BSS Function at 0x{targetAddress:X}");
                continue;
            }
            int globalAddress = Symbols[i].offsetAddress + SectionHeaders[Symbols[i].section].FileOffset;
            if (globalAddress == targetAddress)
            {
                return -1;
            }
        }

        if (sb != null)
            sb.AppendLine($"Found function at 0x{targetAddress:X} referenced from {symbol.name}");

        foundFunctions.Add(targetAddress);

        return targetAddress;
    }

    public void ExportGhidraSymbols(string path)
    {
        StringBuilder sb = new StringBuilder(Symbols.Length * 50);
        sb.AppendLine(Symbols.Length.ToString());

        for (int i = 0; i < Symbols.Length; i++)
        {

            int sectionOffset; // = SectionHeaders[Symbols[i].section].FileOffset;
            if (Symbols[i].section < SectionHeaders.Length)
                sectionOffset = SectionHeaders[Symbols[i].section].FileOffset;
            else if (Symbols[i].section == 0xfff1)
                sectionOffset = SymbolLocations[i];
            else
            // if (Symbols[i].section >= SectionHeaders.Length)
            {
                Console.WriteLine($"Symbol section out of bounds: {Symbols[i].section}, {SectionHeaders.Length} {Symbols[i].section == 0xfff1}");
                continue;
            }

            if (Symbols[i].section == bssSectionIndex)
            {
                sectionOffset = 0x200000;
            }

            if (Symbols[i].section == 0)
            {
                sectionOffset = 0x300000;
            }

            sb.AppendLine($"{Symbols[i].name};0x{Symbols[i].offsetAddress + sectionOffset:X};{Symbols[i].section}");
        }

        File.WriteAllText(path, sb.ToString());
    }
}