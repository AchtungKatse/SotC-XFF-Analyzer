using System.Text;

public interface IExecutableInfo
{
    // Metadata
    public string Name { get; set; }
    public string[] SectionNames { get; set; }
    public Split[]? Splits { get; set; }
    public Relocation[] Relocations { get; set; }
    public Dictionary<string, FunctionDefinition> FunctionDefinitions { get; set; }
    public Function[] Functions { get; set; }
    public int TextSectionIndex { get; set; }
}

public class Executable<SectionHeader> : IExecutableInfo where SectionHeader : ISectionHeader
{
    // Metadata
    public string Name { get; set; } = "Executable";
    public string[] SectionNames { get; set; } = [];
    public Split[]? Splits { get; set; } = [];
    public Relocation[] Relocations { get; set; } = [];
    public Dictionary<string, FunctionDefinition> FunctionDefinitions { get; set; } = [];
    public Function[] Functions { get; set; } = [];

    public SectionHeader[] SectionHeaders { get; set; } = [];

    public int TextSectionIndex { get; set; }
    public SectionHeader TextSection => SectionHeaders[TextSectionIndex];

    public void LoadSplitsFromFile(string splitsPath, bool findNewRelocations = false)
    {
        // Load the splits and functions
        Splits = Splitter.LoadSplitsFromFile(splitsPath, SectionNames, out Dictionary<string, FunctionDefinition> functions);

        if (Splits == null)
        {
            Debug.LogCritical("Failed to get splits for executable");
            return;
        }

        FunctionDefinitions = functions;

        // Find all the relocations
        if (findNewRelocations)
            Relocations = CreateRelocations(TextSectionIndex).ToArray();

        // Create all functions using above data
        GetFunctions();
    }

    private bool TryGetRelocationData(uint memoryAddress, out int splitIndex)
    {
        int section = -1;
        long sectionOffset = -1;
        splitIndex = -1;

        // Do nothing if the executable has no splits
        if (Splits == null)
        {
            Debug.LogCritical("Cannot get relocation data without splits");
            return false;
        }

        // Find the section that the memory address is in
        // Create a map from section name to its index (for some reason?)
        Dictionary<string, int> nameToSectionIndex = new Dictionary<string, int>();
        for (int i = 0; i < SectionHeaders.Length; i++)
        {
            nameToSectionIndex.Add(SectionHeaders[i].Name, i);
        }

        // Go through each section then check if the memory address is within its bounds
        for (int i = 0; i < SectionHeaders.Length; i++)
        {
            var header = SectionHeaders[i];

            if (memoryAddress < header.MemoryAddress + header.Length && memoryAddress >= header.MemoryAddress)
            {
                // If so capture the section index and the offset of the relocation relative to the start of the section
                section = i;
                sectionOffset = memoryAddress - header.MemoryAddress;
                break;
            }
        }

        // Return nothing if it failed to get a valid section
        if (sectionOffset < 0)
            return false;

        // Check if there is a split at the target position
        for (int i = 0; i < Splits.Length; i++)
        {
            if (Splits[i].Section == section && Splits[i].StartAddress == sectionOffset)
            {
                // If so that's the target split
                splitIndex = i;
                return true;
            }
        }

        Debug.LogWarn($"Dissasembler failed to get symbol at offset 0x{sectionOffset:X} in section {SectionHeaders[section].Name}");
        return false;
    }

