public struct Split
{
    public string Name { get; set; }
    // Relative to the start of the section
    public int StartAddress { get; set; }
    public readonly int Length => End - StartAddress;
    public int End { get; set; }
    public int Section { get; set; }
    public string Type { get; set; }
    public FunctionDefinition functionDefinition;

    public string GetFunctionDefinition()
    {
        string parameterText = "";

        // Check if the function requires the recomp context
        if (functionDefinition.IncludeContext)
        {
            parameterText = "RecompContext* ctx";
            if (functionDefinition.parameters.Length > 0)
                parameterText += ", ";
        }

        for (int i = 0; i < functionDefinition.parameters.Length; i++)
        {
            FunctionParameter parameter = functionDefinition.parameters[i];
            parameterText += $"{parameter.type} {parameter.name}";

            if (i < functionDefinition.parameters.Length - 1)
                parameterText += ", ";
        }

        return $"{functionDefinition.ReturnType} {Name}({parameterText})";
    }
}