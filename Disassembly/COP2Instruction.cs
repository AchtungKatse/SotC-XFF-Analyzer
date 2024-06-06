public class COP2Instruction : Instruction
{
    public enum Format { Undefined, RtId };
    public Format format { get; private set; }

    public Register RT { get; private set; }
    public Register ID { get; private set; }

    public COP2Instruction(uint data)
    {
        RT = new Register(data >> 16 & 0x1f);
        ID = new Register(data >> 11 & 0x1f);
        Name = GetName(data);
    }

    private string GetName(uint data)
    {
        uint fmt = data >> 21 & 0x1f;

        if (fmt > 0x10)
        {
            return GetSpecial(data);
        }
        else
        {
            switch (fmt)
            {
                case 0x1: format = Format.RtId; return "QMFC2";
                case 0x2: format = Format.RtId; return "CFC2";
                case 0x5: format = Format.RtId; return "QMTC2";
                case 0x6: format = Format.RtId; return "CTC2";
                case 0x8: return GetBC2(data);
            }
        }

        return "unknown cop2 instruction";
    }

    public override string ToCMacro(string branch = "")
    {
        return $"{Name.ToUpper()}(ctx, ctx->{RT}, ctx->f{(int)ID})";
    }
    
    public override string ToString(string symbol)
    {
        switch (format)
        {
            default: return $"{Name} undefined format";
            case Format.RtId: return $"{Name} ${RT}, ${symbol}";
        }
    }

    public override string ToString()
    {
        switch (format)
        {
            default: return $"{Name} undefined format";
            case Format.RtId: return $"{Name} ${RT}, ${ID}";
        }
    }

    private string GetBC2(uint data)
    {
        uint format = data >> 16 & 0x1f;
        switch (format)
        {
            default: return "Unknown bc2 instruction";
            case 0x0: return "BC2F";
            case 0x1: return "BC2T";
            case 0x2: return "BC2FL";
            case 0x3: return "BC2TL";
        }
    }

    private string GetSpecial(uint data)
    {
        uint function = data & 0x3f;
        switch (function)
        {
            default: return "unknown cop2 special";
            case 0x0: return "vaddx";
            case 0x1: return "vaddy";
            case 0x2: return "vaddz";
            case 0x3: return "vaddw";
            case 0x4: return "vsubx";
            case 0x5: return "vsuby";
            case 0x6: return "vsubz";
            case 0x7: return "vsubw";

            case 0x8: return "vmaddx";
            case 0x9: return "vmaddy";
            case 0xa: return "vmaddz";
            case 0xb: return "vmaddw";
            case 0xc: return "vmsubx";
            case 0xd: return "vmsuby";
            case 0xe: return "vmsubz";
            case 0xf: return "vmsubw";

            case 0x10: return "vmaxx";
            case 0x11: return "vmaxy";
            case 0x12: return "vmaxz";
            case 0x13: return "vmaxw";
            case 0x14: return "vminix";
            case 0x15: return "vminiy";
            case 0x16: return "vminiz";
            case 0x17: return "vminiw";

            case 0x18: return "vmulx";
            case 0x19: return "vmuly";
            case 0x1a: return "vmulz";
            case 0x1b: return "vmulw";
            case 0x1c: return "vmulq";
            case 0x1d: return "vmaxi";
            case 0x1e: return "vmuli";
            case 0x1f: return "vminii";

            case 0x20: return "vaddq";
            case 0x21: return "vmaddq";
            case 0x22: return "vaddi";
            case 0x23: return "vmaddi";
            case 0x24: return "vsubq";
            case 0x25: return "vmsubq";
            case 0x26: return "vsubi";
            case 0x27: return "vsubi";

            case 0x28: return "vadd";
            case 0x29: return "vmadd";
            case 0x2a: return "vmul";
            case 0x2b: return "vmax";
            case 0x2c: return "vsub";
            case 0x2d: return "vmsubq";
            case 0x2e: return "vsubi";
            case 0x2f: return "vmsubi";

            case 0x30: return "viadd";
            case 0x31: return "visub";
            case 0x32: return "viaddi";
            case 0x34: return "viand";
            case 0x35: return "vior";

            case 0x38: return "vcallms";
            case 0x39: return "callmsr";

            case 0x3c: return GetCopSpecial2(data);
            case 0x3d: return GetCopSpecial2(data);
            case 0x3e: return GetCopSpecial2(data);
            case 0x3f: return GetCopSpecial2(data);
        }
    }

    private string GetCopSpecial2(uint data)
    {
        uint flo = data & 3;
        uint fhi = data >> 6 & 0xf;
        uint opcode = flo | (fhi * 4);

        switch (opcode)
        {
            default: return "unknown cop special 2";
            case 0x0: return "vaddax";
            case 0x1: return "vadday";
            case 0x2: return "vaddaz";
            case 0x3: return "vaddaw";
            case 0x4: return "vsubax";
            case 0x5: return "vsubay";
            case 0x6: return "vsubaz";
            case 0x7: return "vsubaw";

            case 0x8: return "vmaddax";
            case 0x9: return "vmadday";
            case 0xa: return "vmaddaz";
            case 0xb: return "vmaddaw";
            case 0xc: return "vmsubax";
            case 0xd: return "vmsubay";
            case 0xe: return "vmsubaz";
            case 0xf: return "vmsubaw";

            case 0x10: return "vitof0";
            case 0x11: return "vitof4";
            case 0x12: return "vitof12";
            case 0x13: return "vitof15";
            case 0x14: return "vitoi0";
            case 0x15: return "vitoi0";
            case 0x16: return "vitoi0";
            case 0x17: return "vitoi0";

            case 0x18: return "vmulax";
            case 0x19: return "vmulay";
            case 0x1a: return "vmulaz";
            case 0x1b: return "vmulaw";
            case 0x1c: return "vmulaq";
            case 0x1d: return "vabs";
            case 0x1e: return "vmulai";
            case 0x1f: return "vclipw";

            case 0x20: return "vaddaq";
            case 0x21: return "vmaddaq";
            case 0x22: return "vaddai";
            case 0x23: return "vmaddai";
            case 0x24: return "vsubaq";
            case 0x25: return "vmsubaq";
            case 0x26: return "vsubai";
            case 0x27: return "vmsubai";

            case 0x28: return "vadda";
            case 0x29: return "vmaddaq";
            case 0x2a: return "vaddai";
            case 0x2b: return "vmaddai";
            case 0x2c: return "vsubaq";
            case 0x2d: return "vmsubaq";
            case 0x2e: return "vsubai";
            case 0x2f: return "vmsubai";

            case 0x30: return "vmove";
            case 0x31: return "vmr32";
            case 0x34: return "vlqi";
            case 0x35: return "vsqi";
            case 0x36: return "vlqd";
            case 0x37: return "vsqd";

            case 0x38: return "vdiv";
            case 0x39: return "vsqrt";
            case 0x3a: return "vrsqrt";
            case 0x3b: return "vwaitq";
            case 0x3c: return "vmir";
            case 0x3d: return "vmir";
            case 0x3e: return "vilwr";
            case 0x3f: return "viswr";

            case 0x40: return "vrnext";
            case 0x41: return "vrget";
            case 0x42: return "vrinit";
            case 0x43: return "vrxor";
        }
    }
}