    private List<Relocation> CreateRelocations(int sectionIndex)
    {
        List<Relocation> relocations = new List<Relocation>();

        // Go through each instruction in the section and determin if it needs to be relocated
        for (int i = 0; i < SectionHeaders[sectionIndex].Data.Length / 4; i++)
        {
            uint instructionData = BitConverter.ToUInt32(SectionHeaders[sectionIndex].Data, i * 4);
            Instruction instruction = Dissassembler.GetInstruction(instructionData);

            // Skip invalid instructions
            if (instruction == null)
                continue;

            // Simple jumps
            if (instruction is JumpInstruction)
            {
                JumpInstruction jumpInstruction = (JumpInstruction)instruction;
                if (TryGetRelocationData(jumpInstruction.jumpAddress << 2, out int symbolIndex))
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
                for (int j = i + 1; j < SectionHeaders[sectionIndex].Data.Length / 4 && j - i < MaxSearchLength; j++)
                {
                    uint nextInstructionData = BitConverter.ToUInt32(SectionHeaders[sectionIndex].Data, j * 4);
                    Instruction nextInstruction = Dissassembler.GetInstruction(nextInstructionData);
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
                    if (!TryGetRelocationData(address, out int symbolIndex))
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

    private void GetFunctions()
    {
        // Null check
        if (Splits == null)
        {
            Debug.LogCritical($"Executable {Name} does not have any splits.");
            return;
        }

        // Temporary linked list of functions
        List<Function> functions = new List<Function>();

        // Go through each split and look for a valid function
        for (int i = 0; i < Splits.Length; i++)
        {
            Split split = Splits[i];

            // Invalid section or static function
            if (split.Section == 0 || split.Section == 0xfff1)
                continue;

            if (split.Section != TextSectionIndex)
                continue;

            // Skip symbol if it isn't a function
            // if (((int)symbol.flags & 0xf) != (int)Symbol.Flags.Function)
            //     continue;

            if (split.Length <= 0)
            {
                Debug.LogWarn($"Invalid split: {split.Name}");
                continue;
            }

            // Don't spit out new c file if not needed
            if (!split.functionDefinition.GenerateC)
                continue;
            // if (split.Name.Contains("00100a58"))
            // Console.WriteLine("Hell");

            byte[] functionData = TextSection.Data.AsSpan(split.StartAddress, split.Length).ToArray();
            var relocationsInFunction = GetRelocationsInFunction(Relocations, split, Splits);
            Instruction[] instructions = Dissassembler.GetInstructions(functionData);
            int[] branchOffsets = GetBranchOffsets(instructions);

            Function function = new Function
            {
                Instructions = instructions,
                Relocations = relocationsInFunction.ToArray(),
                Splits = Splits,
                split = Splits[i],
                BranchOffsets = branchOffsets
            };

            functions.Add(function);
        }

        Functions = functions.ToArray();
    }

    private static List<IndexedRelocation> GetRelocationsInFunction(Relocation[] relocationsInSection, Split split, Split[] splits)
    {
        // Get the start and end points of the function
        int startPos = split.StartAddress;
        int endPos = startPos + split.Length;

        // Go through each relocation
        List<IndexedRelocation> relocations = new List<IndexedRelocation>();
        for (int i = 0; i < relocationsInSection.Length; i++)
        {
            // Check if the relocation is applied within the start and end of the function
            Relocation relocation = relocationsInSection[i];

            // Make sure that the other split's section is the same as the targets
            if (splits[relocation.SplitIndex].Section != split.Section)
                continue;

            int relocationAddress = relocation.offset;

            if (relocationAddress >= startPos && relocationAddress <= endPos)
            {
                relocations.Add(new IndexedRelocation
                {
                    instructionIndex = (relocationAddress - startPos) / 4,
                    relocation = relocationsInSection[i]
                });
            }
        }

        return relocations;
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

    public void Decompile(string outputDirectory)
    {
        if (Splits == null)
        {
            Debug.LogCritical($"Not decompiling {Name} because it has no splits.");
            return;
        }

        // Get and create output paths
        string decompilationPath = $"{outputDirectory}/Decompilation";
        string sectionMetaPath = $"{outputDirectory}/Decompilation/SectionMeta";
        Directory.CreateDirectory(decompilationPath);
        Directory.CreateDirectory(sectionMetaPath);

        // Create metadata for static decompilation
        FileStream sectionsMetaStream = new FileStream($"{sectionMetaPath}/SectionMeta.bin", FileMode.OpenOrCreate);
        BinaryWriter bw = new BinaryWriter(sectionsMetaStream);

        Debug.LogInfo($"Writing all {Name} sections and metadata.");
        for (int i = 0; i < SectionHeaders.Length; i++)
        {
            var section = SectionHeaders[i];

            // Skip over sectiosn without data
            if (section.Type == ISectionHeader.SH_Type.NoBits)
                continue;

            // Write out all of the section data
            string path = $"{sectionMetaPath}/{SectionHeaders[i].Name.TrimStart('.').ToLower()}.bin";
            File.WriteAllBytes(path, SectionHeaders[i].Data);

            // Append the info to the meta file
            bw.Write(section.Length);
            bw.Write(section.MemoryAddress);
            bw.Write(Encoding.UTF8.GetBytes((section.Name.ToLower().TrimStart('.') + ".bin").PadRight(32, '\x0')));
        }
        sectionsMetaStream.Close();

        // Write all functions to file
        Dissassembler.DecompileFunctions(Functions, decompilationPath, FunctionDefinitions);


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
        for (int i = 0; i < Splits.Length; i++)
        {
            // if (!string.IsNullOrEmpty(Splitsplits[i].functionDefinition.name))
            //     continue;
            if (SectionHeaders[Splits[i].Section].Name != ".text")
                continue;

            if (!Splits[i].functionDefinition.Link)
                continue;

            if (addedDefinitions.Contains(Splits[i].GetFunctionDefinition()))
                continue;
            addedDefinitions.Add(Splits[i].GetFunctionDefinition());

            functionHeader.AppendLine($"{Splits[i].GetFunctionDefinition()};");
        }

        functionHeader.AppendLine("#endif");
        Directory.CreateDirectory($"{outputDirectory}/Decompilation/src");
        File.WriteAllText($"{outputDirectory}/Decompilation/src/FunctionList.h", functionHeader.ToString());
    }

    public void Disassemble(string outputDirectory)
    {
        if (Splits == null)
        {
            Debug.LogCritical($"Not disassembling {Name} because it has no splits.");
            return;
        }

        // Create output directory
        string disassemblyPath = $"{outputDirectory}/Disassembly";
        Directory.CreateDirectory(disassemblyPath);

        // Dissassemble all functions
        Debug.LogInfo($"Disassembling {Functions.Length} functions for {Name}.");
        Dissassembler.DisassembleFunctions(Functions, disassemblyPath);
    }
}