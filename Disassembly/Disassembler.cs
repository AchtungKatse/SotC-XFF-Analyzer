public class GenericInstruction : Instruction
{
    public override string ToString()
    {
        return Name;
    }

    public override string ToString(string symbol)
    {

        return "Generic instruction should not have relocation";
    }

    public override string ToCMacro(string branch = "")
    {
        if (Name.ToLower() == "nop")
        return "NOP()";
        else return base.ToCMacro();
    }
}

static class Dissasembler
{
    public static Instruction GetInstruction(uint data)
    {
        if (data == 0)
        {
            return new GenericInstruction
            {
                Name = "nop"
            };
        }

        int opcode = (int)((data >> 26) & 0x3f);

        // Unique opcode overrides
        switch (opcode)
        {
            default: return new ImmediateInstruction(data, opcode);
            case 0x0: return new RegisterInstruction(data);
            case 0x1: return new RegimmInstruction(data);
            case 0x2: return new JumpInstruction(data, opcode);
            case 0x3: return new JumpInstruction(data,opcode);
            
            case 0x10: return new Cop0Instruction(data);
            case 0x11: return new COP1Instruction(data);
            case 0x12: return new COP2Instruction(data);
            case 0x1C: return new MMIInstruction(data);

            case 0x31: return new WC1Instruction(data, opcode);
            case 0x39: return new WC1Instruction(data, opcode);
        }
    }

    private static string GetFPUOpcodeToName(int data)
    {
        int opcode = data & 0x3f;
        int type = (data >> 21) & 0x1f;
        int bc1Instr = (data >> 16) & 0x1f;

        if (type == 0x10) // S
        {

            switch (opcode)
            {
                case 0x0: return "ADD.S";
                case 0x5: return "ABS.S";
                case 0x18: return "ADDA.S";
            }
        }
        else if (type == 0x8) // BC1
        {
            switch (bc1Instr)
            {
                case 0x0: return "BC1F";
                case 0x2: return "BC1FL";
            }
        }
        else if (type == 0x4)
        {
            return "MTC1";
        }

        return $"UNKNOWN FPU INSTRUCTION: 0x{opcode:X} TYPE: 0x{type:X}";
    }


}