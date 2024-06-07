using System.Text;

public struct CFile
{
    public CFile(Function function)
    {
        baseFunction = function;
        split = function.split;
        Relocations = function.Relocations;

        void FlipBranch(ref int i, IndexedRelocation[] Relocations, Instruction[] Instructions, Instruction[] instructions)
        {
            // Flip jump instructions
            Instructions[i] = instructions[i + 1];
            Instructions[i + 1] = instructions[i];

            // Flip any required relocations
            for (int r = 0; r < Relocations.Length; r++)
            {
                if (Relocations[r].instructionIndex == i + 1)
                {
                    Relocations[r].instructionIndex--;
                    continue;
                }
                if (Relocations[r].instructionIndex == i)
                {
                    Relocations[r].instructionIndex++;
                    continue;
                }
            }
            i++;
        }

        // Go through each instruction and flip jump functions and the instruction they run before jumping
        // Also mips is wack
        Instruction[] instructions = function.Instructions;
        Instructions = new Instruction[instructions.Length];
        Instructions[instructions.Length - 1] = instructions[instructions.Length - 1]; // Last isntruction must be the same

        if (split.Name.Contains("Function_0x25080"))
        {
            Console.WriteLine("a");
        }

        for (int i = 0; i < instructions.Length - 1; i++)
        {
            var instruction = instructions[i];
            if (instruction is JumpInstruction)
            {
                FlipBranch(ref i, Relocations, Instructions, instructions);
                continue;
            }
            else if (instruction is RegisterInstruction reg && (reg.Name.ToLower() == "jr" || reg.Name.ToLower() == "jalr"))
            {
                FlipBranch(ref i, Relocations, Instructions, instructions);
                continue;
            }
            else if (instruction is RegimmInstruction regimm && regimm.format == RegimmInstruction.Format.BranchRsOffset)
            {
                FlipBranch(ref i, Relocations, Instructions, instructions);
                // regimm.SetImmediate((short)(regimm.Immediate - 1));
                continue;
            }
            else if (instruction is ImmediateInstruction imm && (imm.format == ImmediateInstruction.Format.BranchRs || imm.format == ImmediateInstruction.Format.BranchRsRt))
            {
                FlipBranch(ref i, Relocations, Instructions, instructions);
                // imm.Immediate--;
                continue;
            }
            else
            {
                Instructions[i] = instruction;
            }
        }
    }

    public Split split { get; set; }
    public Instruction[] Instructions { get; set; }
    public IndexedRelocation[] Relocations { get; set; }
    private Function baseFunction;

    public void Write(string outputPath, Dictionary<string, FunctionDefinition> functions)
    {
        StringBuilder sb = new StringBuilder();

        // Get all branches
        int[] branches = new int[Instructions.Length];
        bool[] hasBranch = new bool[Instructions.Length];
        for (int i = 0; i < Instructions.Length; i++)
        {
            Instruction instruction = Instructions[i];
            if (instruction is ImmediateInstruction imm)
            {
                if (imm.format != ImmediateInstruction.Format.BranchRs && imm.format != ImmediateInstruction.Format.BranchRsRt)
                    continue;

                int branchIndex = imm.Immediate + i + 0;
                branches[i] = branchIndex;

                if (branchIndex < hasBranch.Length)
                {
                    hasBranch[branchIndex] = true;
                }
                else
                {
                    Debug.LogError($"Function at {outputPath} has invalid branch of index {branchIndex} and only {hasBranch.Length} instructions.");
                }
            }

            if (instruction is RegimmInstruction regimm)
            {
                int branchIndex = regimm.Immediate + i + 0;
                branches[i] = branchIndex;
                hasBranch[branchIndex] = true;
            }
        }

        // Add all includes
        // Base includes all files have
        sb.AppendLine("#include \"Context.h\"");
        sb.AppendLine("#include \"FunctionList.h\"");

        // File specific includes
        for (int i = 0; i < split.functionDefinition.Includes.Length; i++)
        {
            sb.AppendLine($"#include {split.functionDefinition.Includes[i]}");
        }

        sb.AppendLine();

        // Write function definition
        sb.AppendLine($"{split.GetFunctionDefinition()}{{");

        // Write contents of function
        sb.AppendLine($"printf(\"Running function '{split.Name}'\\n\");");
        for (int i = 0; i < Instructions.Length; i++)
        {
            if (hasBranch[i])
                sb.AppendLine($"Branch_0x{i}:");

            // Try adding the goto statement
            string relocationName = "norelocation";
            for (int j = 0; j < Relocations.Length; j++)
            {
                if (Relocations[j].instructionIndex == i)
                {
                    relocationName = baseFunction.Splits[Relocations[j].relocation.SplitIndex].Name;
                    break;
                }
            }

            string outText = GetInstructionText(functions, Instructions[i], relocationName, branches[i]);
            if (outText.ToLower().Contains("nop()"))
                continue;

            if (!string.IsNullOrEmpty(outText))
                sb.AppendLine($"\t{outText};");
        }

        // End function
        sb.AppendLine($"printf(\"Exiting function '{split.Name}'\\n\");");
        sb.AppendLine("}");

        // Return contents of file
        File.WriteAllText(outputPath, sb.ToString().Replace("ctx->zero", "0").Replace("fzero", "f0"));
    }

