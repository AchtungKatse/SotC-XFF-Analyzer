using System.Text.Json.Serialization;

public class ImmediateInstruction : Instruction
{
    public enum Format { Unknown, RsRt, RtRs, Rt, RtOffsetBase, BranchRsRt, BranchRs, Cache};

    public short Immediate { get; set; }
    public Register RS { get; set; }
    public Register RT { get; set; }
    public Format format { get; private set; }


    public ImmediateInstruction(uint data, int opcode)
    {
        Data = data;

        Immediate = (short)(data & ushort.MaxValue);
        RT = (Register)((data >> 16) & 0x1f);
        RS = (Register)((data >> 21) & 0x1f);
        Name = OpcodeToName(opcode);
        format = OpcodeToFormat(opcode);
    }


    private static Format OpcodeToFormat(int opcode)
    {
        switch (opcode)
        {
            default: return Format.Unknown;
            // case 0x0: return "WRONG REGISTER TYPE";
            // case 0x1: return "UNDEFINED REGIMM";
            // case 0x2: return "j";
            // case 0x3: return "jal";

            case 0x4: return Format.BranchRsRt;
            case 0x5: return Format.BranchRsRt;
            case 0x6: return Format.BranchRs;
            case 0x7: return Format.BranchRs;
            case 0x8: return Format.RtRs;
            case 0x9: return Format.RtRs;
            case 0xA: return Format.RtRs;
            case 0xB: return Format.RtRs;
            case 0xC: return Format.RtRs;
            case 0xD: return Format.RtRs;
            case 0xE: return Format.RtRs;
            case 0xF: return Format.Rt;
            // case 0x10: return "mfc0";
            // case 0x11: return GetFPUOpcodeToName(data);
            // case 0x11: return "Unknown FPU OPCODE";
            case 0x14: return Format.BranchRsRt;
            case 0x15: return Format.BranchRsRt;
            case 0x16: return Format.BranchRs;
            case 0x17: return Format.BranchRs;
            case 0x18: return Format.RtRs;
            case 0x19: return Format.RtRs;
            case 0x1a: return Format.RtOffsetBase;
            case 0x1b: return Format.RtOffsetBase;
            case 0x1e: return Format.RtOffsetBase;
            case 0x1f: return Format.RtOffsetBase;
            case 0x20: return Format.RtOffsetBase;
            case 0x21: return Format.RtOffsetBase;
            case 0x22: return Format.RtOffsetBase;
            case 0x23: return Format.RtOffsetBase;
            case 0x24: return Format.RtOffsetBase;
            case 0x25: return Format.RtOffsetBase;
            case 0x26: return Format.RtOffsetBase;
            case 0x27: return Format.RtOffsetBase;
            case 0x28: return Format.RtOffsetBase;
            case 0x29: return Format.RtOffsetBase;
            case 0x2a: return Format.RtOffsetBase;
            case 0x2b: return Format.RtOffsetBase;
            case 0x2c: return Format.RtOffsetBase;
            case 0x2d: return Format.RtOffsetBase;
            case 0x2e: return Format.RtOffsetBase;
            case 0x2f: return Format.Cache;
            // case 0x31: return "lwc1";
            // case 0x33: return "pref";
            case 0x37: return Format.RtOffsetBase;
            case 0x3f: return Format.RtOffsetBase;
        }
    }

    private string ToString(string immediate = "", string rs = "", string rt = "")
    {
        int im = (int)Immediate;
        if (format == Format.BranchRsRt || format == Format.BranchRs)
            im <<= 2;

        bool convertToNegative = true;
        // if (Name == "ori" || Name == "andi" || Name == "xori")
        //     convertToNegative = false;


        if (immediate == "")
        {
            if (convertToNegative)
                immediate = im < 0 ? $"-0x{-im:X}" : $"0x{im:X}";
            else immediate = $"0x{im & 0xffff:X}";
        }

        immediate = ((ushort)Immediate & 0xffff).ToString();



        if (rs == "")
            rs = RS.ToString();
        if (rt == "")
            rt = RT.ToString();

        switch (format)
        {
            default:
                return $"{Name}; Unknown Immediate Instruction Format {format} {Data & 0x3f}";

            case Format.Rt:
                if (Name == "jal")
                    Console.WriteLine("wtf");
                return $"{Name} ${rt}, {immediate}";
            case Format.RtRs:
                return $"{Name} ${rt}, ${rs}, {immediate}";
            case Format.RsRt:
                return $"{Name} ${rs}, ${rt}, {immediate}";
            case Format.RtOffsetBase:
                return $"{Name} ${rt}, {immediate}(${rs})";
            case Format.BranchRsRt:
                return $"{Name} ${rs}, ${rt}, {immediate}";
            case Format.BranchRs:
                return $"{Name} ${rs}, {immediate}";
            case Format.Cache: return $"{Name} {RT}, {immediate}(${rs});";
        }

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
        // string immText = Immediate < 0 ? $"-0x{-Immediate:X}" : $"0x{Immediate:X}";
        return ToString();
    }

    public override string ToString(string symbol)
    {
        switch (format)
        {
            default:
                return $"{Name}; Unknown Immediate Instruction Format {format}";

            case Format.Rt:
                return $"{Name} ${RT}, ${symbol}";
            case Format.RtRs:
                return $"{Name} ${RT}, ${RS}, ${symbol}";
            case Format.RsRt:
                return $"{Name} ${RS}, ${RT}, ${symbol}";
            case Format.RtOffsetBase:
                return $"{Name} ${RT}, {symbol}(${RS})";

            case Format.BranchRsRt:
                return $"{Name} ${RS}, ${RT}, ${symbol}";
            case Format.BranchRs:
                return $"{Name} ${RS}, ${symbol}";
        }

        string immText = Immediate < 0 ? $"-0x{-Immediate:X}" : $"0x{Immediate:X}";
        return ToString(symbol, RS.ToString(), RT.ToString());
    }

    public override string ToCMacro(string branch = "") 
    {
        string name = Name.ToUpper();
        switch (format)
        {
            default:
                return $"{name}; Unknown Immediate Instruction Format {format} {Data & 0x3f}";

            case Format.Rt:
                if (name == "jal")
                    Console.WriteLine("wtf");
                return $"{name}(ctx, ctx->{RT}, {Immediate})";
            case Format.RtRs:
                return $"{name}(ctx, ctx->{RT}, ctx->{RS}, {Immediate})";
            case Format.RsRt:
                return $"{name}(ctx, ctx->{RS}, ctx->{RT}, {Immediate})";
            case Format.RtOffsetBase:
                return $"{name}(ctx, ctx->{RT}, {Immediate}, ctx->{RS})";
            case Format.BranchRsRt:
                return $"{name}(ctx, ctx->{RS}, ctx->{RT}, {branch})";
            case Format.BranchRs:
                return $"{name}(ctx, ctx->{RS}, {branch})";
            case Format.Cache: return $"{Name}(ctx, ctx->{RT}, {Immediate}, ctx->{RS});";
        }
    }
}