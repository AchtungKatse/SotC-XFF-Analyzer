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
public struct Relocation
{
    public enum RelocationType
    {
        Null = 0x0,
        Jump = 0x2,
        Somethign = 0x4,
        High16 = 0x5,
        Low16 = 0x6,
    }

    public int offset;
    public int packedSymbolIndex; // First byte is the relocation type, the last 3 bytes are the symbol index 

    public int SplitIndex => packedSymbolIndex >> 8;
    public RelocationType Type => (RelocationType)(packedSymbolIndex & 0xff);

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

public class XFF : Executable<XFF.SectionHeader>
{
    public struct SectionHeader : ISectionHeader
    {
        public string Name { get; set; }
        public int Padding { get; set; }
        public int MemoryAddress { get; set; }
        public int Length { get; set; }
        public int Alignment { get; set; }
        public ISectionHeader.SH_Type Type { get; set; }
        public int Unk1 { get; set; }
        public int Unk2 { get; set; }
        public int FileOffset { get; set; }

        public byte[] Data { get; set; }
        public int SectionIndex { get; set; }

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

    public XFF(string path)
    {
        FileInfo info = new FileInfo(path);
        Name = info.Name.Substring(0, info.Name.Length - info.Extension.Length); // Removes the .XFF extension from the name

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
        GetSectionNames(br, sectionNameOffsetsPtr, sectionNameTablePtr);

        br.BaseStream.Position = sectionHeadersPtr;
        for (int i = 0; i < sectionCount; i++)
        {
            sections[i] = new SectionHeader
            {
                Name = SectionNames[i],
                Padding = br.ReadInt32(),
                MemoryAddress = br.ReadInt32(),
                Length = br.ReadInt32(),
                Alignment = br.ReadInt32(),
                Type = (ISectionHeader.SH_Type)br.ReadInt32(),
                Unk1 = br.ReadInt32(),
                Unk2 = br.ReadInt32(),
                FileOffset = br.ReadInt32(),
                SectionIndex = i
            };

            if (sections[i].Name == ".bss")
                bssSectionIndex = i;

            if (sections[i].Name == ".text")
                TextSectionIndex = i;
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
    public Dictionary<int, Symbol> ExternalFunctionReferences;

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

    void GetSectionNames(BinaryReader br, int sectionNameOffsetsPtr, int sectionStringsOffset)
    {
        br.BaseStream.Position = sectionNameOffsetsPtr;
        int[] nameOffsets = new int[sectionCount];
        for (int i = 0; i < sectionCount; i++)
        {
            nameOffsets[i] = br.ReadInt32();
        }

        SectionNames = new string[sectionCount];
        for (int i = 0; i < sectionCount; i++)
        {
            br.BaseStream.Position = sectionStringsOffset + nameOffsets[i];
            SectionNames[i] = ReadString(br);
        }
    }

    public static bool IsAnXFF(string path)
    {
        using (FileStream fs = new FileStream(path, FileMode.Open))
        {
            BinaryReader br = new BinaryReader(fs);
            return br.ReadInt32() == 0x32666678;
        }
    }

}