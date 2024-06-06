// See https://aka.ms/new-console-template for more information
using System.Data;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks.Dataflow;

class MainProgram
{
    private static bool IsAnXFF(string path)
    {
        using (FileStream fs = new FileStream(path, FileMode.Open))
        {
            BinaryReader br = new BinaryReader(fs);
            return br.ReadInt32() == 0x32666678;
        }
    }

    private static string[] XffPaths { get; set; } = new string[0];
    private static XFF[] Xffs { get; set; } = new XFF[0];
    public static ELF MainElf { get; private set; }
    private static string InputDirectory { get; set; } = "";
    private static string OutputDirectory { get; set; } = "";
    private static string MainExecutablePath { get; set; } = "";



    static void Main(string[] args)
    {
        args = ["../Files", "SCUS-97472", "-ws", "-de"];
        if (args.Length < 2)
        {
            Console.WriteLine("Invalid format: ./XFF (input directory) (output directory)");
            return;
        }


        // // Get all of the arguments
        InputDirectory = args[0];
        OutputDirectory = args[1];

        // InputDirectory = "../Files";
        // OutputDirectory = "SCUS-97472-new";

        // Get all xff files
        List<string> xffPaths = new List<string>();
        foreach (var file in Directory.GetFiles(InputDirectory))
        {
            FileInfo info = new FileInfo(file);
            if (info.Extension.ToLower().Equals(".xff"))
                if (IsAnXFF(file))
                    xffPaths.Add(file);

            // check for main executable
            if (info.Name.Contains("SCPS") || info.Name.Contains("SCUS") || info.Name.Contains("SCES"))
                MainExecutablePath = file;
        }

        if (xffPaths.Count == 0)
        {
            Console.WriteLine("No XFF files found in input directory");
            return;
        }

        if (MainExecutablePath == null)
        {
            Console.WriteLine("Could not find main executable in input directory");
            return;
        }

        MainElf = new ELF(MainExecutablePath);
        Xffs = new XFF[xffPaths.Count];
        XffPaths = xffPaths.ToArray();

        for (int i = 0; i < Xffs.Length; i++)
        {
            Xffs[i] = new XFF(xffPaths[i]);
        }

        if (!Directory.Exists(OutputDirectory))
            Directory.CreateDirectory(OutputDirectory);

        bool force = false;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-f" || args[i] == "--force")
            {
                force = true;
                break;
            }
        }

