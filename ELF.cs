using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;

public struct ELF
{
    public struct Header
    {
        public enum EI_Class { Invalid = 0, BIT_32 = 1, BIT_64 = 2 };
        public enum Endianness { Invalid = 0, LSB = 1, MSB = 2 };
        public enum E_Type { None = 0, Rel = 1, Exec = 2, Dyn = 3, Core = 4, LOPROC = 0xff00, HIPROC = 0xffff };
        public enum E_Machine { None = 0, M32 = 1, Sparc = 2, M_386 = 3, M_68K = 4, M88K = 5, M_860 = 6, Mips = 8, Mips_rs4_be = 10 };
        public enum EF_Machine_Flags { };

        public const int Magic = 0x464c457f; // 0x7f ELF
        public const int Version = 1;

        public EI_Class Class { get; set; }
        public Endianness endianness;
        public E_Type Type { get; set; }
        public E_Machine InstructionSet { get; set; }
        public int EntryAddress { get; set; }
        public int ProgramHeaderOffset { get; set; }
        public int SectionHeaderOffset { get; set; }
        public EF_Machine_Flags Flags { get; set; }
        public ushort HeaderSize { get; set; }
        public ushort ProgramHeaderSize { get; set; }
        public ushort ProgramHeadersCount { get; set; }
        public ushort SectionHeaderSize { get; set; }
        public ushort SectionHeadersCount { get; set; }
        public ushort SectionStringTableIndex { get; set; }

        public void Write(Stream stream)
        {
            BinaryWriter bw = new BinaryWriter(stream);

            // 0x0
            bw.Write(Magic);
            bw.Write((byte)Class);
            bw.Write((byte)endianness);
            bw.Write((byte)Version);
            bw.Write(new byte[9]); // Padding

            // 0x10
            bw.Write((ushort)Type);
            bw.Write((ushort)InstructionSet);
            bw.Write(1);
            bw.Write(EntryAddress);
            bw.Write(ProgramHeaderOffset);

            // 0x20
            bw.Write(SectionHeaderOffset);
            bw.Write((int)Flags);
            bw.Write(HeaderSize);
            bw.Write((ushort)0x20);
            bw.Write(ProgramHeadersCount);
            bw.Write((ushort)0x28);

            // 0x30
            bw.Write(SectionHeadersCount);
            bw.Write(SectionStringTableIndex);
        }

        public Header(Stream stream)
        {
            BinaryReader br = new BinaryReader(stream);

            // Identification
            int magic = br.ReadInt32();
            if (magic != Magic)
                Console.WriteLine($"INVALID ELF MAGIC OF 0x{magic:X}");

            Class = (EI_Class)br.ReadByte();
            endianness = (Endianness)br.ReadByte();
            byte version = br.ReadByte();

            if (version != 1)
                Console.WriteLine($"INVALID ELF VERSION OF 0x{version:X}");

            stream.Position += 0x9; // Skip padding

            // Header
            Type = (E_Type)br.ReadInt16();
            InstructionSet = (E_Machine)br.ReadInt16();
            int _version = br.ReadInt32();
            EntryAddress = br.ReadInt32();
            ProgramHeaderOffset = br.ReadInt32();
            SectionHeaderOffset = br.ReadInt32();
            Flags = (EF_Machine_Flags)br.ReadInt32();
            HeaderSize = br.ReadUInt16();
            ProgramHeaderSize = br.ReadUInt16();
            ProgramHeadersCount = br.ReadUInt16();
            SectionHeaderSize = br.ReadUInt16();
            SectionHeadersCount = br.ReadUInt16();
            SectionStringTableIndex = br.ReadUInt16();
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Type:                          {Type}");
            sb.AppendLine($"Instruction Set:               {InstructionSet}");
            sb.AppendLine($"Entry Address:                 {EntryAddress:X}");
            sb.AppendLine($"Program Header Offset:         {ProgramHeaderOffset:X}");
            sb.AppendLine($"Section Header Offset:         {SectionHeaderOffset:X}");
            sb.AppendLine($"Flags:                         {Flags:X}");
            sb.AppendLine($"Header Size:                   {HeaderSize:X}");
            sb.AppendLine($"Program Header Size:           {ProgramHeaderSize:X}");
            sb.AppendLine($"Program Headers Count:         {ProgramHeadersCount:X}");
            sb.AppendLine($"Section Header Size:           {SectionHeaderSize:X}");
            sb.AppendLine($"Section Headers Count:         {SectionHeadersCount:X}");
            sb.AppendLine($"Section String Table Index:    {SectionStringTableIndex:X}");
            return sb.ToString();
        }
    }

    public struct SectionHeader
    {
        public enum SH_Type { NULL, PROGBITS, SymbolTable, StringTable, RelocationAddend, Hash, Dynamic, Note, NoBits, Relocatable, SHLIB, DynamicSymbol };
        public enum SH_Flags { Write = 0x1, Allocate = 0x2, Execute = 0x4 };

