// See https://aka.ms/new-console-template for more information
using System.Text;
using System.Threading.Tasks.Dataflow;

class MainProgram
{
    private static bool IsAnXFF(string path)
    {
        using (FileStream fs = new FileStream(path, FileMode.Open))
        {
            BinaryReader br = new BinaryReader(fs);
            return br.ReadInt32() == 0x32666678;
        }
    }

    static void Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.WriteLine("Invalid format: ./Main (input directory) (output directory)");
            return;
        }

        // Get all of the arguments
        string inputDirectory = args[0];
        string outputDirectory = args[1];

        // string inputDirectory = "../Files";
        // string outputDirectory = "SCUS-97472";

        // Get all xff files
        List<string> xffPaths = new List<string>();
        string mainExecutablePath = null;
        foreach (var file in Directory.GetFiles(inputDirectory))
        {
            FileInfo info = new FileInfo(file);
            if (info.Extension.ToLower().Equals(".xff"))
                if (IsAnXFF(file))
                    xffPaths.Add(file);

            // check for main executable
            if (info.Name.Contains("SCPS") || info.Name.Contains("SCUS") || info.Name.Contains("SCES"))
                mainExecutablePath = file;
        }

        if (xffPaths.Count == 0)
        {
            Console.WriteLine("No XFF files found in input directory");
            return;
        }

        if (mainExecutablePath == null)
        {
            Console.WriteLine("Could not find main executable in input directory");
            return;
        }

        if (!Directory.Exists(outputDirectory))
            Directory.CreateDirectory(outputDirectory);

        MegaXFFer.CreatePatchedFile(xffPaths.ToArray(), mainExecutablePath, outputDirectory);
    }
}