        for (int i = 2; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-p":
                    PatchAction();
                    break;
                case "--patch":
                    PatchAction();
                    break;

                case "-s":
                    CreateSplits(force);
                    break;
                case "--split":
                    CreateSplits(force);
                    break;

                case "-ws":
                    WriteSectionSplits();
                    break;
                case "--write-sections":
                    WriteSectionSplits();
                    break;

                case "-d":
                    DisassembleAction();
                    break;
                case "--disassemble":
                    DisassembleAction();
                    break;

                case "-de":
                    // Split[] splits = Splitter.LoadSplitsFromFile($"{OutputDirectory}/Main ELF/Splits.txt", GetElfSectionNames());
                    // Split[] ghidraSplits = Splitter.FromGhidra($"{OutputDirectory}/Main ELF/GhidraFunctions.txt");
                    // splits = Splitter.MergeSplits(splits, ghidraSplits);

                    // Split[] newSplits = Splitter.UpdateSplits(splits, MainElf);
                    // Splitter.WriteSplitsToFile($"{OutputDirectory}/Main ELF/New Splits.txt", splits, GetElfSectionNames());
                    // Register.WriteRegisterName = false;

                    // MergeFuncSigsToSplits();
                    DisassembleELF($"{OutputDirectory}/Main ELF/Splits.txt");
                    break;
            }
        }
    }

    public static string[] GetElfSectionNames()
    {
        return (from x in MainElf.SectionHeaders select x.Name).ToArray();
    }

    private static string[] GetXffSectionNames(XFF xff)
    {
        return (from x in xff.SectionHeaders select x.Name).ToArray();
    }

    private static void PatchAction()
    {
        MegaXFFer.CreatePatchedFile(XffPaths, MainExecutablePath, OutputDirectory);
    }

    private static void DisassembleAction()
    {
        for (int i = 0; i < XffPaths.Length; i++)
        {
            Disassemble(Xffs[i], XffPaths[i], GetSplitPath(XffPaths[i], OutputDirectory), MainExecutablePath, OutputDirectory);
        }
        DisassembleELF($"{OutputDirectory}/Main ELF/Splits.txt");

    }

    private static bool GetSectionIndex(ELF elf, string name, out int section)
    {
        for (int i = 0; i < elf.SectionHeaders.Length; i++)
        {
            if (elf.SectionHeaders[i].Name == name)
            {
                section = i;
                return true;
            }
        }

        section = -1;
        return false;
    }

    private static string GetSplitPath(string xffPath, string OutputDirectory)
    {
        FileInfo xffInfo = new FileInfo(xffPath);


        string outputSplitPath = $"{OutputDirectory}/{xffInfo.Name.Replace(xffInfo.Extension, "")}";
        if (!Directory.Exists(outputSplitPath))
            Directory.CreateDirectory(outputSplitPath);

        return $"{outputSplitPath}/Splits.txt";
    }

    private static void CreateSplits(bool force = false)
    {
        for (int i = 0; i < XffPaths.Length; i++)
        {
            string outSplitPath = GetSplitPath(XffPaths[i], OutputDirectory);
            if (File.Exists(outSplitPath) && !force)
            {
                Console.WriteLine($"Not updating splits at path {outSplitPath}. To do so, use -f or --force");
                continue;
            }

            Console.WriteLine($"Creating splits for xff {XffPaths[i]}");
            Split[] splits = Splitter.CreateXFFSplits(XffPaths[i]);


            Splitter.WriteSplitsToFile(outSplitPath, splits, GetXffSectionNames(Xffs[i]));
        }

        string mainElfSplitPath = $"{OutputDirectory}/Main ELF/Splits.txt";
        if (File.Exists(mainElfSplitPath) && !force)
        {
            Console.WriteLine($"Not updating splits at path {mainElfSplitPath}. To do so, use -f or --force");
            return;
        }

        Split[] elfSplits = Splitter.CreateELFSplits(Xffs, MainElf);
        Splitter.WriteSplitsToFile(mainElfSplitPath, elfSplits, GetElfSectionNames());
    }

    private static void WriteSectionSplits()
    {
        // Write split sections
        string mainElfSplitPath = $"{OutputDirectory}/Main ELF/Splits.txt";
        Split[] elfSplits = Splitter.LoadSplitsFromFile(mainElfSplitPath, GetElfSectionNames(), out Dictionary<string, FunctionDefinition> functions);

        void WriteSectionToFile(string sectionName, string StructName)
        {
            if (!Directory.Exists($"{OutputDirectory}/Main ELF/Sections/"))
                Directory.CreateDirectory($"{OutputDirectory}/Main ELF/Sections/");

            if (GetSectionIndex(MainElf, sectionName, out int elfDataSection))
            {
                string elfDataHeader = CreateDataSectionHeader(elfSplits, MainElf.SectionHeaders[elfDataSection].Size, elfDataSection, StructName);
                File.WriteAllText($"{OutputDirectory}/Main ELF/Sections/{StructName}.h", elfDataHeader);
            }
            else
            {
                Console.WriteLine($"Failed to get section with name {sectionName}");
            }
        }

        StringBuilder masterSectionInclude = new StringBuilder();
        for (int i = 0; i < MainElf.SectionHeaders.Length; i++)
        {
            ELF.SectionHeader header = MainElf.SectionHeaders[i];
            string sectionName = $"{header.Name.TrimStart('.').Replace('.', '_').ToUpperInvariant()}Section";
            WriteSectionToFile(header.Name, sectionName);
            masterSectionInclude.AppendLine($"#include \"{sectionName}.h\"");
        }

        File.WriteAllText($"{OutputDirectory}/Main ELF/Sections/All.h", masterSectionInclude.ToString());
    }

    private static void Disassemble(XFF xff, string xffPath, string splitsPath, string mainExecutablePath, string OutputDirectory)
    {
        Split[] splits = Splitter.LoadSplitsFromFile(splitsPath, GetXffSectionNames(xff), out Dictionary<string, FunctionDefinition> functions);
        FileInfo xffFileInfo = new FileInfo(xffPath);
        Console.WriteLine($"Disassembling xff {xffFileInfo.Name}: Splits {splitsPath}");

        string outputDisassemblyDirectory = $"{OutputDirectory}/{xffFileInfo.Name.Replace(xffFileInfo.Extension, "")}/Disassembly";
        if (!Directory.Exists(outputDisassemblyDirectory))
            Directory.CreateDirectory(outputDisassemblyDirectory);

        // Get all relocations within this symbol
        for (int i = 0; i < xff.SectionHeaders.Length; i++)
        {
            List<Relocation> relocations = GetRelocationsInSection(xff, i);
            DisassembleSection(xff.SectionHeaders[i].Data, i, splits, functions, outputDisassemblyDirectory, relocations, xff.Symbols);
        }

    }

    private static List<Relocation> CreateRelocationsForELFSection(Split[] splits, int sectionIndex, out List<Symbol> symbols)
    {

        bool TryGetRelocationData(uint memoryAddress, List<Symbol> symbols, out int symbolIndex)
        {
            int section = -1;
            long sectionOffset = -1;
            symbolIndex = -1;

            // Find the section
            Dictionary<string, int> nameToSectionIndex = new Dictionary<string, int>();
            for (int i = 0; i < MainElf.SectionHeaders.Length; i++)
            {
                nameToSectionIndex.Add(MainElf.SectionHeaders[i].Name, i);
            }

            for (int i = 0; i < MainElf.SectionHeaders.Length; i++)
            {
                var header = MainElf.SectionHeaders[i];

                if (memoryAddress < header.MemoryAddress + header.Size && memoryAddress >= header.MemoryAddress)
                {
                    section = i;
                    sectionOffset = memoryAddress - header.MemoryAddress;
                    break;
                }
            }

            if (sectionOffset < 0)
                return false;

            for (int i = 0; i < symbols.Count; i++)
            {
                if (symbols[i].section == section && symbols[i].offsetAddress == sectionOffset)
                {
                    symbolIndex = i;
                    return true;
                }
            }

            Console.WriteLine($"Failed to get symbol at offset 0x{sectionOffset:X} in section {MainElf.SectionHeaders[section].Name}");
            // symbols.Add(new Symbol
            // {
            //     name = $"{MainElf.SectionHeaders[section].Name.TrimStart('.')}_field_0x{sectionOffset:X}",
            //     section = (ushort)section,
            //     offsetAddress = (int)sectionOffset,
            // });
            // symbolIndex = symbols.Count - 1;
            // return true;

            return false;
        }

        List<Relocation> relocations = new List<Relocation>();
        symbols = new List<Symbol>();

        // Convert each valid split into a symbol
        for (int i = 0; i < splits.Length; i++)
        {
            // if (splits[i].section != sectionIndex)
            //     continue;

            symbols.Add(new Symbol
            {
                name = splits[i].Name,
                section = (ushort)splits[i].section,
                offsetAddress = splits[i].start,
            });
        }
        // Go through each instruction in the section and determin if it needs to be relocated
        for (int i = 0; i < MainElf.SectionHeaders[sectionIndex].Data.Length / 4; i++)
        {
            uint instructionData = BitConverter.ToUInt32(MainElf.SectionHeaders[sectionIndex].Data, i * 4);
            Instruction instruction = Dissasembler.GetInstruction(instructionData);

            // Skip invalid instructions
            if (instruction == null)
                continue;

            // Simple jumps
            if (instruction is JumpInstruction)
            {
                JumpInstruction jumpInstruction = (JumpInstruction)instruction;
                if (TryGetRelocationData(jumpInstruction.jumpAddress << 2, symbols, out int symbolIndex))
                {
                    // Console.WriteLine($"Creating relocation for address 0x{jumpInstruction.jumpAddress << 2:X} to symbol {symbols[symbolIndex].name}");
                    // Symbol symbol = symbols[symbolIndex];
                    relocations.Add(new Relocation
                    {
                        offset = i * 4,
                        packedSymbolIndex = 2 | (symbolIndex << 8),
                    });
                    continue;
                }
            }

            if (instruction.Name == "lui")
            {
                // Search forward for addiu
                const int MaxSearchLength = 0x20;
                for (int j = i + 1; j < MainElf.SectionHeaders[sectionIndex].Data.Length / 4 && j - i < MaxSearchLength; j++)
                {
                    uint nextInstructionData = BitConverter.ToUInt32(MainElf.SectionHeaders[sectionIndex].Data, j * 4);
                    Instruction nextInstruction = Dissasembler.GetInstruction(nextInstructionData);
                    if (nextInstruction == null)
                        continue;

                    if (nextInstruction.Name != "addiu")
                        continue;

                    ImmediateInstruction lui = (ImmediateInstruction)instruction;
                    ImmediateInstruction addiu = (ImmediateInstruction)nextInstruction;

                    // Any time an object is accessed with lui
                    // addiu is used and both RS and RT are the same
                    if (lui.RT != addiu.RS)
                        continue;

                    // EXTRACT the address from these instructions 
                    uint address = (uint)((lui.Immediate << 0x10) + addiu.Immediate);
                    if (!TryGetRelocationData(address, symbols, out int symbolIndex))
                        continue;

                    // Add a relocation for the upper 16 bits (lui)
                    relocations.Add(new Relocation
                    {
                        offset = i * 4,
                        packedSymbolIndex = 0x5 | (symbolIndex << 8),
                    });

                    // Then add another for lower 16 (addiu)
                    relocations.Add(new Relocation
                    {
                        offset = j * 4,
                        packedSymbolIndex = 0x6 | (symbolIndex << 8),
                    });

                    break;
                }
            }
        }

        return relocations;
    }

    private static void DisassembleELF(string splitsPath)
    {
        string disassemblyPath = $"{OutputDirectory}/Main ELF/Disassembly";
        if (!Directory.Exists(disassemblyPath))
            Directory.CreateDirectory(disassemblyPath);

        Split[] splits = Splitter.LoadSplitsFromFile(splitsPath, GetElfSectionNames(), out Dictionary<string, FunctionDefinition> functions);
        for (int i = 0; i < MainElf.SectionHeaders.Length; i++)
        {
            var header = MainElf.SectionHeaders[i];
            if (header.Name != ".text")
                continue;

            if (header.Type == ELF.SectionHeader.SH_Type.NoBits || header.Type == ELF.SectionHeader.SH_Type.NULL)
                continue;


            List<Relocation> relocations = CreateRelocationsForELFSection(splits, i, out List<Symbol> symbols);
            DisassembleSection(header.Data, i, splits, functions, disassemblyPath, relocations, symbols.ToArray());
        }

        Directory.CreateDirectory($"{OutputDirectory}/Main ELF/SectionData");
        FileStream sectionsMetaStream = new FileStream($"{OutputDirectory}/Main ELF/SectionData/SectionMeta.bin", FileMode.Create);
        BinaryWriter bw = new BinaryWriter(sectionsMetaStream);

        for (int i = 0; i < MainElf.SectionHeaders.Length; i++)
        {
            var section = MainElf.SectionHeaders[i];

            // Skip over sectiosn without data
            if (section.Type == ELF.SectionHeader.SH_Type.NoBits)
                continue;

            string path = $"{OutputDirectory}/Main ELF/SectionData/{MainElf.SectionHeaders[i].Name.TrimStart('.').ToLower()}.bin";
            File.WriteAllBytes(path, MainElf.SectionHeaders[i].Data);
            Console.WriteLine($"Wrote main elf section to {path}");

            bw.Write(section.Size);
            bw.Write(section.MemoryAddress);
            bw.Write(Encoding.UTF8.GetBytes((section.Name.ToLower().TrimStart('.') + ".bin").PadRight(32, '\x0')));
        }

        sectionsMetaStream.Close();
        Console.WriteLine($"Disassembling Main Elf");


        // Create master function list
        string[] functionListIncludes = ["\"reimplemented/Threads/Threading.h\""];

        StringBuilder functionHeader = new StringBuilder();
        functionHeader.AppendLine("#ifndef __SHADOW_FUNCTIONS__");
        functionHeader.AppendLine("#define __SHADOW_FUNCTIONS__");

        functionHeader.AppendLine();
        // Add includes
        foreach (var include in functionListIncludes)
        {
            functionHeader.AppendLine($"#include {include}");   
        }

        functionHeader.AppendLine();

        List<string> addedDefinitions = new List<string>();
        for (int i = 0; i < splits.Length; i++)
        {
            // if (!string.IsNullOrEmpty(splits[i].functionDefinition.name))
            //     continue;
            if (MainElf.SectionHeaders[splits[i].section].Name != ".text")
                continue;

            if (!splits[i].functionDefinition.Link)
                continue;

            if (addedDefinitions.Contains(splits[i].GetFunctionDefinition()))
                continue;
            addedDefinitions.Add(splits[i].GetFunctionDefinition());

            functionHeader.AppendLine($"{splits[i].GetFunctionDefinition()};");
        }

        functionHeader.AppendLine("#endif");
        File.WriteAllText($"{OutputDirectory}/Main ELF/Disassembly/src/FunctionList.h", functionHeader.ToString());
    }

    private static string CreateDataSectionHeader(Split[] splits, int sectionLength, int dataSectionIndex, string structName)
    {
        int lastAddress = 0;

        var orderedSplits = (from x in splits orderby x.start ascending select x).ToArray();

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("struct {");

        void WriteField(string name, string type, int startAddress, int size)
        {
            if (!string.IsNullOrEmpty(name))
                name = name.Replace("-", "_");

            string arrayData = "";
            if (type != null)
                if (type.Contains('[') && type.Contains(']'))
                {
                    string[] typeSplits = type.Split('[');
                    arrayData = $"[{typeSplits[1].Split(']')[0]}]";
                    type = typeSplits[0];
                }

            // Name is null
            // Type is null
            if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(type))
                sb.AppendLine($"\tchar field_0x{startAddress:X}[0x{size:X}]; // {lastAddress:X} - {lastAddress + size:X}");

            // Name is null
            // Type Exists
            if (string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(type))
                sb.AppendLine($"\t{type} field_0x{startAddress:X}{arrayData}; // {startAddress:X} - {startAddress + size:X}");

            // Name exists
            // Type is null
            if (!string.IsNullOrEmpty(name) && string.IsNullOrEmpty(type))
                sb.AppendLine($"\tchar {name}[0x{size:X}]; // {startAddress:X} - {startAddress + size:X}");

            // Name and type exist
            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(type))
                sb.AppendLine($"\t{type} {name}{arrayData}; // {startAddress:X} - {startAddress + size:X}");

        }

        void AddSplit(Split split, int start)
        {
            // Write the gaps
            int gapSize = start - lastAddress;
            if (gapSize > 0)
            {
                WriteField("", "", lastAddress, gapSize);
            }

            if (split.Equals(default(Split)))
                return;

            WriteField(split.Name, split.type, split.start, split.length);


            lastAddress = split.start + split.length;
        }

        for (int i = 0; i < orderedSplits.Length; i++)
        {
            // Go through each split
            Split split = orderedSplits[i];

            // Only proccess it if it is in the target section
            if (split.section != dataSectionIndex)
                continue;

            AddSplit(split, split.start);
        }

        AddSplit(default(Split), sectionLength);

        sb.AppendLine($"{'}'} typedef {structName}; // Section length: 0x{sectionLength:X}");
        sb.AppendLine();
        sb.AppendLine($"extern {structName} _{structName};");

        return sb.ToString();
    }

    private static Instruction[] GetInstructions(byte[] functionData)
    {
        Instruction[] instructions = new Instruction[functionData.Length / 4];
        for (int i = 0; i < instructions.Length; i++)
        {
            uint instructionData = BitConverter.ToUInt32(functionData, i * 4);
            instructions[i] = Dissasembler.GetInstruction(instructionData);
        }

        return instructions;
    }

    private static int[] GetBranchOffsets(Instruction[] instructions)
    {
        List<int> branchOffsets = new List<int>();
        for (int i = 0; i < instructions.Length; i++)
        {
            Instruction instruction = instructions[i];
            if (instruction is ImmediateInstruction)
            {
                ImmediateInstruction immediate = (ImmediateInstruction)instruction;

                if (immediate.format == ImmediateInstruction.Format.BranchRs || immediate.format == ImmediateInstruction.Format.BranchRsRt)
                {
                    branchOffsets.Add(i + immediate.Immediate + 1); // Add 1 because its relative to the next instruction
                }
            }

            if (instruction is RegimmInstruction)
            {
                RegimmInstruction regimm = (RegimmInstruction)instruction;
                if (regimm.format == RegimmInstruction.Format.BranchRsOffset)
                {
                    branchOffsets.Add(i + regimm.Immediate + 1); // Add 1 because its relative to the next instruction
                    continue;
                }
            }
        }

        return branchOffsets.ToArray();
    }

    private static void DisassembleSection(byte[] sectionData, int sectionIndex, Split[] splits, Dictionary<string, FunctionDefinition> functions, string OutputDirectory, List<Relocation> relocations, Symbol[] symbols)
    {

        StringBuilder compileScript = new StringBuilder();
        const string CompilerPath = "gcc";
        compileScript.AppendLine("#!/bin/bash");
        compileScript.Append($"{CompilerPath} -O2 -c -w -g3 ");
        StringBuilder linkScript = new StringBuilder();
        linkScript.AppendLine("#!/bin/bash");
        linkScript.Append($"{CompilerPath} ");

        Directory.CreateDirectory($"{OutputDirectory}/src/reiplemented  ");
        Directory.CreateDirectory($"{OutputDirectory}/src/auto_generated");
        Directory.CreateDirectory($"{OutputDirectory}/src/asm");
        for (int i = 0; i < splits.Length; i++)
        {
            Split split = splits[i];


            if (split.section == 0 || split.section == 0xfff1)
                continue;

            if (split.section != sectionIndex) // Temporary but its for .text sections only
                continue;

            // Skip symbol if it isn't a function
            // if (((int)symbol.flags & 0xf) != (int)Symbol.Flags.Function)
            //     continue;

            if (split.length <= 0)
            {
                Console.WriteLine($"Invalid split: {split.Name}");
                continue;
            }

            if (split.functionDefinition.Link)
                linkScript.Append($"{split.Name}.o ");

            if (split.functionDefinition.Compile)
                compileScript.Append($"{split.Name}.c ");

            // Don't spit out new c file if not needed
            if (!split.functionDefinition.GenerateC)
                continue;
            // if (split.Name.Contains("00100a58"))
            // Console.WriteLine("Hell");

            byte[] functionData = sectionData.AsSpan(split.start, split.length).ToArray();
            var relocationsInFunction = GetRelocationsInFunction(relocations, split, sectionData, symbols, true);
            Instruction[] instructions = GetInstructions(functionData);
            int[] branchOffsets = GetBranchOffsets(instructions);
            string disassembledText = FunctionDataToDisassembly(split, instructions, branchOffsets, relocationsInFunction);

            string returnType = "void";
            if (split.functionDefinition != null)
                returnType = split.functionDefinition.ReturnType;

            CFile cFile = new CFile(split, instructions, relocationsInFunction);
            cFile.Write($"{OutputDirectory}/src/auto_generated/{split.Name}.c", functions);


            File.WriteAllText($"{OutputDirectory}/src/asm/{split.Name}.S", disassembledText);
        }

        compileScript.Append(" $@");
        linkScript.Append(" $@ -o main.elf");

        // File.WriteAllText($"{OutputDirectory}/build.sh", compileScript.ToString());
        // File.WriteAllText($"{OutputDirectory}/link.sh", linkScript.ToString());
        Console.WriteLine($"Creating build script at {OutputDirectory}");
    }

    private static string FunctionDataToDisassembly(Split split, Instruction[] instructions, int[] branchOffsets, List<Tuple<int, string>> relocatedSymbols)
    {
        if (split.Name.Contains("loaderLoop"))
            Console.WriteLine("HELP");
        StringBuilder sb = new StringBuilder(instructions.Length * 0x10);
        sb.AppendLine(".section .text");
        sb.AppendLine(".set noat");
        sb.AppendLine();
        sb.AppendLine($"{split.Name}:");

        for (int i = 0; i < instructions.Length; i++)
        {
            // Check if we need to write a branch offset
            for (int j = 0; j < branchOffsets.Length; j++)
            {
                if (branchOffsets[j] == i)
                {
                    sb.AppendLine();
                    sb.AppendLine($"$Branch_0x{i * 4:X}:");
                    break;
                }
            }

            Instruction instruction = instructions[i];

            // Check if there is a relocation applied to this address
            bool hasAppliedRelocation = false;
            for (int r = 0; r < relocatedSymbols.Count; r++)
            {
                if (relocatedSymbols[r].Item1 == i)
                {
                    if (hasAppliedRelocation)
                        Console.WriteLine($"ERROR: APPLYING MULTIPLE RELOCATIONS AT SAME ADDRESS");

                    hasAppliedRelocation = true;

                    string line = $"\t{instruction.ToString(relocatedSymbols[r].Item2)}";
                    if (line.Contains("%"))
                    {
                        int lastSymbol = line.IndexOf("$%");
                        line = line.Remove(lastSymbol, 1);
                    }
                    sb.AppendLine(line);
                }
            }

            // Do nothing if we already applied a relocation
            if (hasAppliedRelocation)
                continue;

            // Try changing branch instructions
            if (instruction is ImmediateInstruction)
            {
                ImmediateInstruction imm = (ImmediateInstruction)instruction;
                if (imm.format == ImmediateInstruction.Format.BranchRs || imm.format == ImmediateInstruction.Format.BranchRsRt)
                {
                    string line = $"\t{imm.ToString($"Branch_0x{(i + imm.Immediate + 1) * 4:X}")}";
                    sb.AppendLine(line);
                    continue;
                }
            }

            if (instruction is RegimmInstruction)
            {
                RegimmInstruction regimm = (RegimmInstruction)instruction;
                if (regimm.format == RegimmInstruction.Format.BranchRsOffset)
                {
                    sb.AppendLine($"\t{instruction.ToString($"Branch_0x{(i + (short)regimm.Immediate + 1) * 4:X}")}");
                    continue;
                }
            }

            sb.AppendLine($"\t{instruction}");
        }

        return sb.ToString();
    }

    private static string GetRelocationName(byte[] sectionData, List<Relocation> relocations, int relocationIndex, Symbol relocatedSymbol, Symbol[] symbols)
    {
        string symbolName = relocatedSymbol.name;


        // Return the symbol name if it is not a section
        // if (relocatedSymbol.flags != Symbol.Flags.Section)
        //     return symbolName;

        // Find the address of the relocation
        Relocation relocation = relocations[relocationIndex];
        if (relocation.Type == Relocation.RelocationType.Low16)
        {
            uint offset = BitConverter.ToUInt32(sectionData.AsSpan(relocation.offset, 4)) & 0xffff;
            if (offset > 0 && relocatedSymbol.flags == Symbol.Flags.Section)
                return $"%lo({symbolName.TrimStart('.')}+0x{offset:X})";
            else
                return $"%lo({symbolName.TrimStart('.')})";
        }

        if (relocation.Type != Relocation.RelocationType.High16)
            return $"{symbolName}";

        // Find the low 16
        int nextRelocationIndex = relocationIndex + 1;
        while (relocations[nextRelocationIndex].Type == Relocation.RelocationType.High16)
        {
            nextRelocationIndex++;
        }

        Relocation low16Relocation = relocations[nextRelocationIndex];
        Symbol low16Symbol = symbols[relocations[nextRelocationIndex].SymbolIndex];

        uint low16Data = BitConverter.ToUInt32(sectionData.AsSpan(low16Relocation.offset, 4));
        uint low16Offset = low16Data & 0xffff;

        uint high16Data = BitConverter.ToUInt32(sectionData.AsSpan(relocation.offset, 4));
        uint high16Offset = high16Data & 0xffff;

        uint totalOffset = low16Offset + high16Offset * 0x10000;
        if (totalOffset > 0 && relocatedSymbol.flags == Symbol.Flags.Section)
            return $"%hi({symbolName.TrimStart('.')}+0x{totalOffset:X})";
        else return $"%hi({symbolName.TrimStart('.')})";
    }

    private static List<Relocation> GetRelocationsInSection(XFF xff, int section)
    {
        List<Relocation> relocations = new List<Relocation>();
        for (int i = 0; i < xff.RelocationHeaders.Length; i++)
        {
            if (xff.RelocationHeaders[i].sectionIndex != section)
                continue;

            relocations.AddRange(xff.RelocationHeaders[i].relocations);
        }

        return relocations;
    }

    private static List<Tuple<int, string>> GetRelocationsInFunction(List<Relocation> relocationsInSection, Split split, byte[] sectionData, Symbol[] symbols, bool onlyFunctions = false)
    {
        // Get the start and end points of the function
        int startPos = split.start;
        int endPos = startPos + split.length;

        // Go through each relocation
        List<Tuple<int, string>> relocations = new List<Tuple<int, string>>();
        for (int i = 0; i < relocationsInSection.Count; i++)
        {
            // Check if the relocation is applied within the start and end of the function
            Relocation relocation = relocationsInSection[i];

            if (MainElf.SectionHeaders[symbols[relocation.SymbolIndex].section].Name != ".text" && onlyFunctions)
                continue;

            int relocationAddress = relocation.offset;

            if (relocationAddress >= startPos && relocationAddress <= endPos)
            {
                string relocationName = GetRelocationName(sectionData, relocationsInSection, i, symbols[relocation.packedSymbolIndex >> 8], symbols);
                relocations.Add(new Tuple<int, string>((relocationAddress - startPos) / 4, relocationName));
            }
        }



        return relocations;
    }
}