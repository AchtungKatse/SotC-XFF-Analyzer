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
            case 0x31:
                return new WC1Instruction(data, opcode);

            case 0x39:
                return new WC1Instruction(data, opcode);
        }


        if (opcode == 0)
        {
            // Register
            return new RegisterInstruction(data);
        }

        if (opcode == 0x2 || opcode == 0x3)
        {
            return new JumpInstruction(data, opcode);
        }

        if (opcode == 0x11)
            return FPUInstruction.Read(data); // Done this way because fpu instructions are much more complicated

        return new ImmediateInstruction(data, opcode);
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