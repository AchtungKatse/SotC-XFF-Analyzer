
public class JumpInstruction : Instruction
{
    public uint jumpAddress;
    public JumpInstruction(uint data, int opcode)
    {
        base.Read(data, opcode);
        jumpAddress = data & ((1 << 26) - 1);
        Name = OpcodeToName(opcode);
    }

    public override string ToString()
    {
        return $"{Name} 0x{jumpAddress << 2:X}";
    }

    public override string ToString(string symbol)
    {
        return $"{Name} {symbol}";
    }

    public override string ToCMacro(string branch = "") => "";
}