public class RegisterInstruction : Instruction
{
    public Register RS { get; set; }
    public Register RD { get; set; }
    public Register RT { get; set; }
    public uint Null { get; set; }

    public RegisterInstruction(uint data)
    {
        Data = data;

        uint func = data & 0x3f;
        Null = (data >> 6) & 0x1f;
        RD = (Register)((data >> 12) & 0x1f);
        RT = (Register)((data >> 17) & 0x1f);
        RS = (Register)((data >> 22) & 0x1f);

        Name = "UNKNOWN";

        switch (func)
        {
            case 0x0: Name = "sll"; break;
            case 0x2: Name = "srl"; break;
            case 0x3: Name = "sra"; break;
            case 0x4: Name = "sllv"; break;
            case 0x6: Name = "srlv"; break;
            case 0x08: Name = "jr"; break;
            case 0x09: Name = "jalr"; break;
            case 0x0A: Name = "movz"; break;
            case 0x0B: Name = "movn"; break;
            case 0x0C: Name = "syscall"; break;
            case 0x0D: Name = "break"; break;
            case 0x0F: Name = "sync"; break;
            case 0x10: Name = "mfhi"; break;
            case 0x11: Name = "mthi"; break;
            case 0x12: Name = "mflo"; break;
            case 0x13: Name = "mtlo"; break;
            case 0x14: Name = "dsllv"; break;
            case 0x16: Name = "dsrlv"; break;
            case 0x17: Name = "dsrav"; break;
            case 0x18: Name = "mult"; break;
            case 0x19: Name = "multu"; break;
            case 0x1A: Name = "div"; break;
            case 0x1B: Name = "divu"; break;
            case 0x20: Name = "add"; break;
            case 0x21: Name = "addu"; break;
            case 0x22: Name = "sub"; break;
            case 0x23: Name = "subu"; break;
            case 0x24: Name = "and"; break;
            case 0x25: Name = "or"; break;
            case 0x26: Name = "xor"; break;
            case 0x27: Name = "nor"; break;
            case 0x2A: Name = "slt"; break;
            case 0x2B: Name = "sltu"; break;
            case 0x2C: Name = "dadd"; break;
            case 0x2D: Name = "daddu"; break;
            case 0x2E: Name = "dsub"; break;
            case 0x2F: Name = "dsubu"; break;
            case 0x30: Name = "tge"; break;
            case 0x31: Name = "tgeu"; break;
            case 0x32: Name = "tlt"; break;
            case 0x33: Name = "tltu"; break;
            case 0x34: Name = "teq"; break;
            case 0x36: Name = "tne"; break;
            case 0x38: Name = "dsll"; break;
            case 0x3A: Name = "dsrl"; break;
            case 0x3B: Name = "dsra"; break;
            case 0x3C: Name = "dsll32"; break;
            case 0x3E: Name = "dsrl32"; break;
            case 0x3f: Name = "dsra32"; break;
        }
    }

    public override string ToString()
    {
        switch (Name)
        {
            default:
                return $"{Name} ${RD}, ${RS}, ${RT}";


            case "jr":
                return $"{Name} ${RS}";
        }
    }

    public override string ToString(string symbol)
    {
        switch (Name)
        {
            default:
                return $"{Name} ${RD}, {symbol}, ${RT}";


            case "jr":
                return $"{Name} {symbol}";
        }
    }
}