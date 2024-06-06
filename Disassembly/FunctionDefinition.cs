
public struct FunctionParameter
{
    public string type;
    public string name;
    public bool ConvertToNativePointer;
}

public class FunctionDefinition
{

    public string name = "";
    public string ReturnType {get;set;} = "void";
    public Register ReturnRegister = new Register((int)Register._Register.v0);
    public FunctionParameter[] parameters = [];
    public bool Link = true;
    public bool Compile = true;
    public bool GenerateC = true;
    public bool IncludeContext = true;
    public string[] Includes = [];
}