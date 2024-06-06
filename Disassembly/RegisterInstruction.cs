public class RegisterInstruction : Instruction
{
    private enum Format { Undefined, Rs, Rd, RsRt, RdRsRt, RdRtSa, RdRtRs, Sa, Syscall, RsRtCode, None };

    public Register RS { get; set; }
    public Register RD { get; set; }
    public Register RT { get; set; }
    public uint SA { get; set; }
    private Format format;

    public RegisterInstruction(uint data)
    {
        Data = data;

        uint func = data & 0x3f;
        SA = (data >> 6) & 0x1f;
        RD = (Register)((data >> 11) & 0x1f);
        RT = (Register)((data >> 16) & 0x1f);
        RS = (Register)((data >> 21) & 0x1f);

        Name = "UNKNOWN";
        GetNameFromFunction(func);


    }

    private void GetNameFromFunction(uint function)
    {
        switch (function)
        {
            case 0x0: Name = "sll"; format = Format.RdRtSa; break;
            case 0x2: Name = "srl"; format = Format.RdRtSa; break;
            case 0x3: Name = "sra"; format = Format.RdRtSa; break;
            case 0x4: Name = "sllv"; format = Format.RdRtRs; break;
            case 0x6: Name = "srlv"; format = Format.RdRtRs; break;
            case 0x7: Name = "srav"; format = Format.RdRtRs; break;
            case 0x08: Name = "jr"; format = Format.Rs; break;
            case 0x09: Name = "jalr"; format = Format.Rs; break;
            case 0x0A: Name = "movz"; format = Format.RdRsRt; break;
            case 0x0B: Name = "movn"; format = Format.RdRsRt; break;
            case 0x0C: Name = "syscall"; format = Format.Syscall; break;
            case 0x0D: Name = "break"; format = Format.None; break;
            case 0x0F:
                if (SA <= 0)
                {
                    Name = "sync";
                    format = Format.None;
                }
                else
                {
                    Name = "syncl";
                    format = Format.None;
                }
                break;
            case 0x10: Name = "mfhi"; format = Format.Rd; break;
            case 0x11: Name = "mthi"; format = Format.Rs; break;
            case 0x12: Name = "mflo"; format = Format.Rd; break;
            case 0x13: Name = "mtlo"; format = Format.Rs; break;
            case 0x14: Name = "dsllv"; format = Format.RdRtRs; break;
            case 0x16: Name = "dsrlv"; format = Format.RdRtRs; break;
            case 0x17: Name = "dsrav"; format = Format.RdRtRs; break;
            case 0x18: Name = "mult"; format = Format.RsRt; break;
            case 0x19: Name = "multu"; format = Format.RsRt; break;
            case 0x1A: Name = "div"; format = Format.RsRt; break;
            case 0x1B: Name = "divu"; format = Format.RsRt; break;
            case 0x20: Name = "add"; format = Format.RdRsRt; break;
            case 0x21: Name = "addu"; format = Format.RdRsRt; break;
            case 0x22: Name = "sub"; format = Format.RdRsRt; break;
            case 0x23: Name = "subu"; format = Format.RdRsRt; break;
            case 0x24: Name = "and"; format = Format.RdRsRt; break;
            case 0x25: Name = "or"; format = Format.RdRsRt; break;
            case 0x26: Name = "xor"; format = Format.RdRsRt; break;
            case 0x27: Name = "nor"; format = Format.RdRsRt; break;
            case 0x28: Name = "mfsa"; format = Format.Rd; break;
            case 0x29: Name = "mtsa"; format = Format.Rs; break;
            case 0x2A: Name = "slt"; format = Format.RdRsRt; break;
            case 0x2B: Name = "sltu"; format = Format.RdRsRt; break;
            case 0x2C: Name = "dadd"; format = Format.RdRsRt; break;
            case 0x2D: Name = "daddu"; format = Format.RdRsRt; break;
            case 0x2E: Name = "dsub"; format = Format.RdRsRt; break;
            case 0x2F: Name = "dsubu"; format = Format.RdRsRt; break;
            case 0x30: Name = "tge"; format = Format.RsRtCode; break;
            case 0x31: Name = "tgeu"; format = Format.RsRtCode; break;
            case 0x32: Name = "tlt"; format = Format.RsRtCode; break;
            case 0x33: Name = "tltu"; format = Format.RsRtCode; break;
            case 0x34: Name = "teq"; format = Format.RsRtCode; break;
            case 0x36: Name = "tne"; format = Format.RsRtCode; break;
            case 0x38: Name = "dsll"; format = Format.RdRtSa; break;
            case 0x3A: Name = "dsrl"; format = Format.RdRtSa; break;
            case 0x3B: Name = "dsra"; format = Format.RdRtSa; break;
            case 0x3C: Name = "dsll32"; format = Format.RdRtSa; break;
            case 0x3E: Name = "dsrl32"; format = Format.RdRtSa; break;
            case 0x3f: Name = "dsra32"; format = Format.RdRtSa; break;
        }
    }


    public override string ToString()
    {
        //private enum Format { Undefined, Rs,Rd, RsRt, RdRsRt, RdRtSa, RdRtRs, Sa, Syscall, RsRtCode, None };

        switch (format)
        {
            default:
                return "Unimplemented register instruction format";
            case Format.Rd:
                return $"{Name} ${RD}";
            case Format.RsRt:
                return $"{Name} ${RS}, ${RT}";
            case Format.RdRsRt:
                return $"{Name} ${RD}, ${RS}, ${RT}";
            case Format.RdRtSa:
                return $"{Name} ${RD}, ${RT}, 0x{SA:X}";
            case Format.RdRtRs:
                return $"{Name} ${RD}, ${RT}, ${RS}";
            case Format.Sa:
                return $"{Name} 0x{SA:X}";
            case Format.Syscall:
                return $"{Name} 0x{(Data >> 6) & 0xFFFFF:X}";
            case Format.RsRtCode:
                return $"{Name} ${RS}, ${RT}, {(Data >> 6) & 0b1111111111}";
            case Format.None:
                return $"{Name}";


            case Format.Rs:
                return $"{Name} ${RS}";
        }
    }

    public override string ToString(string symbol)
    {
        switch (format)
        {
            default:
                return $"{Name} ${RD}, {symbol}, ${RT}";


            case Format.Rs:
                return $"{Name} {symbol}";
        }
    }


    public override string ToCMacro(string branch = "")
    {
        string name = Name.ToUpper();

        if ((name == "JR") && RS == new Register((int)Register._Register.ra))
            return "return";

        switch (format)
        {
            default:
                return $"Unimplemented register instruction format {Name}";
            case Format.Rd:
                return $"{name}(ctx, ctx->{RD})";
            case Format.RsRt:
                return $"{name}(ctx, ctx->{RS}, ctx->{RT})";
            case Format.RdRsRt:
                return $"{name}(ctx, ctx->{RD}, ctx->{RS}, ctx->{RT})";
            case Format.RdRtSa:
                return $"{name}(ctx, ctx->{RD}, ctx->{RT}, 0x{SA:X})";
            case Format.RdRtRs:
                return $"{name}(ctx, ctx->{RD}, ctx->{RT}, ctx->{RS})";
            case Format.Sa:
                return $"{name}(0x{SA:X})";
            case Format.Syscall:
                return $"{name}({(Data >> 6) & 0xFFFFF:X})";
            case Format.RsRtCode:
                return $"{name}(ctx, ctx->{RS}, ctx->{RT}, {(Data >> 6) & 0b1111111111})";
            case Format.None:
                return $"{name}";
            case Format.Rs:
                return $"{name}(ctx, ctx->{RS})";
        }
     
        return $"{Name.ToUpper()}(ctx, ctx->{RD}, ctx->{RS}, ctx->{RT})";
    }
}