        public string Name;
        public int NameOffset { get; set; }
        public SH_Type Type { get; set; }
        public SH_Flags Flags { get; set; }
        public int MemoryAddress { get; set; }
        public int FileOffset { get; set; }
        public int Size { get; set; }
        public SH_Type Link { get; set; }
        public SH_Type Info { get; set; }
        public int Alignment { get; set; }
        public int EntrySize { get; set; }

        public byte[] Data { get; set; }

        public SectionHeader(Stream stream)
        {
            BinaryReader br = new BinaryReader(stream);
            NameOffset = br.ReadInt32();
            Type = (SH_Type)br.ReadInt32();
            Flags = (SH_Flags)br.ReadInt32();
            MemoryAddress = br.ReadInt32();
            FileOffset = br.ReadInt32();
            Size = br.ReadInt32();
            Link = (SH_Type)br.ReadInt32();
            Info = (SH_Type)br.ReadInt32();
            Alignment = br.ReadInt32();
            EntrySize = br.ReadInt32();
        }

        public void Write(Stream stream)
        {
            BinaryWriter bw = new BinaryWriter(stream);
            bw.Write(NameOffset);
            bw.Write((int)Type);
            bw.Write((int)Flags);
            bw.Write(MemoryAddress);
            bw.Write(FileOffset);
            bw.Write(Size);
            bw.Write((int)Link);
            bw.Write((int)Info);
            bw.Write(Alignment);
            bw.Write(EntrySize);

        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Name:            {Name}");
            sb.AppendLine($"\tName Offset:     {NameOffset:X}");
            sb.AppendLine($"\tType:            {Type}");
            sb.AppendLine($"\tFlags:           {Flags}");
            sb.AppendLine($"\tMemory Address:  {MemoryAddress:X}");
            sb.AppendLine($"\tFile Offset:     {FileOffset:X}");
            sb.AppendLine($"\tSize:            {Size:X}");
            sb.AppendLine($"\tLink:            {Link}");
            sb.AppendLine($"\tInfo:            {Info}");
            sb.AppendLine($"\tAlignment:       {Alignment:X}");
            sb.AppendLine($"\tEntry Size:      {EntrySize:X}");
            return sb.ToString();
        }
    }

    public struct ProgramHeader
    {
        public enum P_Type { Null, Load, Dynamic, Interpreter, Note, SHLIB, PHDR, TLS, LOPROC = 0x70000000 };
        public enum P_Flags { Execute = 1, Write = 2, Read = 4 };

        public P_Type Type { get; set; }
        public int FileOffset { get; set; }
        public int MemoryAddress { get; set; }
        public int PhysicalAddress { get; set; }
        public int MemorySize { get; set; }
        public int FileSize { get; set; }
        public P_Flags Flags { get; set; }
        public int Alignment { get; set; }

        public byte[] Data { get; set; }

        public ProgramHeader(Stream stream)
        {
            BinaryReader br = new BinaryReader(stream);
            Type = (P_Type)br.ReadInt32();
            FileOffset = br.ReadInt32();
            MemoryAddress = br.ReadInt32();
            PhysicalAddress = br.ReadInt32();
            MemorySize = br.ReadInt32();
            FileSize = br.ReadInt32();
            Flags = (P_Flags)br.ReadInt32();
            Alignment = br.ReadInt32();
        }

        public void Write(Stream stream)
        {
            BinaryWriter bw = new BinaryWriter(stream);
            bw.Write((int)Type);
            bw.Write(FileOffset);
            bw.Write(MemoryAddress);
            bw.Write(PhysicalAddress);
            bw.Write(MemorySize);
            bw.Write(FileSize);
            bw.Write((int)Flags);
            bw.Write(Alignment);
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Type:                {Type}");
            sb.AppendLine($"Offset:              {FileOffset:X}");
            sb.AppendLine($"Memory Address:      {MemoryAddress:X}");
            sb.AppendLine($"Physical Address:    {PhysicalAddress:X}");
            sb.AppendLine($"Memory Size:         {MemorySize:X}");
            sb.AppendLine($"File Size:           {FileSize:X}");
            sb.AppendLine($"Flags:               {Flags}");
            sb.AppendLine($"Alignment:           {Alignment:X}");
            return sb.ToString();
        }
    }

    private string ReadString(BinaryReader br)
    {
        StringBuilder sb = new StringBuilder();
        char c = br.ReadChar();

        while (c != 0x0)
        {
            sb.Append(c);
            c = br.ReadChar();
        }

        return sb.ToString();
    }

    private void ReadSectionHeaders(Stream stream)
    {
        SectionHeaders = new SectionHeader[header.SectionHeadersCount];
        stream.Position = header.SectionHeaderOffset; // This shouldnt change

        // Read all headers
        for (int i = 0; i < SectionHeaders.Length; i++)
        {
            SectionHeaders[i] = new SectionHeader(stream);
        }

        // Readback all names
        BinaryReader br = new BinaryReader(stream);
        for (int i = 0; i < SectionHeaders.Length; i++)
        {
            stream.Position = SectionHeaders[header.SectionStringTableIndex].FileOffset + SectionHeaders[i].NameOffset;
            SectionHeaders[i].Name = ReadString(br);

            switch (SectionHeaders[i].Name)
            {
                case ".text":
                    _textSection = i; 
                    break;
            }
        }

        // Read all data
        for (int i = 0; i < SectionHeaders.Length; i++)
        {
            stream.Position = SectionHeaders[i].FileOffset;
            byte[] data = br.ReadBytes(SectionHeaders[i].Size);
            SectionHeaders[i].Data = data;
        }

        // Revert the stream back to where it was
        stream.Position = header.SectionHeaderOffset + SectionHeaders.Length * header.SectionHeaderSize;
    }

