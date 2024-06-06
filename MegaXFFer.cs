using System.IO.Pipes;
using System.Net;
using System.Text;

static class MegaXFFer
{
    private struct SymbolReference
    {
        public Symbol symbol { get; set; }
        public int XFFIndex { get; set; }
    }

    #region Creation
    private static void Create(Stream stream, string[] paths, string mainElfPath, string outputDirectory)
    {
        // Read all xffs
        XFF[] xffs = new XFF[paths.Length];
        for (int i = 0; i < paths.Length; i++)
        {
            xffs[i] = new XFF(paths[i]);
        }

        // Go through each xff then split everything by section

        // SectionToXffOffset
        // Key = section name (i.e. .text)
        // Value = int[] where each element in the array maps to an xff and the value is the xff's data offset
        //          I.E. XFF 1 has a text section 0x280 bytes long at file offset 0x0
        //               XFF 2 has a text section 0x100 bytes long at file offset 0x0
        //               Value[0] = 0
        //               Value[1] = 0x280
        // The last element of the array is reserved for sections in the main ELF 
        CreateSectionMapping(xffs, mainElfPath, out ELF elf, out Dictionary<string, int[]> sectionToXffOffset, out Dictionary<string, List<byte>> sectionToData, out Dictionary<string, ELF.SectionHeader> elfSections);
        WritePatchedXFF(stream, elf, sectionToData, out Dictionary<string, long> sectionStarts);
        ApplyRelocations(stream, xffs, paths, sectionStarts, sectionToXffOffset, outputDirectory);
        WriteDebugInformation(xffs, paths, elf, sectionStarts, sectionToXffOffset, sectionToData, elfSections, outputDirectory);
    }

    public static void CreatePatchedFile(string[] paths, string mainElfPath, string outputDirectory)
    {
        string OutputPatchDirectory = GetOutputPatchDirectory(outputDirectory); ;
        if (!Directory.Exists(OutputPatchDirectory))
            Directory.CreateDirectory(OutputPatchDirectory);

        // Write all of these sections to a file
        using (FileStream fs = new FileStream($"{OutputPatchDirectory}/Master.XFF", FileMode.Create))
        {
            Create(fs, paths, mainElfPath, outputDirectory);
        }
    }

    #endregion
    #region Combining XFFs
    private static void CreateSectionMapping(XFF[] xffs, string mainElfPath, out ELF elf, out Dictionary<string, int[]> sectionToXffOffset, out Dictionary<string, List<byte>> sectionToData, out Dictionary<string, ELF.SectionHeader> elfSections)
    {
        sectionToData = new Dictionary<string, List<byte>>();
        sectionToXffOffset = new Dictionary<string, int[]>();

        for (int i = 0; i < xffs.Length; i++)
        {
            // Go through each header
            for (int s = 0; s < xffs[i].SectionHeaders.Length; s++)
            {
                SectionHeader header = xffs[i].SectionHeaders[s];
                string sectionName = header.Name;

                // Append the data to the sectionToData thing
                if (!sectionToData.ContainsKey(sectionName))
                    sectionToData.Add(sectionName, new List<byte>());

                if (!sectionToXffOffset.ContainsKey(sectionName))
                    sectionToXffOffset.Add(sectionName, new int[xffs.Length + 1]); // Add 1 which reserves the ELF sections


                List<byte> massSectionData = sectionToData[sectionName];
                sectionToXffOffset[sectionName][i] = massSectionData.Count;

                massSectionData.AddRange(header.Data);
            }
        }

        // Add Main ELF Sections
        elfSections = new Dictionary<string, ELF.SectionHeader>();
        elf = new ELF(mainElfPath);
        for (int i = 0; i < elf.SectionHeaders.Length; i++)
        {
            elfSections.Add(elf.SectionHeaders[i].Name, elf.SectionHeaders[i]);
        }
    }

