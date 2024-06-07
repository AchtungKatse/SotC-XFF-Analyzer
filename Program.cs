// See https://aka.ms/new-console-template for more information
using System.Data;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks.Dataflow;


class MainProgram
{
    public struct Argument(string name, string[] triggers, Argument.Parameter[] parameters, Action<Argument.Parameter[]> callback) // I just learned about the primary constructor. This is black magic.
    {
        public struct Parameter(string name, string defaultValue)
        {
            public string Name { get; set; } = name;
            public string DefaultValue { get; set; } = defaultValue;
            public string Value { get; set; }
        }

        public string Name = name;
        public string[] Triggers { get; set; } = triggers;
        public Parameter[] Parameters { get; set; } = parameters;
        public Action<Parameter[]> Callback { get; set; } = callback;
    };


    private static string[] XffPaths { get; set; } = [];
    private static XFF[] Xffs { get; set; } = [];
    public static ELF? MainElf { get; private set; } = null;
    private static string InputDirectory { get; set; } = "";
    private static string OutputDirectory { get; set; } = "";
    private static string MainExecutablePath { get; set; } = "";


    // All of the dynamic strings
    public static string MainELFSplitsPath => $"{OutputDirectory}/MainElf/Splits.txt";
    public static string MainElfDisassemblyPath => $"{OutputDirectory}/MainElf/Disassembly";
    public static string MainELFSectionMetaPath => $"{OutputDirectory}/MainElf/SectionData";
    public static string MainELFSectionMetaFilePath => $"{OutputDirectory}/MainElf/SectionData/SectionMeta.bin";

    // Function arguments
    private static Argument[] AllArguments =
    {
        new("Initialize",       ["-i", "--init"],           [],                                             ctx=> {CreateSplits();} ),
        new("Disassemble",      ["-s", "--split"],          [new Argument.Parameter("Force", "false")],     ctx=> {CreateSplits(ctx[0].Value == "true");} ),
        new("Disassemble",      ["-ds", "--disassemble"],   [],                                             ctx=> {DisassembleAction();} ),
        new("Decompile",        ["-de", "--decompile"],     [],                                             ctx=> {DecompileAction();} ),
        new("Patch",            ["-p", "--patch"],          [],                                             ctx=> {MegaXFFer.CreatePatchedFile(XffPaths, MainExecutablePath, OutputDirectory);} ),
        new("Help",             ["-h", "--help"],           [],                                             ctx=> { ShowHelp();} ),
        new("Set Log Level",    ["-l", "--log-level"],      [new Argument.Parameter("Level", "warn")],      ctx=> { SetLogLevel(ctx);} ),
    };

    static void Main(string[] args)
    {
        // args = ["../Files", "SCUS-97472", "-s", "true", "-de"];
        if (args.Length < 2)
        {
            Debug.LogCritical("No parameters given");
            ShowHelp();
            return;
        }

        // Get all of the arguments
        InputDirectory = args[0];
        OutputDirectory = args[1];

        if (!Directory.Exists(InputDirectory))
        {
            Debug.LogCritical($"Could not find input directory \"{InputDirectory}\"");
            return;
        }

        // Get all xff files
        List<string> xffPaths = new();
        foreach (var file in Directory.GetFiles(InputDirectory))
        {
            FileInfo info = new FileInfo(file);
            if (info.Extension.ToLower().Equals(".xff"))
                if (XFF.IsAnXFF(file))
                    xffPaths.Add(file);

            // check for main executable
            if (info.Name.Contains("SCPS") || info.Name.Contains("SCUS") || info.Name.Contains("SCES"))
                MainExecutablePath = file;
        }

        if (xffPaths.Count == 0)
        {
            Debug.LogCritical("No XFF files found in input directory");
            return;
        }

        if (MainExecutablePath == null)
        {
            Debug.LogCritical("Could not find main executable in input directory");
            return;
        }

        // XFFs
        Xffs = new XFF[xffPaths.Count];
        XffPaths = xffPaths.ToArray();

        for (int i = 0; i < Xffs.Length; i++)
        {
            Xffs[i] = new XFF(xffPaths[i]);
            Xffs[i].LoadSplitsFromFile($"{OutputDirectory}/{Xffs[i].Name}/Splits.txt");
        }

        // Load main elf
        MainElf = new ELF(MainExecutablePath);
        MainElf.LoadSplitsFromFile(MainELFSplitsPath, true);


        if (!Directory.Exists(OutputDirectory))
            Directory.CreateDirectory(OutputDirectory);

        // Read and executa all arguments
        for (int i = 2; i < args.Length; i++)
        {
            if (!TryGetArgument(args[i], out Argument arg))
            {
                Debug.LogWarn($"Failed to parse argument \"{args[i]}\"");
                continue;
            }

            if (!TryGetArgumentParameters(args, i, arg, out Argument.Parameter[] parameters))
            {
                Debug.LogError($"Failed to get parameters for argument {arg.Name} ({args[i]})");
                continue;
            }

            // Skip over read parameters
            i += parameters.Length;

            // Call function
            arg.Callback(parameters);
        }
    }

