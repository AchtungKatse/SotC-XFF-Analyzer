public class RegimmInstruction : Instruction
{
    public enum Format { BranchRsOffset, RsOffset };

    public Register RS { get; private set; }
    public short Immediate { get; private set; }
    public Format format { get; private set; }

    public RegimmInstruction(uint data)
    {
        RS = (Register)((data >> 21) & 0x1f);
        Immediate = (short)(data & 0xffff);

        uint function = (uint)((data >> 16) & 0x1f);

        Name = FunctionToName(function);
    }

    private string FunctionToName(uint function)
    {
        switch (function)
        {
            case 0x1: format = Format.BranchRsOffset; return "bgez";
            case 0x11: format = Format.BranchRsOffset; return "bgezal";
            case 0x13: format = Format.BranchRsOffset; return "bgezall";
            case 0x3: format = Format.BranchRsOffset; return "bgezl";
            case 0x0: format = Format.BranchRsOffset; return "bltz";
            case 0x10: format = Format.BranchRsOffset; return "bltzal";
            case 0x12: format = Format.BranchRsOffset; return "bltzall";
            case 0x2: format = Format.BranchRsOffset; return "bltzl";
            case 0xc: format = Format.RsOffset; return "teqi";
            case 0x8: format = Format.RsOffset; return "tgei";
            case 0x9: format = Format.RsOffset; return "tgeiu";
            case 0xa: format = Format.RsOffset; return "tlti";
            case 0xb: format = Format.RsOffset; return "tltiu";
            case 0xe: format = Format.RsOffset; return "tnei";
        }

        return "UNKNOWN REGIMM FUNCTION: " + function;
    }


    public override string ToString(string symbol)
    {
        switch (format)
        {
            case Format.RsOffset:
                return $"{Name} ${RS}, ${symbol}";

            case Format.BranchRsOffset:
                return $"{Name} ${RS}, ${symbol}";
        }

        return $"{Name}: Unknown format";
    }
    public override string ToString()
    {
        short _immediate = (short)Immediate;
        string immText = _immediate < 0 ? $"-0x{-_immediate:X}" : $"0x{_immediate:X}";

        switch (format)
        {
            case Format.RsOffset:
                return $"{Name} ${RS}, {immText}";

            case Format.BranchRsOffset:
                immText = _immediate < 0 ? $"-0x{-_immediate << 2:X}" : $"0x{_immediate << 2:X}";
                return $"{Name} ${RS}, {immText}";
        }

        return $"{Name}: Unknown format";
    }

    public override string ToCMacro(string branch = "")
    {
        switch (format)
        {
            case Format.BranchRsOffset:
                return $"{Name.ToUpper()}(ctx, ctx->{RS}, {branch})";
            // case Format.RsOffset:
            //     break;
        }
        return $"{Name.ToUpper()}(ctx, ctx->{RS}, {branch})";
    }

    public void SetImmediate(short imm)
    {
        Immediate = imm;
    }
}