    private static void WritePatchedXFF(Stream stream, ELF elf, Dictionary<string, List<byte>> sectionToData, out Dictionary<string, long> sectionStarts)
    {
        // Order by their position in memory
        var elfSectionsByMemory = (from x in elf.SectionHeaders orderby x.MemoryAddress ascending select x).ToArray();

        BinaryWriter bw = new BinaryWriter(stream);
        sectionStarts = new Dictionary<string, long>();

        // Write each section to the output file
        int currentElfSection = 0;
        // foreach (var section in sectionToData.Keys)
        for (int i = 0; i < sectionToData.Keys.Count; i++)
        {
            var sectionName = sectionToData.Keys.ToArray()[i];

            // Check if this section can be written and doesn't intersect with the main elf's sections
            if (TryWriteELFSection(bw, elf, elfSectionsByMemory, sectionStarts, sectionToData[sectionName].Count, ref currentElfSection))
            {
                i--;
                continue;
            }

            // Otherwise write the section normally
            WriteSection(bw, sectionName, sectionToData, sectionStarts);
        }
    }

    private static bool TryWriteELFSection(BinaryWriter bw, ELF elf, ELF.SectionHeader[] elfSectionsByMemoryAddress, Dictionary<string, long> sectionStarts, int currentSectionLength, ref int currentElfSection)
    {
        // Cant write a section if we've written all of the elf sections already
        if (currentElfSection >= elfSectionsByMemoryAddress.Length)
            return false;

        // Get the current section
        ELF.SectionHeader elfSection = elfSectionsByMemoryAddress[currentElfSection];

        // Skip sections without data
        while (elfSection.Size <= 0 && currentElfSection < elfSectionsByMemoryAddress.Length - 1)
        {
            currentElfSection++;
            elfSection = elfSectionsByMemoryAddress[currentElfSection];
        }

        if (currentSectionLength + bw.BaseStream.Position >= elfSection.MemoryAddress)
        {
            // If not, write padding until the elf section
            long elfPadding = elfSection.MemoryAddress - bw.BaseStream.Position;

            // yaaaaay debuging
            // I love when my code doesnt work
            Console.WriteLine($"Writing padding until elf section");
            Console.WriteLine($"\tFile Stream Pos: {bw.BaseStream.Position:X}");
            Console.WriteLine($"\tSection Size:    {elfSection.Size:X}");
            Console.WriteLine($"\tCalculated End:  {elfSection.Size + bw.BaseStream.Position + elfPadding:X}");
            Console.WriteLine($"\tMemory Address:  {elfSection.MemoryAddress:X}");
            Console.WriteLine($"\tSection:         {elfSection.Name}");
            Console.WriteLine($"\tPadding Amount:  {elfPadding:X}");
            bw.Write(new byte[elfPadding]);

            sectionStarts.Add($"{elfSection.Name}-main", bw.BaseStream.Position);

            // Write the elf section
            if (elfSection.Type == ELF.SectionHeader.SH_Type.NoBits)
            {
                bw.Write(new byte[elfSection.Size]);
            }
            else
            {
                bw.Write(elfSection.Data);
            }

            currentElfSection++;
            return true;
        }

        return false;
    }

    private static void WriteSection(BinaryWriter bw, string sectionName, Dictionary<string, List<byte>> sectionToData, Dictionary<string, long> sectionStarts)
    {
        // Console.WriteLine("Writing section: " + section);
        Console.WriteLine($"{sectionName} at 0x{bw.BaseStream.Position:X} - 0x{bw.BaseStream.Position + sectionToData[sectionName].Count:X} (Length: 0x{sectionToData[sectionName].Count:X})");

        sectionStarts.Add(sectionName, bw.BaseStream.Position);

        // Write empty data to bss section
        // Technically should be if the section header is of type 'NoBits' but this works well enough for now
        if (sectionName == ".bss")
        {
            bw.Write(new byte[sectionToData[sectionName].Count]);
            return;
        }

        bw.Write(sectionToData[sectionName].ToArray());
        // Write padding for each section to the nearest 0x4 bytes
        long padding = 4 - bw.BaseStream.Position % 4;
        bw.Write(new byte[padding]);
    }


