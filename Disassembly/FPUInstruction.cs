class FPUInstruction : Instruction
{
    public enum InstFormat { FdFs, FdFsFt, FsFt, FdFt, RtFs }

    public InstFormat Format { get; set; }

    public uint FD { get; set; }
    public uint FS { get; set; }
    public uint FT { get; set; }
    public uint RT { get; set; }
    public uint Offset { get; set; }

    public static FPUInstruction Read(uint data)
    {
        FPUInstruction inst = new FPUInstruction();
        uint type = (data >> 21) & 0x1f;
        switch (type)
        {
            case 0x0:
                inst.Name = "MFC1";
                inst.FS = (data >> 11) & 0x1f;
                inst.RT = (data >> 16) & 0x1f;
                inst.Format = InstFormat.RtFs;
                return inst;

            case 0x10: // Special 
                return new FPUSpecialInstruction(data);
            case 0x8: // Branches
                return new FPUBranchInstruction(data);

            case 0x2:
                inst.Name = "CFC1";
                inst.FS = (data >> 11) & 0x1f;
                inst.RT = (data >> 16) & 0x1f;
                inst.Format = InstFormat.RtFs;
                return inst;

            case 0x4:
                inst.Name = "MTC1";
                inst.FS = (data >> 11) & 0x1f;
                inst.RT = (data >> 16) & 0x1f;
                inst.Format = InstFormat.RtFs;
                return inst;

            case 0x6:
                inst.Name = "CTC1";
                inst.FS = (data >> 11) & 0x1f;
                inst.RT = (data >> 16) & 0x1f;
                inst.Format = InstFormat.RtFs;
                return inst;

            case 0x14:
                inst.Name = "CVT.S.W";
                inst.FD = (data >> 6) & 0x1f;
                inst.FS = (data >> 11) & 0x1f;
                inst.Format = InstFormat.FdFs;
                return inst;
        }



        return null;
    }


    public override string ToString()
    {
        switch (Format)
        {
            case InstFormat.FdFs:
                return $"{Name} $f{FD}, $f{FS}";
            case InstFormat.FdFsFt:
                return $"{Name} $f{FD}, $f{FS}, $f{FT}";
            case InstFormat.FsFt:
                return $"{Name} $f{FS}, $f{FT}";
            case InstFormat.FdFt:
                return $"{Name} $f{FD}, $f{FT}";
            case InstFormat.RtFs:
                return $"{Name} ${(Register)RT}, $f{FS}";
        }

        return $"{Name} UNKNOWN FORMAT '{Format}'";
    }

    public override string ToString(string symbol)
    {
        switch (Format)
        {
            case InstFormat.FdFs:
                return $"{Name} $f{FD}, {symbol}";
            case InstFormat.FdFsFt:
                return $"{Name} $f{FD}, {symbol}, $f{FT}";
            case InstFormat.FsFt:
                return $"{Name} {symbol}, $f{FT}";
            case InstFormat.FdFt:
                return $"{Name} $f{FD}, $f{FT}";
            case InstFormat.RtFs:
                return $"{Name} ${(Register)RT}, {symbol}";
        }

        return $"{Name} UNKNOWN FORMAT '{Format}'";
    }
}

class FPUSpecialInstruction : FPUInstruction
{
    public FPUSpecialInstruction(uint data)
    {
        uint function = data & 0x3f;
        FD = (data >> 6) & 0x1f;
        FS = (data >> 11) & 0x1f;
        FT = (data >> 16) & 0x1f;

        // Special case for comparison instructions
        if (function >> 4 == 0x3)
        {
            // Compare function
            uint condition = (data >> 1) & 0x3;
            switch (condition)
            {
                default:
                    Name = "Unknown FPU Compare";
                    return;
                case 0x0: Name = "C.F.S"; Format = InstFormat.FsFt; return;
                case 0x1: Name = "C.EQ.S"; Format = InstFormat.FsFt; return;
                case 0x2: Name = "C.LT.S"; Format = InstFormat.FsFt; return;
                case 0x3: Name = "C.LE.S"; Format = InstFormat.FsFt; return;
            }
        }


        // Default functions
        switch (function)
        {
            case 0x0: Name = "ADD.S"; Format = InstFormat.FdFsFt; break;
            case 0x1: Name = "SUB.S"; Format = InstFormat.FdFsFt; break;
            case 0x2: Name = "MUL.S"; Format = InstFormat.FdFsFt; break;
            case 0x3: Name = "DIV.S"; Format = InstFormat.FdFsFt; break;
            case 0x4: Name = "SQRT.S"; Format = InstFormat.FdFt; break;
            case 0x5: Name = "ABS.S"; Format = InstFormat.FdFs; break;
            case 0x6: Name = "MOV.S"; Format = InstFormat.FdFs; break;
            case 0x7: Name = "NEG.S"; Format = InstFormat.FdFs; break;
            case 0x16: Name = "RSQRT.S"; Format = InstFormat.FdFsFt; break;
            case 0x18: Name = "ADDA.S"; Format = InstFormat.FsFt; break;
            case 0x19: Name = "SUBA.S"; Format = InstFormat.FsFt; break;
            case 0x1A: Name = "MULA.S"; Format = InstFormat.FsFt; break;
            case 0x1C: Name = "MADD.S"; Format = InstFormat.FdFsFt; break;
            case 0x1D: Name = "MSUB.S"; Format = InstFormat.FdFsFt; break;
            case 0x1E: Name = "MADDA.S"; Format = InstFormat.FsFt; break;
            case 0x1F: Name = "MSUBA.S"; Format = InstFormat.FsFt; break;
            case 0x24: Name = "CVT.W.S"; Format = InstFormat.FdFs; break;
            case 0x28: Name = "MAX.S"; Format = InstFormat.FdFsFt; break;
            case 0x29: Name = "MIN.S"; Format = InstFormat.FdFsFt; break;
        }
    }
}

class FPUBranchInstruction : FPUInstruction
{
    public uint Offset { get; set; }
    public FPUBranchInstruction(uint data)
    {
        Offset = data & ushort.MaxValue;
        uint type = (data >> 16) & 0x1f;

        Name = "UNKNOWN BC1 INSTRUCTION";
        switch (type)
        {
            case 0x0: Name = "BC1F"; break;
            case 0x1: Name = "BC1T"; break;
            case 0x2: Name = "BC1FL"; break;
            case 0x3: Name = "BC1TL"; break;
        }
    }

    public override string ToString()
    {
        return $"{Name} 0x{Offset << 2:X}";
    }

    public override string ToString(string symbol)
    {
        return $"{Name} {symbol}";
    }
}

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
}