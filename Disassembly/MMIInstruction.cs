public class MMIInstruction : Instruction
{
    public enum Format { Unimplimented, Rd, Rs, RsRt, RdRs, RdRt, RdRtRs, RdRsRt, RdRtSa }

    public Register RS { get; private set; }
    public Register RT { get; private set; }
    public Register RD { get; private set; }
    public Register SA { get; private set; }

    public Format format { get; private set; }

    public MMIInstruction(uint data)
    {
        SA = (Register)(data >> 6 & 0x1f);
        RD = (Register)(data >> 11 & 0x1f);
        RT = (Register)(data >> 16 & 0x1f);
        RS = (Register)(data >> 21 & 0x1f);
        uint function = data >> 0 & 0x3f;
        Name = FunctionToName(function, data);
    }

    public override string ToString()
    {
        switch (format)
        {
            default: return $"{Name} ; unimplemented mmi format";
            case Format.Rd: return $"{Name} ${RD}";
            case Format.Rs: return $"{Name} ${RS}";
            case Format.RsRt: return $"{Name} ${RS}, ${RT}";
            case Format.RdRs: return $"{Name} ${RD}, ${RS}";
            case Format.RdRsRt: return $"{Name} ${RD}, ${RS}, ${RT}";
            case Format.RdRtSa: return $"{Name} ${RD}, ${RT}, ${SA}";
            case Format.RdRtRs: return $"{Name} ${RD}, ${RT} ${RS}";
            case Format.RdRt: return $"{Name} ${RD}, ${RT}";
        }
    }
    public override string ToString(string symbol)
    {
        return "havent done this yet";
        switch (format)
        {
            default: return "unimplemented format";
            case Format.Rd: return $"{Name} {RD}";
            case Format.Rs: return $"{Name} {RS}";
            case Format.RsRt: return $"{Name} ${RS}, ${RT}";
            case Format.RdRs: return $"{Name} {RD}, {RS}";
            case Format.RdRsRt: return $"{Name} {RD}, {RS}, {RT}";
            case Format.RdRtSa: return $"{Name} {RD}, {RT}, {SA}";
        }
    }

    private string FunctionToName(uint function, uint data)
    {
        switch (function)
        {
            default: format = Format.Unimplimented; return $"undefined mmi function 0x{function:X}";
            case 0x0: format = Format.RsRt; return "madd";
            case 0x1: format = Format.RsRt; return "maddu";
            case 0x4: format = Format.RdRs; return "plzcw";
            case 0x8: return MMI0FunctionToName(data);
            case 0x9: return MMI2FunctionToName(data);
            case 0x10: format = Format.Rd; return "mfhi1";
            case 0x11: format = Format.Rs; return "mthi1";
            case 0x12: format = Format.Rd; return "mflo1";
            case 0x13: format = Format.Rs; return "mtlo1";
            case 0x18: format = Format.RdRsRt; return "mult1";
            case 0x19: format = Format.RdRsRt; return "multu1";
            case 0x1a: format = Format.RsRt; return "div1";
            case 0x1b: format = Format.RsRt; return "divu1";
            case 0x20: format = Format.RsRt; return "madd1";
            case 0x21: format = Format.RsRt; return "maddu1";
            case 0x28: return MMI1FunctionToName(data);
            case 0x29: return MMI3FunctionToName(data);
            case 0x30: format = Format.Rd; return "pmfhl";
            case 0x31: format = Format.Rs; return "pmthl";
            case 0x34: format = Format.RdRtSa; return "psllh";
            case 0x36: format = Format.RdRtSa; return "psrlh";
            case 0x37: format = Format.RdRtSa; return "psrah";
            case 0x3c: format = Format.RdRtSa; return "psllw";
            case 0x3e: format = Format.RdRtSa; return "psrlw";
            case 0x3f: format = Format.RdRtSa; return "psraw";
        }
    }

    private string MMI0FunctionToName(uint data)
    {
        uint function = data >> 6 & 0x1f;
        format = Format.RdRsRt;
        switch (function)
        {
            default: return "unknwn mmi0 function";

            case 0x0: return "paddw";
            case 0x1: return "psubw";
            case 0x2: return "pcgtw";
            case 0x3: return "pmaxw";

            case 0x4: return "paddh";
            case 0x5: return "psubh";
            case 0x6: return "pcgth";
            case 0x7: return "pmaxh";

            case 0x8: return "paddb";
            case 0x9: return "psubb";
            case 0xa: return "pcgtb";

            case 0x10: return "paddsw";
            case 0x11: return "psubsw";
            case 0x12: return "pextlw";
            case 0x13: return "ppacw";

            case 0x14: return "paddsh";
            case 0x15: return "psubsh";
            case 0x16: return "pextlh";
            case 0x17: return "ppach";

            case 0x18: return "paddsb";
            case 0x19: return "psubsb";
            case 0x1a: return "pextlb";
            case 0x1b: return "ppacb";

            case 0x1e: format = Format.RdRt; return "pext5";
            case 0x1f: format = Format.RdRt; return "ppac5";
        }
    }

    private string MMI1FunctionToName(uint data)
    {
        uint function = data >> 6 & 0x1f;
        format = Format.RdRsRt;
        switch (function)
        {
            default: return $"unknown mmi1 function 0x{function:X}";
            case 0x1: format = Format.RdRt; return "pabsw";
            case 0x2: return "pceqw";
            case 0x3: return "pminw";

            case 0x4: return "padsbh";
            case 0x5: format = Format.RdRt; return "pabsh";
            case 0x6: return "pceqh";
            case 0x7: return "pminh";

            case 0xa: return "pceqb";

            case 0x10: return "padduw";
            case 0x11: return "psubuw";
            case 0x12: return "pextuw";

            case 0x13: return "padduh";
            case 0x14: return "psubuh";
            case 0x15: return "pextuh";

            case 0x16: return "paddub";
            case 0x17: return "psubub";
            case 0x18: return "pextub";
            case 0x19: return "qfsrv";
        }
    }

    private string MMI2FunctionToName(uint data)
    {
        uint function = data >> 6 & 0x1f;
        format = Format.RdRsRt;
        switch (function)
        {
            default: return "undefined mmi2 function";
            case 0x0: return "pmaddw";

            case 0x2: format = Format.RdRtRs; return "psllvw";
            case 0x3: format = Format.RdRtRs; return "psrlvw";
            case 0x4: return "pmsubw";

            case 0x8: format = Format.Rd; return "pmfhi";
            case 0x9: format = Format.Rd; return "pmflo";
            case 0xa: return "pinth";
            case 0xc: return "pmultw";
            case 0xd: format = Format.RsRt; return "pdivw";
            case 0xe: return "pcpyld";
            case 0x10: return "pmaddh";
            case 0x11: return "phmadh";
            case 0x12: return "pand";
            case 0x13: return "pxor";
            case 0x14: return "pmsubh";
            case 0x15: return "phmsbh";
            case 0x1a: format = Format.RdRt; return "pexeh";
            case 0x1b: format = Format.RdRt; return "prevh";
            case 0x20: return "pmulth";
            case 0x21: format = Format.RsRt; return "pdivbw";
            case 0x22: format = Format.RdRt; return "pexew";
            case 0x23: format = Format.RdRt; return "prot3w";
        }
    }

    private string MMI3FunctionToName(uint data)
    {
        uint function = data >> 6 & 0x1f;
        format = Format.RdRsRt;
        switch (function)
        {
            default: return "undefined mmi3 function";
            case 0x0: return "pmadduw";
            case 0x3: return "psravw";
            case 0x8: format = Format.Rs; return "pmthi";
            case 0x9: return "pmtlo";
            case 0xa: return "pinteh";
            case 0xc: return "pmultuw";
            case 0xd: format = Format.RsRt; return "pdivuw";
            case 0xe: return "pcpyud";
            case 0x12: return "por";
            case 0x13: return "pnor";
            case 0x1a: return "pexch";
            case 0x1b: format = Format.RdRt; return "pcpyh";
            case 0x1e: format = Format.RdRt; return "pexcw";
        }
    }

    public override string ToCMacro(string branch = "")
    {
        string name = Name.ToUpper();
        switch (format)
        {
            default: return $"{name} ; unimplemented mmi format";
            case Format.Rd: return $"{name}(ctx, ctx->{RD})";
            case Format.Rs: return $"{name}(ctx, ctx->{RS})";
            case Format.RsRt: return $"{name}(ctx, ctx->{RS}, ctx->{RT})";
            case Format.RdRs: return $"{name}(ctx, ctx->{RD}, ctx->{RS})";
            case Format.RdRsRt: return $"{name}(ctx, ctx->{RD}, ctx->{RS}, ctx->{RT})";
            case Format.RdRtSa: return $"{name}(ctx, ctx->{RD}, ctx->{RT}, {SA})";
            case Format.RdRtRs: return $"{name}(ctx, ctx->{RD}, ctx->{RT} ctx->{RS})";
            case Format.RdRt: return $"{name}(ctx, ctx->{RD}, ctx->{RT})";
        }
    }
}