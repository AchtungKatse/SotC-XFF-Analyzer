// Just contains a relocation for an instruction at an index 
using System.Text;

public struct IndexedRelocation
{
    public Relocation relocation;
    public int instructionIndex;
}

public struct Function
{
    public Split split { get; set; }
    public Instruction[] Instructions { get; set; }
    public IndexedRelocation[] Relocations { get; set; }
    public Split[] Splits { get; set; }
    public int[] BranchOffsets { get; set; }

    public readonly FunctionDefinition Definition => split.functionDefinition;

    public string ToAssembly()
    {
        StringBuilder sb = new StringBuilder(Instructions.Length * 0x10);
        sb.AppendLine(".section .text");
        sb.AppendLine(".set noat");
        sb.AppendLine();
        sb.AppendLine($"{split.Name}:");

        for (int i = 0; i < Instructions.Length; i++)
        {
            // Check if we need to write a branch offset
            for (int j = 0; j < BranchOffsets.Length; j++)
            {
                if (BranchOffsets[j] == i)
                {
                    sb.AppendLine();
                    sb.AppendLine($"$Branch_0x{i * 4:X}:");
                    break;
                }
            }

            Instruction instruction = Instructions[i];

            // Check if there is a relocation applied to this address
            bool hasAppliedRelocation = false;
            for (int r = 0; r < Relocations.Length; r++)
            {
                if (Relocations[r].instructionIndex == i)
                {
                    if (hasAppliedRelocation)
                        Console.WriteLine($"ERROR: APPLYING MULTIPLE RELOCATIONS AT SAME ADDRESS");

                    hasAppliedRelocation = true;

                    string line = $"\t{instruction.ToString(RelocationToAssemblyName(Relocations[r].instructionIndex, r))}";
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

    private string RelocationToAssemblyName(int instructionIndex, int relocationIndex)
    {
        string symbolName = Splits[Relocations[relocationIndex].relocation.SplitIndex].Name;

        // Find the address of the relocation
        Relocation relocation = Relocations[relocationIndex].relocation;
        if (relocation.Type == Relocation.RelocationType.Low16)
        {
            uint offset = Instructions[instructionIndex].Data & 0xffff;
            if (offset > 0 && relocation.offset == 0) // Its a section relocation
                return $"%lo({symbolName.TrimStart('.')}+0x{offset:X})";
            else
                return $"%lo({symbolName.TrimStart('.')})";
        }

        if (relocation.Type != Relocation.RelocationType.High16)
            return $"{symbolName}";

        // Find the low 16
        int nextRelocationIndex = relocationIndex + 1;
        while (Relocations[nextRelocationIndex].relocation.Type == Relocation.RelocationType.High16)
        {
            nextRelocationIndex++;
        }

        Relocation low16Relocation = Relocations[nextRelocationIndex].relocation;
        Split low16Symbol = Splits[Relocations[nextRelocationIndex].relocation.SplitIndex];

        uint low16Data = Instructions[nextRelocationIndex].Data;
        uint low16Offset = low16Data & 0xffff;

        uint high16Data = Instructions[instructionIndex].Data;
        uint high16Offset = high16Data & 0xffff;

        uint totalOffset = low16Offset + high16Offset * 0x10000;
        if (totalOffset > 0 && relocation.offset == 0) // edge case for sections
            return $"%hi({symbolName.TrimStart('.')}+0x{totalOffset:X})";
        else return $"%hi({symbolName.TrimStart('.')})";
    }



}