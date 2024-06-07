using System.Text;

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

static class Dissassembler
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
        Instruction instruction;
        switch (opcode)
        {
            default: instruction = new ImmediateInstruction(data, opcode); break;
            case 0x0: instruction = new RegisterInstruction(data); break;
            case 0x1: instruction = new RegimmInstruction(data); break;
            case 0x2: instruction = new JumpInstruction(data, opcode); break;
            case 0x3: instruction = new JumpInstruction(data, opcode); break;

            case 0x10: instruction = new Cop0Instruction(data); break;
            case 0x11: instruction = new COP1Instruction(data); break;
            case 0x12: instruction = new COP2Instruction(data); break;
            case 0x1C: instruction = new MMIInstruction(data); break;

            case 0x31: instruction = new WC1Instruction(data, opcode); break;
            case 0x39: instruction = new WC1Instruction(data, opcode); break;
        }

        instruction.Data = data;
        return instruction;
    }

    public static void DisassembleFunctions(Function[] functions, string outputDirectory)
    {
        Directory.CreateDirectory($"{outputDirectory}");
        for (int i = 0; i < functions.Length; i++)
        {
            File.WriteAllText($"{outputDirectory}/{functions[i].split.Name}.S", functions[i].ToAssembly());
        }
    }

    public static void DecompileFunctions(Function[] functions, string outputDirectory, Dictionary<string, FunctionDefinition> functionDefinitions)
    {
        Directory.CreateDirectory($"{outputDirectory}/src/reimplemented");
        Directory.CreateDirectory($"{outputDirectory}/src/decompiled");
        Directory.CreateDirectory($"{outputDirectory}/src/auto_generated");
        for (int i = 0; i < functions.Length; i++)
        {
            CFile cFile = new CFile(functions[i]);
            cFile.Write($"{outputDirectory}/src/auto_generated/{functions[i].split.Name}.c", functionDefinitions);
        }
    }
    
   
    public static Instruction[] GetInstructions(byte[] functionData)
    {
        Instruction[] instructions = new Instruction[functionData.Length / 4];
        for (int i = 0; i < instructions.Length; i++)
        {
            uint instructionData = BitConverter.ToUInt32(functionData, i * 4);
            instructions[i] = GetInstruction(instructionData);
        }

        return instructions;
    }
}