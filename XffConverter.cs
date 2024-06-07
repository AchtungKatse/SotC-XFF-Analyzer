using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// This doesnt work
// Dont try to use it

static class XFFConverter
{
    public static ELF.SectionHeader XffToElfSectionHeader(XFF.SectionHeader xff, ELF.SectionHeader.SH_Flags flags, int elementSize)
    {
        return new ELF.SectionHeader 
        {
            Type = xff.Type,
            Flags = flags,
            MemoryAddress = xff.MemoryAddress,
            Length = xff.Length,
            Alignment = xff.Alignment,
            EntrySize = elementSize,
            Data = xff.Data
        };
    }

    public static ELF.SectionHeader XffRelocationHeaderToSectionHeader(RelocationHeader relocation)
    {
        byte[] data = new byte[relocation.relocationCount * 8];
        MemoryStream ms = new MemoryStream(data);
        BinaryWriter bw = new BinaryWriter(ms);
        for (int i = 0; i < relocation.relocationCount; i++)
        {
            Relocation r = relocation.relocations[i];
            bw.Write(r.fileLocation);
            bw.Write(r.packedSymbolIndex);
        }

        ms.Close();

        return new ELF.SectionHeader
        {
            Type= ISectionHeader.SH_Type.Relocatable,
            Flags = 0,
            MemoryAddress = relocation.virtmemPtr,
            Length = relocation.relocationCount * 0x8,
            Alignment = 0x8,
            EntrySize = 0x8,
            Data = data,
        };
    }

    public static ELF XffToElf(XFF xff)
    {
        // Rip out all section headers
        ELF.SectionHeader[] sections = new ELF.SectionHeader[xff.SectionHeaders.Length + xff.RelocationHeaders.Length];

        // Convert all sections
        for (int i = 0; i < xff.SectionHeaders.Length; i++)
        {
            ELF.SectionHeader.SH_Flags flags = 0;
            switch (xff.SectionHeaders[i].Name)
            {
                case ".text": flags = ELF.SectionHeader.SH_Flags.Execute | ELF.SectionHeader.SH_Flags.Allocate; break;
                case ".data": flags = ELF.SectionHeader.SH_Flags.Write; break; // idk
            }

            sections[i] = XffToElfSectionHeader(xff.SectionHeaders[i], flags, xff.SectionHeaders[i].Alignment);
        }

        for (int i = 0; i < xff.RelocationHeaders.Length; i++)
        {
            sections[i + xff.SectionHeaders.Length] = XffRelocationHeaderToSectionHeader(xff.RelocationHeaders[i]);
        }

        return new ELF
        {
            header = new ELF.Header
            {
                Class = ELF.Header.EI_Class.BIT_32,
                endianness = ELF.Header.Endianness.LSB,
                Type = ELF.Header.E_Type.Rel,
                InstructionSet = ELF.Header.E_Machine.Mips,
                EntryAddress = 0,
            },

            SectionHeaders = sections,
        };
    } 
}