    #endregion
    #region Relocations

    private static Dictionary<string, SymbolReference> GetAllSymbols(XFF[] xffs, string[] paths)
    {
        // Try to find all the symbols in the xffs' that need to be found elsewhere
        Dictionary<string, SymbolReference> masterSymbolReference = new Dictionary<string, SymbolReference>();
        for (int i = 0; i < xffs.Length; i++)
        {
            FileInfo xffFile = new FileInfo(paths[i]);
            for (int s = 0; s < xffs[i].Symbols.Length; s++)
            {
                Symbol symbol = xffs[i].Symbols[s];
                if (symbol.section == 0)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(symbol.name))
                    continue;

                if (masterSymbolReference.ContainsKey(symbol.name))
                {
                    // SymbolReference oldRef = masterSymbolReference[symbol.name];
                    Console.WriteLine($"Trying to add duplicate symbol reference: {symbol.name}");
                    continue;
                }

                masterSymbolReference.Add(symbol.name, new SymbolReference
                {
                    XFFIndex = i,
                    symbol = symbol
                });
            }
        }

        return masterSymbolReference;
    }

    private static void ApplyRelocations(Stream stream, XFF[] xffs, string[] paths, Dictionary<string, long> sectionStarts, Dictionary<string, int[]> sectionToXffOffset, string outputDirectory)
    {
        Dictionary<string, SymbolReference> nameToSymbol = GetAllSymbols(xffs, paths);

        // Apply all relocations
        int appliedRelocations = 0;
        int relocatable = 0;

        BinaryReader br = new BinaryReader(stream);

        StringBuilder relocationDebug = new StringBuilder();
        StringBuilder failedRelocations = new StringBuilder();

        for (int i = 0; i < xffs.Length; i++)
        {
            ApplyXFFRelocation(xffs, br, i, sectionStarts, sectionToXffOffset, nameToSymbol, relocationDebug, failedRelocations, ref relocatable, ref appliedRelocations);
        }

        // Write relocation results to a file
        string outputPatchDirectory = GetOutputPatchDirectory(outputDirectory);
        File.WriteAllText($"{outputPatchDirectory}/Failed Relocations.txt", failedRelocations.ToString());
        File.WriteAllText($"{outputPatchDirectory}/Relocation debug.txt", relocationDebug.ToString());
        Console.WriteLine($"Applied {appliedRelocations} / {relocatable} relocations ({(float)appliedRelocations / relocatable * 100}%)");
        Console.WriteLine($"Failed {relocatable - appliedRelocations} relocations");
    }

    private static void ApplyXFFRelocation(XFF[] xffs, BinaryReader br, int xffIndex, Dictionary<string, long> sectionStarts, Dictionary<string, int[]> sectionToXffOffset, Dictionary<string, SymbolReference> nameToSymbol, StringBuilder relocationDebug, StringBuilder failedRelocations, ref int possibleRelocations, ref int appliedRelocations)
    {
        XFF currentXff = xffs[xffIndex];
        foreach (var header in currentXff.RelocationHeaders)
        {
            string sectionName = currentXff.SectionHeaders[header.sectionIndex].Name;

            for (int relocationIndex = 0; relocationIndex < header.relocations.Length; relocationIndex++)
            {
                Relocation relocation = header.relocations[relocationIndex];

                // Calculate the new file location
                Symbol xffSymbol = currentXff.Symbols[relocation.SymbolIndex];


                // skip null symbols
                if (string.IsNullOrEmpty(xffSymbol.name))
                    continue;

                GetInstructionAddresses(header, sectionStarts, sectionToXffOffset, relocationIndex, xffIndex, sectionName, out long relocationFileAddress, out long low16Address);

                possibleRelocations++;
                if (!nameToSymbol.ContainsKey(xffSymbol.name))
                {
                    // I assume most of these were stripped from the final version because most of them 
                    // seem to be debug displays or tools
                    failedRelocations.AppendLine($"File Address: {relocationFileAddress:X} Symbol Name: {xffSymbol.name}");
                    Console.WriteLine($"Could not find symbol with name '{xffSymbol.name}' Flags: {xffSymbol.flags:X}");
                    continue;
                }

                SymbolReference symbolReference = nameToSymbol[xffSymbol.name];

                // Get the address of the symbol in the file
                long symbolFileAddress = GetSymbolAddressInFile(xffs, xffIndex, relocation, symbolReference, sectionStarts, sectionToXffOffset, out string symbolSectionName);

                if (symbolFileAddress == -1)
                {
                    Console.WriteLine("Failed to get symbol address");
                    continue;
                }

                // Apply relocation
                br.BaseStream.Position = relocationFileAddress;

                relocationDebug.AppendLine($"Address: 0x{relocationFileAddress:X}");
                relocationDebug.AppendLine($"\tSymbol: {xffSymbol.name}");
                relocationDebug.AppendLine($"\tSymbol Address: 0x{symbolFileAddress:x}");
                relocationDebug.AppendLine($"\tType: {relocation.Type}");
                relocationDebug.AppendLine($"\t\tBase Instruction: {br.ReadUInt32():X}");
                relocationDebug.AppendLine($"\tSymbol Flags: {xffSymbol.flags}");
                relocationDebug.AppendLine($"\tXFF: {xffIndex}");
                relocationDebug.AppendLine($"\tXFF Section Start: {sectionToXffOffset[symbolSectionName][xffIndex]:X}");

                if (ApplyRelocation(br.BaseStream, header, relocationIndex, relocation, symbolReference.symbol, relocationFileAddress, symbolFileAddress, low16Address))
                    appliedRelocations++;
            }
        }
    }

    private static void GetInstructionAddresses(RelocationHeader header, Dictionary<string, long> sectionStarts, Dictionary<string, int[]> sectionToXffOffset, int relocationIndex, int xffIndex, string sectionName, out long relocationFileAddress, out long low16Address)
    {
        Relocation relocation = header.relocations[relocationIndex];

        relocationFileAddress = sectionStarts[sectionName] + relocation.offset + sectionToXffOffset[sectionName][xffIndex];
        low16Address = relocationFileAddress;

        Relocation.RelocationType relocationType = relocation.Type;
        int nextRelocationIndex = relocationIndex + 1;

        while (relocationType == Relocation.RelocationType.High16 && nextRelocationIndex < header.relocations.Length)
        {
            Relocation nextRelocation = header.relocations[nextRelocationIndex];
            if (nextRelocation.Type == Relocation.RelocationType.Low16)
            {
                low16Address = sectionStarts[sectionName] + nextRelocation.offset + sectionToXffOffset[sectionName][xffIndex];
                break;
            }

            nextRelocationIndex++;
        }
    }

    private static long GetSymbolAddressInFile(XFF[] xffs, int currentXffIndex, Relocation relocation, SymbolReference symbolReference, Dictionary<string, long> sectionStarts, Dictionary<string, int[]> sectionToXffOffset, out string symbolSectionName)
    {
        symbolSectionName = "";
        // 0xfff1 is reserved for functions loaded at a static address
        if (symbolReference.symbol.section == 0xfff1) // CSPX_150.97 function
        {
            int[] symbolLocations = xffs[currentXffIndex].SymbolLocations;
            long staticAddress = symbolLocations[relocation.SymbolIndex];
            return staticAddress;
        }
        else if (symbolReference.symbol.section < 0xff00) // Everything less than 0xff00 is any other section inside the xff
        {
            if (symbolReference.symbol.flags == Symbol.Flags.Section)
                return sectionStarts[symbolReference.symbol.name] + sectionToXffOffset[symbolReference.symbol.name][currentXffIndex];

            symbolSectionName = xffs[symbolReference.XFFIndex].SectionHeaders[symbolReference.symbol.section].Name;
            long symbolSectionAddress = sectionStarts[symbolSectionName] + sectionToXffOffset[symbolSectionName][symbolReference.XFFIndex];
            return symbolReference.symbol.offsetAddress + symbolSectionAddress;
        }
        else if (symbolReference.symbol.section == 0 && symbolReference.symbol.flags != Symbol.Flags.External) // Error
            Console.WriteLine("Unable to find symbol for relocation");

        return -1;
    }

    private static bool ApplyRelocation(Stream fs, RelocationHeader header, int relocationIndex, Relocation relocation, Symbol symbol, long relocationTargetAddress, long symbolFileAddress, long low16Location = 0)
    {
        BinaryReader br = new BinaryReader(fs);
        BinaryWriter bw = new BinaryWriter(fs);


        bw.BaseStream.Position = relocationTargetAddress;
        uint baseInstruction = br.ReadUInt32();

        br.BaseStream.Position = low16Location;
        uint low16 = br.ReadUInt32() & 0xffff;
        br.BaseStream.Position = relocationTargetAddress;


        uint lowerAddressData = (uint)(symbolFileAddress + low16) & 0xffff;
        uint upperAddressData = (uint)(symbolFileAddress + low16) >> 0x10;
        if (lowerAddressData > short.MaxValue)
        {
            upperAddressData++;
            // lowerAddressData = 0x10000 - lowerAddressData;
        }

        if (relocationTargetAddress == 0x396C70)
            Console.WriteLine("Hell");

        switch (relocation.Type)
        {
            default:

                // if (symbol.name == "gcGlobalVar")
                //     break;

                Console.WriteLine("Unknown relocation type: " + relocation.Type);
                // Console.WriteLine($"\tSymbol Name:      {symbol.name}");
                // Console.WriteLine($"\tSymbol Section:   {SectionHeaders[symbol.section].Name}");
                return false;

            case Relocation.RelocationType.Null:
                Console.WriteLine("Null relocation type");
                return false;

            case Relocation.RelocationType.Jump: // I think
                bw.Write((uint)symbolFileAddress + baseInstruction);
                return true;

            case Relocation.RelocationType.Somethign:
                uint writtenData = (uint)(((symbolFileAddress >> 2 & 0x3ffffff) + ((uint)baseInstruction & 0x3ffffff)) | ((uint)baseInstruction & ~0x3ffffff));
                bw.Write(writtenData);
                return true;

            case Relocation.RelocationType.High16:  // upper half, Probably implemented wrong
                bw.Write((uint)(baseInstruction & 0xffff0000) | ((upperAddressData + (baseInstruction & 0xffff))));


                return true;

            case Relocation.RelocationType.Low16:  // Lower half of the target address
                     // bw.Write((int)((symbolFileAddress & 0xffff) + baseInstruction));

                bw.Write((int)(baseInstruction & 0xffff0000 | lowerAddressData));
                return true;
        }

    }
    #endregion
    #region Write Debug Files
    private static string GetOutputPatchDirectory(string baseDirectory) => $"{baseDirectory}/Patched";

    private static void WriteDebugInformation(XFF[] xffs, string[] paths, ELF elf, Dictionary<string, long> sectionStarts, Dictionary<string, int[]> sectionToXffOffset, Dictionary<string, List<byte>> sectionToData, Dictionary<string, ELF.SectionHeader> elfSections, string outputDirectory)
    {
        WriteGhidraImport(xffs, sectionStarts, sectionToXffOffset, outputDirectory);
        WriteMasterSymbolList(xffs, sectionStarts, sectionToXffOffset, outputDirectory);
        WriteElfSections(elf, outputDirectory);
        WriteMasterXFFSections(xffs, paths, sectionStarts, sectionToData, sectionToXffOffset, elfSections, outputDirectory);
        WriteXFFDebugInfo(xffs, paths, sectionStarts, sectionToXffOffset, outputDirectory);
    }


    private static void WriteGhidraImport(XFF[] xffs, Dictionary<string, long> sectionStarts, Dictionary<string, int[]> sectionToXffOffset, string outputDirectory)
    {
        // Create symbol list for ghidra
        StringBuilder sb = new StringBuilder();

        int symbolCount = 0;
        for (int i = 0; i < xffs.Length; i++)
        {
            foreach (var symbol in xffs[i].Symbols)
            {
                if (!string.IsNullOrEmpty(symbol.name))
                    symbolCount++;
            }
        }

        sb.AppendLine(symbolCount.ToString());

        for (int xffIndex = 0; xffIndex < xffs.Length; xffIndex++)
        {
            XFF xff = xffs[xffIndex];
            for (int i = 0; i < xff.Symbols.Length; i++)
            {
                Symbol symbol = xff.Symbols[i];

                if (string.IsNullOrEmpty(symbol.name))
                    continue;

                // Get section name
                string sectionName;
                long fileOffset = -1;

                if (symbol.section < 0xff00)
                {
                    sectionName = xff.SectionHeaders[symbol.section].Name;
                    fileOffset = sectionStarts[sectionName] + sectionToXffOffset[sectionName][xffIndex] + symbol.offsetAddress;
                }
                else
                {
                    int memoryAddress = xff.SymbolLocations[i];
                    fileOffset = memoryAddress;
                }

                if (fileOffset < 0)
                {
                    Console.WriteLine($"Failed to get ghidra symbol");
                    continue;
                }

                sb.AppendLine($"{symbol.name};0x{fileOffset:X}");
            }
        }

        File.WriteAllText($"{GetOutputPatchDirectory(outputDirectory)}/Ghidra Import.txt", sb.ToString());
    }
    private static void WriteMasterSymbolList(XFF[] xffs, Dictionary<string, long> sectionStarts, Dictionary<string, int[]> sectionToXffOffset, string outputDirectory)
    {
        StringBuilder masterSymbolBuilder = new StringBuilder();
        for (int i = 0; i < xffs.Length; i++)
        {
            var xff = xffs[i];
            foreach (var symbol in xff.Symbols)
            {
                masterSymbolBuilder.AppendLine(symbol.ToString());

                if (symbol.section >= xffs[i].SectionHeaders.Length)
                    continue;

                string symbolSection = xffs[i].SectionHeaders[symbol.section].Name;
                masterSymbolBuilder.AppendLine($"\tFile Location: 0x{symbol.offsetAddress + sectionStarts[symbolSection] + sectionToXffOffset[symbolSection][i]:X}");
                masterSymbolBuilder.AppendLine();

            }
        }

        File.WriteAllText($"{GetOutputPatchDirectory(outputDirectory)}/Symbols.txt", masterSymbolBuilder.ToString());
    }

    private static void WriteElfSections(ELF elf, string outputDirectory)
    {
        string dir = $"{outputDirectory}/Main ELF";
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        StringBuilder elfSectionsWriter = new StringBuilder();
        for (int i = 0; i < elf.SectionHeaders.Length; i++)
        {
            var header = elf.SectionHeaders[i];
            elfSectionsWriter.AppendLine($"Section 0x{i:X}");
            elfSectionsWriter.AppendLine(header.ToString());
        }
        File.WriteAllText($"{dir}/Sections.txt", elfSectionsWriter.ToString());

    }

    private static void WriteMasterXFFSections(XFF[] xffs, string[] paths, Dictionary<string, long> sectionStarts, Dictionary<string, List<byte>> sectionToData, Dictionary<string, int[]> sectionToXffOffset, Dictionary<string, ELF.SectionHeader> elfSections, string outputDirectory)
    {
        // Write Mega XFF section splits
        StringBuilder sectionWriter = new StringBuilder();
        foreach (var section in sectionStarts.Keys)
        {
            sectionWriter.AppendLine();
            // if (section.Contains("-main"))
            //     continue;
            byte[] data;
            if (!sectionToData.ContainsKey($"{section}"))
            {
                data = elfSections[section.Replace("-main", "")].Data;
            }
            else
            {
                data = sectionToData[section].ToArray();
            }


            sectionWriter.AppendLine($"Section: {section}\n\tStart: \t\t0x{sectionStarts[section]:X}\n\tEnd: \t\t0x{sectionStarts[section] + data.Length:X}\n\tLength: \t0x{data.Length:X}");
            if (section.Contains("-main"))
                continue;

            for (int i = 0; i < xffs.Length; i++)
            {
                FileInfo info = new FileInfo(paths[i]);

                sectionWriter.AppendLine();
                sectionWriter.AppendLine($"\tXFF Name:               {info.Name}");
                sectionWriter.AppendLine($"\tXFF Section Offset:     0x{sectionToXffOffset[section][i]:X}");
                sectionWriter.AppendLine($"\tXFF Section File Pos:   0x{sectionStarts[section] + sectionToXffOffset[section][i]:X}");
            }

        }

        File.WriteAllText($"{GetOutputPatchDirectory(outputDirectory)}/Sections.txt", sectionWriter.ToString());
    }

    private static void WriteXFFDebugInfo(XFF[] xffs, string[] paths, Dictionary<string, long> sectionStarts, Dictionary<string, int[]> sectionToXffOffset, string outputDirectory)
    {
        // Write all info for each XFF
        for (int i = 0; i < paths.Length; i++)
        {
            FileInfo xffFile = new FileInfo(paths[i]);
            string xffDir = $"{outputDirectory}/{xffFile.Name.Replace(xffFile.Extension, "")}";
            Console.WriteLine($"Creating debug info at path {xffDir}");
            if (!Directory.Exists(xffDir))
                Directory.CreateDirectory(xffDir);


            StringBuilder symbolBuilder = new StringBuilder();
            foreach (var symbol in xffs[i].Symbols)
            {
                symbolBuilder.AppendLine(symbol.ToString().Trim());

                if (symbol.section >= xffs[i].SectionHeaders.Length)
                    continue;

                string symbolSection = xffs[i].SectionHeaders[symbol.section].Name;
                symbolBuilder.AppendLine($"\tFile Location:    0x{symbol.offsetAddress + sectionStarts[symbolSection] + sectionToXffOffset[symbolSection][i]}");
                symbolBuilder.AppendLine();

            }
            File.WriteAllText($"{xffDir}/Symbols.txt", symbolBuilder.ToString());

            File.WriteAllText($"{xffDir}/Section Headers.txt", WriteObjectArray(xffs[i].SectionHeaders));
            File.WriteAllText($"{xffDir}/Relocations.txt", WriteRelocations(xffs[i]));
        }
    }
    private static string WriteObjectArray<T>(T[] objects)
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < objects.Length; i++)
        {
            sb.AppendLine(objects[i].ToString());
        }

        return sb.ToString();
    }

    private static string WriteRelocations(XFF xff)
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < xff.RelocationHeaders.Length; i++)
        {
            RelocationHeader header = xff.RelocationHeaders[i];
            for (int r = 0; r < header.relocations.Length; r++)
            {
                Relocation relocation = header.relocations[r];

                sb.AppendLine($"Header {i} Relocation {r}");
                sb.AppendLine($"\tFile Offset: {xff.SectionHeaders[header.sectionIndex].FileOffset + relocation.offset}");
                sb.AppendLine($"\tSymbol Name: {xff.Symbols[relocation.packedSymbolIndex >> 8].name}");
                sb.AppendLine($"\tType:        {relocation.packedSymbolIndex & 0xff}");
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    #endregion

}