    private string GetInstructionText(Dictionary<string, FunctionDefinition> functions, Instruction instruction, string relocationName, int branch)
    {
        if (instruction is JumpInstruction)
        {
            if (functions.ContainsKey(relocationName))
            {
                FunctionDefinition function = functions[relocationName];
                string functionText = "";

                // Check if the function returns something
                if (function.ReturnType.Trim().ToLower() != "void" && !function.GenerateC)
                    functionText += $"ctx->{function.ReturnRegister} = ";

                // Add the function's name
                functionText += $"{relocationName}(";

                // Check if recomp context needs to be added
                if (function.IncludeContext)
                    functionText += "ctx";

                // Then add a comma if there are parameters and recomp context
                if (function.IncludeContext && function.parameters.Length > 0)
                    functionText += ", ";

                // Add all the parameters
                for (int i = 0; i < function.parameters.Length; i++)
                {
                    string register;
                    if (i < 8)
                        register = (Register._Register.a0 + i).ToString();
                    else register = $"sp + {(i - 8) * 4}";

                    // Check if the parameter is a pointer and if the function wants it as a global pointer
                    string addMemoryText = "";
                    if (function.parameters[i].type.Contains('*') && function.parameters[i].ConvertToNativePointer)
                        addMemoryText = "ctx->mem + ";

                    functionText += $"({function.parameters[i].type})({addMemoryText}ctx->{register})";

                    if (i < function.parameters.Length - 1)
                        functionText += ", ";
                }

                functionText += ")";
                return functionText;
            }

            if (relocationName != "norelocation")
            {
                Debug.LogWarn($"Decompiler failed to find function {relocationName} with {functions.Count} function definitions");
            }

            // return instruction.ToCMacro();
            // return $"{relocationName}(ctx)";
        }

        if (instruction is ImmediateInstruction imm && (relocationName.Contains("hi") || relocationName.Contains("lo")) && relocationName != "norelocation")
        {
            string relocText = relocationName;
            if (relocationName.Contains("hi"))
                relocText = $"((int){relocationName.Replace("%hi", "").Trim('(', ')')} & 0xffff0000u)";
            else if (relocationName.Contains("lo"))
                relocText = $"((int){relocationName.Replace("%lo", "").Trim('(', ')')} & 0xffffu)";

            if (imm.Name.ToLower() == "addiu")
            {
                return $"ctx->{imm.RT} = ctx->{imm.RS} + {relocText}";
            }
            if (imm.Name.ToLower() == "lui")
            {
                return $"ctx->{imm.RT} = {relocText}";
            }
        }

        string macroString = instruction.ToCMacro($"Branch_0x{branch}").Replace(".", "");
        if (relocationName != "norelocation")
            macroString += $";// {relocationName}: {instruction.Name} ";
        return macroString;

    }
}