public class ImmediateInstruction : Instruction
{
    public short Immediate { get; set; }
    public Register RS { get; set; }
    public Register RT { get; set; }

    public ImmediateInstruction(uint data, int opcode)
    {
        Data = data;

        Immediate = (short)(data & ushort.MaxValue);
        RT = (Register)((data >> 16) & 0x1f);
        RS = (Register)((data >> 21) & 0x1f);
        Name = OpcodeToName(opcode);
    }

    public string ToString(string immediate = "", string rs = "", string rt = "")
    {
        if (immediate == "")
            immediate = Immediate < 0 ? $"-0x{-Immediate:X}" : $"0x{Immediate:X}";

        if (rs == "")
            rs = RS.ToString();
        if (rt == "")
            rt = RT.ToString();

        if (Name == "addiu")
        {
            // if (rs == "zero")
            // return $"mov ${rt}, {immediate}"
            return $"{Name} ${rt}, ${rs}, {immediate}";
        }

        if (Name == "beq")
        {
            immediate = Immediate < 0 ? $"-0x{-Immediate << 2:X}" : $"0x{Immediate << 2:X}";
            return $"{Name} ${rs}, ${rt}, {immediate}";
        }

        if (Name == "ori" || Name == "andi")
            return $"{Name} ${rs}, ${rt}, 0x{(ushort)Immediate:X}";

        if (Name == "lui")
            return $"{Name} ${rt}, {immediate}";

        if (Name == "lwc1")
            return $"{Name} $f{(int)RS}, {Immediate:X}(${rt})";

        if (Name == "MTC1")
            return $"{Name} ${rt}, $f{(int)RS}";

        return $"{Name} ${rt}, {immediate}(${rs})";
    }

    public override string ToString()
    {
        string immText = Immediate < 0 ? $"-0x{-Immediate:X}" : $"0x{Immediate:X}";
        return ToString(immText, RS.ToString(), RT.ToString());
    }

    public override string ToString(string symbol)
    {
        string immText = Immediate < 0 ? $"-0x{-Immediate:X}" : $"0x{Immediate:X}";
        return ToString(immText, symbol, RT.ToString());
    }
}