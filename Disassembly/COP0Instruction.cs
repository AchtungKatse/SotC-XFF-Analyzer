public class Cop0Instruction : Instruction
{
    public enum Format { Unimplemented, None, Rs, RsRt, Offset, RtRd, Rt }

    public Format format { get; private set; }
    public Register RS { get; private set; }
    public Register RT { get; private set; }
    public Register RD { get; private set; }
    public short offset;

    public Cop0Instruction(uint data)
    {
        Name = FunctionToName(data);
        RS = (Register)(data >> 21 & 0x1f);
        RT = (Register)(data >> 16 & 0x1f);
        RD = (Register)(data >> 11 & 0x1f);
        offset = (short)(data & 0xffff);
    }

    public override string ToString(string symbol)
    {
        return "Unimplimented";
        switch (format)
        {
            default: return $"{Name}: Unknown format";
            case Format.Rs: return $"{Name} ${RS}";
            case Format.RsRt: return $"{Name} ${RS}, ${RT}";
        }
    }

    public override string ToString()
    {
        switch (format)
        {
            default: return $"{Name}: Unknown format";
            case Format.None: return $"{Name}";
            case Format.Rs: return $"{Name} ${RS}";
            case Format.RsRt: return $"{Name} ${RS}, ${RT}";
            case Format.RtRd: return $"{Name} ${RT}, ${RD}";
            case Format.Rt: return $"{Name} ${RT}";
        }
    }

    private string FunctionToName(uint data)
    {
        uint function = data >> 21 & 0x3f;
        switch (function)
        {
            default: return $"Unknown COP0 Instruction: {function:X}";
            case 0x0: return ReadMT0(data).Replace("MT", "MF");
            case 0x4: return ReadMT0(data);
            case 0x8: return BC0ToName(data);
            case 0x10: return C0ToName(data);
        }
    }

    private string ReadMT0(uint data)
    {
        uint type = data >> 11 & 0x1f;
        uint function = data & 0x1f;
        if (type == 0x18 && (data& 0xfff) != 0)
        {
            format = Format.Rt;
            switch (function)
            {
                case 0x3: return "MTIABM";
                case 0x2: return "MTIAB";
                case 0x7: return "MTDVBM";
                case 0x6: return "MTDVB";
                case 0x5: return "MTDABM";
                case 0x4: return "MTDAB";
                // case 0x0: format = Format.RtRd; return "MTC0";
                case 0x0: return "MTBPC";
            }
        }

        if (type != 0x18 && (data & 0x7ff) == 0)
        {
            format = Format.RtRd;
            return "MTC0";
        }

        if (type == 0x19)
        {
            format = Format.RsRt;
            RS = data >> 16 & 0x1f;
            RT = data >> 1 & 0x1f;
            if ((data & 1) == 1)
                return "MTPC";

            return "MTPS";
        }

        return $"Undefined mfc0 instruction: ${function:X} ${type:X} ${data:X}";

    }

    private string BC0ToName(uint data)
    {
        format = Format.Offset;
        uint function = data >> 16 & 0x3f;
        switch (function)
        {
            default: return $"undefined bc0 function {function:X}";
            case 0x0: return "BC0F";
            case 0x1: return "BC0T";
            case 0x2: return "BC0FL";
            case 0x3: return "BC0TL";
        }
    }

    private string C0ToName(uint data)
    {
        uint function = data & 0x3f;
        format = Format.None;
        switch (function)
        {
            default: return $"Unknown c0 function {function:X}";
            case 0x1: return "TLBR";
            case 0x2: return "TLBWI";
            case 0x6: return "TLBWR";
            case 0x8: return "TLBP";
            case 0x18: return "ERET";
            case 0x38: return "EI";
            case 0x39: return "DI";
        }
    }
    public override string ToCMacro(string branch = "")
    {
        string name = Name.ToUpper().Replace(".", "");
        switch (format)
        {
            default: return $"{name}: Unknown format";
            case Format.None: return $"{name}(ctx)";
            case Format.Rs: return $"{name}(ctx, ctx->{RS})";
            case Format.RsRt: return $"{name}(ctx, ctx->{RS}, ctx->{RT})";
            case Format.RtRd: return $"{name}(ctx, ctx->{RT}, ctx->{RD})";
            case Format.Rt: return $"{name}(ctx->{RT})";
        }
    }
}