    private void ReadProgramHeaders(Stream stream)
    {
        ProgramHeaders = new ProgramHeader[header.ProgramHeadersCount];
        stream.Position = header.ProgramHeaderOffset; // This shouldnt change

        // Read all headers
        for (int i = 0; i < ProgramHeaders.Length; i++)
        {
            ProgramHeaders[i] = new ProgramHeader(stream);
        }

        // Read all data
        BinaryReader br = new BinaryReader(stream);
        for (int i = 0; i < ProgramHeaders.Length; i++)
        {
            stream.Position = ProgramHeaders[i].FileOffset;
            ProgramHeaders[i].Data = br.ReadBytes(ProgramHeaders[i].FileSize);
        }
    }

    public ELF(Stream stream)
    {
        Read(stream);
    }

    public ELF(string path)
    {
        using (FileStream fs = new FileStream(path, FileMode.Open))
            Read(fs);
    }

    private void Read(Stream stream)
    {
        header = new Header(stream);
        ReadSectionHeaders(stream);
        ReadProgramHeaders(stream);
    }

    public Header header;
    public SectionHeader[] SectionHeaders;
    public ProgramHeader[] ProgramHeaders;
    
    private int _textSection;
    public SectionHeader TextSection => SectionHeaders[_textSection];

    public void ToFile(string output, int DataStart = 0x1000)
    {
        using (FileStream fs = new FileStream(output, FileMode.OpenOrCreate))
        {
            BinaryWriter bw = new BinaryWriter(fs);

            // Skip header for now, come back later
            fs.Write(new byte[Math.Min(DataStart, Marshal.SizeOf<Header>())]);
            fs.Position = Marshal.SizeOf<Header>();

            // Write all program headers
            int programHeaderOffset = 0;
            int programHeadersCount = 0;
            if (ProgramHeaders != null)
            {
                programHeadersCount = ProgramHeaders.Length;
                programHeaderOffset = (int)fs.Position;
                for (int i = 0; i < ProgramHeaders.Length; i++)
                {
                    ProgramHeaders[i].FileOffset = int.MaxValue;
                    ProgramHeaders[i].Write(fs);
                }
            }

            // Write all sections
            fs.Position = Math.Max(Marshal.SizeOf<Header>(), DataStart);
            for (int i = 0; i < SectionHeaders.Length; i++)
            {
                // correct alignment
                if (SectionHeaders[i].Alignment != 0)
                    bw.Write(new byte[fs.Position % SectionHeaders[i].Alignment]);


                SectionHeaders[i].FileOffset = (int)fs.Position;
                bw.Write(SectionHeaders[i].Data);
            }

            // Write all section headers
            int sectionHeaderOffset = (int)fs.Position;
            for (int i = 0; i < SectionHeaders.Length; i++)
            {
                SectionHeaders[i].Write(fs);
            }

            int stringTableIndex = SectionHeaders.Length;
            for (int i = 0; i < SectionHeaders.Length; i++)
            {
                switch (SectionHeaders[i].Name)
                {
                    case ".strtbl": stringTableIndex = i; break;
                }
            }


            // Go back for ELF Header
            fs.Position = 0;
            Header header = new Header
            {
                Class = Header.EI_Class.BIT_32,
                endianness = Header.Endianness.LSB,
                Type = Header.E_Type.Rel,
                InstructionSet = Header.E_Machine.Mips,
                EntryAddress = 0,
                ProgramHeaderOffset = programHeaderOffset,
                ProgramHeadersCount = (ushort)programHeadersCount,
                SectionHeaderOffset = sectionHeaderOffset,
                SectionHeadersCount = (ushort)SectionHeaders.Length,
                HeaderSize = (ushort)Marshal.SizeOf<Header>(),
                SectionStringTableIndex = (ushort)stringTableIndex,
            };
            header.Write(fs);
        }
    }

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("----- HEADER -----");
        sb.AppendLine(header.ToString());

        sb.AppendLine("----- Section Headers -----");
        for (int i = 0; i < SectionHeaders.Length; i++)
        {
            sb.AppendLine($"--- Section Header {i} ---");
            sb.AppendLine(SectionHeaders[i].ToString());
        }

        sb.AppendLine("----- Program Headers -----");
        for (int i = 0; i < ProgramHeaders.Length; i++)
        {
            sb.AppendLine($"--- Program Header {i} ---");
            sb.AppendLine(ProgramHeaders[i].ToString());
        }

        return sb.ToString();
    }
}