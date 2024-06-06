public class COP1Instruction : Instruction
{
    public enum Format { FsFtI, FdFsFt, FdFt, RtFs, Offset, RdFsFt, FsFt, FdFs}
    public Format format { get; private set; }

    public Register FT { get; private set; }
    public Register FS { get; private set; }
    public Register FD { get; private set; }
    private short offset;

    public COP1Instruction(uint data)
    {
        FD = (Register)(data << 6 & 0x1f);
        FS = (Register)(data << 11 & 0x1f);
        FT = (Register)(data << 16 & 0x1f);
        Name = DataToName(data);
    }

    public override string ToString()
    {
        switch (format)
        {
            default: return $"{Name}";
            case Format.FsFtI: return $"{Name} $f{FS}, $f{FT} I";
            case Format.FdFt: return $"{Name} $f{FD}, $f{FT}";
            case Format.FdFsFt: return $"{Name} $f{FD}, $f{FS}, $f{FT}";
            case Format.RtFs: return $"{Name} ${FT}, $f{FS}"; 
            case Format.Offset: return $"{Name} ${offset}";
            case Format.RdFsFt: return $"{Name} ${FD}, $f{FS}, $f{FT}";
            case Format.FsFt: return $"{Name} $f{FS}, $f{FT}";
            case Format.FdFs: return $"{Name} $f{FD}, $f{FS}";
        }
    }

    public override string ToString(string symbol)
    {
        switch (format)
        {
            default: return $"{Name} undefined format";
            case Format.FsFtI: return $"{Name} $f{FS}, ${symbol} I";
            case Format.FdFt: return $"{Name} $f{FD}, ${symbol}";
            case Format.FdFsFt: return $"{Name} $f{FD}, $f{FS}, ${symbol}";
            case Format.RtFs: return $"{Name} ${FT}, ${symbol}"; 
            case Format.Offset: return $"{Name} ${symbol}";
            case Format.RdFsFt: return $"{Name} ${FD}, $f{FS}, ${symbol}";
            case Format.FsFt: return $"{Name} $f{FS}, ${symbol}";
            case Format.FdFs: return $"{Name} $f{FD}, ${symbol}";
        }
    }

    public string DataToName(uint data)
    {
        uint function = data >> 21 & 0x1f;
        switch (function)
        {
            default: return "Unknown cop 1 instruction";
            case 0x0: format = Format.RtFs; return "MFC1";
            case 0x2: format = Format.RtFs; return "CFC1";
            case 0x4: format = Format.RtFs; return "MTC1";
            case 0x6: format = Format.RtFs; return "CTC1";
            case 0x8: return GetBC1Name(data);
            case 0x10: return GetSName(data);
            case 0x14: return GetWName(data);
        }
    }

    private string GetBC1Name(uint data)
    {
        uint function = data >> 16 & 0x1f;
        format = Format.Offset;
        offset = (short)(function & 0xffff);
        switch (function)
        {
            default: return "Undefined bc1 function";
            case 0x0: return "BC1F";
            case 0x1: return "BC1T";
            case 0x2: return "BC1FL";
            case 0x3: return "BC1TL";
        }
    }

    private string GetSName(uint data)
    {
        uint function = data & 0x3f;
        format = Format.FdFsFt;
        switch (function)
        {
            default: return "Unknown cop1 s function";
            case 0x0: return "add.s";
            case 0x1: return "sub.s";
            case 0x2: return "mul.s";
            case 0x3: return "div.s";
            case 0x4: format = Format.FdFt; return "sqrt.s";
            case 0x5: format = Format.FdFs; return "abs.s";
            case 0x6: format = Format.FdFs; return "mov.s";
            case 0x7: format = Format.FdFsFt; return "neg.s";
            case 0x16: return "rsqrt.s";
            case 0x18: format = Format.FsFt; return "adda.s";
            case 0x19: format = Format.FsFtI; return "suba.s";
            case 0x1a: format = Format.FsFt; return "mula.s";
            case 0x1c: return "madd.s";
            case 0x1d: return "msub.s";
            case 0x1e: format = Format.FsFt; return "madda.s";
            case 0x1f: format = Format.FsFt; return "msuba.s";
            case 0x24: format = Format.FdFs; return "cvt.w.s";
            case 0x28: return "max.s";
            case 0x29: return "min.s";
            case 0x30: format = Format.FsFt; return "c.f.s";
            case 0x32: format = Format.FsFt; return "c.eq.s";
            case 0x34: format = Format.FsFt; return "c.lt.s";
            case 0x36: format = Format.FsFt; return "c.le.s";
        }

    }

    private string GetWName(uint data)
    {
        uint function = data & 0x3f;
        switch (function)
        {
            default: return "unknown cop1 w function";
            case 0x20: format = Format.FdFs; return "cvt.w.s";
        }
    }

    public override string ToCMacro(string branch = "")
    {
        string name = Name.ToUpper();
        switch (format)
        {
            default: return $"{name}";
            case Format.FsFtI: return $"{name}(ctx, ctx->f{FS}, ctx->f{FT})";
            case Format.FdFt: return $"{name}(ctx, ctx->f{FD}, ctx->f{FT})";
            case Format.FdFsFt: return $"{name}(ctx, ctx->f{FD}, ctx->f{FS}, ctx->f{FT})";
            case Format.RtFs: return $"{name}(ctx, ctx->f{FT}, ctx->f{FS})"; 
            case Format.Offset: return $"{name}(ctx, {offset})";
            case Format.RdFsFt: return $"{name}(ctx, ctx->{FD}, ctx->f{FS}, ctx->f{FT})";
            case Format.FsFt: return $"{name}(ctx, ctx->f{FS}, ctx->f{FT})";
            case Format.FdFs: return $"{name}(ctx, ctx->f{FD}, ctx->f{FS})";
        }
    }
}