    private static bool TryGetArgument(string inputSwitch, out Argument arg)
    {
        for (int i = 0; i < AllArguments.Length; i++)
        {
            // Check argument against all switches
            if (!AllArguments[i].Triggers.Contains(inputSwitch))
                continue;
            arg = AllArguments[i];

            return true;
        }

        arg = new Argument();
        return false;
    }

    private static bool TryGetArgumentParameters(string[] inputSwitches, int currentIndex, Argument arg, out Argument.Parameter[] parameters)
    {
        if (arg.Parameters.Length + currentIndex >= inputSwitches.Length)
        {
            parameters = [];
            Debug.LogCritical($"Invalid Syntax: Failed to get parameters for argument {arg.Name} at index {currentIndex}");
            return false;
        }

        // Is the same switch
        // Get any parameters
        parameters = arg.Parameters;
        for (int p = 0; p < parameters.Length; p++)
        {
            parameters[p].Value = inputSwitches[currentIndex + p + 1];
        }

        return true;
    }


    private static void DecompileAction()
    {
        for (int i = 0; i < XffPaths.Length; i++)
        {
            Debug.LogInfo($"Disassembling {Xffs[i].Name}");
            Debug.LogDebug($"XFF {Xffs[i].Name} text section: {Xffs[i].TextSectionIndex}");
            Xffs[i].Decompile($"{OutputDirectory}/{Xffs[i].Name}");
        }

        Debug.LogInfo($"Disassembling {MainElf?.Name}");
        MainElf?.Decompile($"{OutputDirectory}/MainElf");
    }

    private static void DisassembleAction()
    {
        for (int i = 0; i < XffPaths.Length; i++)
        {
            Xffs[i].Disassemble($"{OutputDirectory}/{Xffs[i].Name}");
        }
        MainElf?.Disassemble($"{OutputDirectory}/MainElf");
    }
    
    private static void CreateSplits(bool force = false)
    {
        for (int i = 0; i < XffPaths.Length; i++)
        {
            string outSplitPath = $"{OutputDirectory}/{Xffs[i].Name}/Splits.txt";
            if (File.Exists(outSplitPath) && !force)
            {
                Debug.LogWarn($"Not updating splits at path {outSplitPath}. To do so, use -f or --force");
                continue;
            }

            Debug.LogInfo($"Creating splits for xff {Xffs[i].Name} at {outSplitPath}");
            SplitCreator splitCreator = new(Xffs[i]);
            Split[] splits = splitCreator.CreateXFFSplits(Xffs[i]);

            Splitter.WriteSplitsToFile(outSplitPath, splits, Xffs[i].SectionNames);
        }

        string mainElfSplitPath = $"{OutputDirectory}/MainElf/Splits.txt";
        if (File.Exists(mainElfSplitPath) && !force)
        {
            Debug.LogWarn($"Not updating splits at path {mainElfSplitPath}. To do so, use -f or --force");
            return;
        }

        if (MainElf == null)
        {
            Debug.LogCritical("Cannot create splits for MainElf because it does not exist.");
            return;
        }

        Debug.LogInfo("Creating elf splits");
        SplitCreator elfSplitCreater = new(MainElf);
        Split[] elfSplits = elfSplitCreater.CreateELFSplits(Xffs, MainElf);
        Splitter.WriteSplitsToFile(mainElfSplitPath, elfSplits, MainElf.SectionNames);
    }

    private static void ShowHelp()
    {
        Console.WriteLine("Format: ./XFF (input directory) (output directory) (parameters)");

        Console.WriteLine();
        for (int i = 0; i < AllArguments.Length; i++)
        {
            Argument arg = AllArguments[i];
            string triggerText = GetArgTriggers(arg);
            Console.WriteLine($"{arg.Name} ({triggerText})");

            if (arg.Parameters.Length > 0)
            {
                Console.WriteLine("\tParameters: ");
                HelpWriteArgumentParameters(arg);
            }

            Console.WriteLine();
        }
    }

    private static void HelpWriteArgumentParameters(Argument arg)
    {
        for (int i = 0; i < arg.Parameters.Length; i++)
        {
            Argument.Parameter param = arg.Parameters[i];
            Console.WriteLine($"\t\tName: {param.Name}");
            Console.WriteLine($"\t\tDefault: {param.DefaultValue}");
        }
    }

    private static string GetArgTriggers(Argument arg)
    {
        string text = "(";
        for (int i = 0; i < arg.Triggers.Length; i++)
        {
            text += arg.Triggers[i];

            if (i < arg.Triggers.Length - 1)
                text += ", ";
        }

        return text + ")";
    }

    private static void SetLogLevel(Argument.Parameter[] parameters)
    {
        switch (parameters[0].Value.ToLower())
        {
            default: Debug.LogCritical($"Invalid log level '{parameters[0].Value}'. Options are debug, warn, info, error, and critical."); break;
            case "debug": Debug.CurrentLogLevel = Debug.LogLevel.Debug; break;
            case "warn": Debug.CurrentLogLevel = Debug.LogLevel.Warn; break;
            case "info": Debug.CurrentLogLevel = Debug.LogLevel.Info; break;
            case "error": Debug.CurrentLogLevel = Debug.LogLevel.Error; break;
            case "critical": Debug.CurrentLogLevel = Debug.LogLevel.Critical; break;
        }
    }
}