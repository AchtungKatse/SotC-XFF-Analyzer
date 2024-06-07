class WC1Instruction : Instruction
{
    public Register Base { get; set; }
    public uint FT { get; set; }
    public uint Offset { get; set; }

    public WC1Instruction(uint data, int opcode)
    {
        if (opcode == 0x39)
            Name = "SWC1";
        else
            Name = "LWC1";

        Base = (Register)((data >> 21) & 0x1f);
        FT = (data >> 16) & 0x1f;
        Offset = data & ushort.MaxValue;
    }

    public override string ToString()
    {
        return $"{Name} $f{FT}, 0x{Offset:X}(${Base})";
    }

    public override string ToString(string symbol)
    {
        return $"{Name} $f{FT}, {symbol}({Base})";
    }

    public override string ToCMacro(string branch = "")
    {
        return $"{Name.ToUpper()}(ctx, ctx->f{FT}, {Offset}, ctx->{Base})";
    }
}