using ThreeHousesSlave;

Console.Title = "Tree Houses Translation Slave Tool";
if (args == null || args.Length != 1)
{
    Console.WriteLine("Drag&Drop a extracted DATA0/DATA1 Directory to this tool executable");
    Console.WriteLine("Press a key to exit");
    Console.ReadKey();
    return;
}

var Dir = args[0];

if (!Directory.Exists(Dir))
{
    Console.WriteLine("Invalid Directory");
    return;
}

var ExtractFileID = (string Name) =>
{
    var FName = Path.GetFileNameWithoutExtension(Name);
    string ID = string.Empty;
    foreach (var Char in FName)
    {
        if (char.IsNumber(Char))
            ID += Char;
        else
            break;
    }

    return int.Parse(ID);
};

var Files = Directory.GetFiles(Dir, "*.bin");
var TxtFiles = Directory.GetFiles(Dir, "*.txt");

if (!Files.Any(x => x.EndsWith("_str.bin")) && !Files.Any(x => x.EndsWith("_scene.bin")) && !Files.Any(x => x.EndsWith("_caption.bin")))
{
    Console.WriteLine("Running Text Detection...");
    foreach (var FilePath in Files.OrderBy(x => ExtractFileID(x)))
    {
        Console.WriteLine($"Checking {Path.GetFileName(FilePath)}...");

        string NewName = null;
        using (var Stream = File.OpenRead(FilePath))
        {
            if (TextSFile.IsValid(Stream))
            {
                NewName = $"{ExtractFileID(FilePath)}_str.bin";
            }
            else if (SceneText.IsValid(Stream))
            {
                NewName = $"{ExtractFileID(FilePath)}_scene.bin";
            }
            else if (Caption.IsValid(Stream))
            {
                NewName = $"{ExtractFileID(FilePath)}_caption.bin";
            }
        }

        if (NewName == null)
            continue;


        var NewFilePath = Path.Combine(Path.GetDirectoryName(FilePath) ?? throw new NullReferenceException(nameof(FilePath)), NewName);
        File.Move(FilePath, NewFilePath);

        Console.WriteLine("New File name: " + Path.GetFileName(NewFilePath));
    }
    return;
}

var Escaper = (string String, bool Enable) =>
{
    if (Enable)
    {
        string Result = string.Empty;
        foreach (char c in String)
        {
            if (c == '\n')
                Result += "\\n";
            else if (c == '\\')
                Result += "\\\\";
            else if (c == '\t')
                Result += "\\t";
            else if (c == '\r')
                Result += "\\r";
            else
                Result += c;
        }
        String = Result;
    }
    else
    {
        string Result = string.Empty;
        bool Special = false;
        foreach (char c in String)
        {
            if (c == '\\' & !Special)
            {
                Special = true;
                continue;
            }
            if (Special)
            {
                switch (c.ToString().ToLower()[0])
                {
                    case '\\':
                        Result += '\\';
                        break;
                    case 'n':
                        Result += '\n';
                        break;
                    case 't':
                        Result += '\t';
                        break;
                    case 'r':
                        Result += '\r';
                        break;
                    default:
                        throw new Exception("\\" + c + " Isn't a valid string escape.");
                }
                Special = false;
            }
            else
                Result += c;
        }
        String = Result;
    }

    return String;
};


if (!TxtFiles.Any(x => x.EndsWith("_str.txt")) && !TxtFiles.Any(x => x.EndsWith("_scene.txt")) && !TxtFiles.Any(x => x.EndsWith("_caption.txt")))
{
    Console.WriteLine("Running Text Extraction...");
    foreach (var FilePath in Files.Where(x => x.EndsWith("_str.bin")).OrderBy(x => ExtractFileID(x)))
    {
        Console.WriteLine($"Dumping {Path.GetFileName(FilePath)}...");
        string TxtPath = Path.ChangeExtension(FilePath, "txt");
        var Data = File.ReadAllBytes(FilePath);
        var Script = new TextSFile(Data);
        var Lines = Script.Import();
        var Escaped = Lines.Select(x => Escaper(x, true)).ToArray();
        File.WriteAllLines(TxtPath, Escaped);
    }

    foreach (var FilePath in Files.Where(x => x.EndsWith("_scene.bin")).OrderBy(x => ExtractFileID(x)))
    {
        Console.WriteLine($"Dumping {Path.GetFileName(FilePath)}...");
        string TxtPath = Path.ChangeExtension(FilePath, "txt");
        var Data = File.ReadAllBytes(FilePath);
        var Script = new SceneText(Data);
        var Lines = Script.Import();
        var Escaped = Lines.Select(x => Escaper(x, true)).ToArray();
        File.WriteAllLines(TxtPath, Escaped);
    }

    foreach (var FilePath in Files.Where(x => x.EndsWith("_caption.bin")).OrderBy(x => ExtractFileID(x)))
    {
        Console.WriteLine($"Dumping {Path.GetFileName(FilePath)}...");
        string TxtPath = Path.ChangeExtension(FilePath, "txt");
        var Data = File.ReadAllBytes(FilePath);
        var Script = new Caption(Data);
        var Lines = Script.Import();
        var Escaped = Lines.Select(x => Escaper(x, true)).ToArray();
        File.WriteAllLines(TxtPath, Escaped);
    }
    return;
}

Console.WriteLine("Running Text Insertion...");

foreach (var FilePath in TxtFiles.Where(x => x.EndsWith("_str.txt")).OrderBy(x => ExtractFileID(x)))
{
    Console.WriteLine($"Inserting {Path.GetFileName(FilePath)}...");
    string BinPath = Path.ChangeExtension(FilePath, "bin");
    string NewBinPath = BinPath + ".new";
    var Data = File.ReadAllBytes(BinPath);
    var Script = new TextSFile(Data);
    var Lines = Script.Import();
    var NewText = File.ReadAllLines(FilePath);
    for (int i = 0; i < Lines.Length; i++)
    {
        Lines[i] = Escaper(NewText[i], false);
    }

    var NewData = Script.Export(Lines);
    File.WriteAllBytes(NewBinPath, NewData);
}

foreach (var FilePath in TxtFiles.Where(x => x.EndsWith("_scene.txt")).OrderBy(x => ExtractFileID(x)))
{
    Console.WriteLine($"Inserting {Path.GetFileName(FilePath)}...");
    string BinPath = Path.ChangeExtension(FilePath, "bin");
    string NewBinPath = BinPath + ".new";
    var Data = File.ReadAllBytes(BinPath);
    var Script = new SceneText(Data);
    var Lines = Script.Import();
    var NewText = File.ReadAllLines(FilePath);
    for (int i = 0; i < Lines.Length; i++)
    {
        Lines[i] = Escaper(NewText[i], false);
    }

    var NewData = Script.Export(Lines);
    File.WriteAllBytes(NewBinPath, NewData);
}

foreach (var FilePath in TxtFiles.Where(x => x.EndsWith("_caption.txt")).OrderBy(x => ExtractFileID(x)))
{
    Console.WriteLine($"Inserting {Path.GetFileName(FilePath)}...");
    string BinPath = Path.ChangeExtension(FilePath, "bin");
    string NewBinPath = BinPath + ".new";
    var Data = File.ReadAllBytes(BinPath);
    var Script = new Caption(Data);
    var Lines = Script.Import();
    var NewText = File.ReadAllLines(FilePath);
    for (int i = 0; i < Lines.Length; i++)
    {
        Lines[i] = Escaper(NewText[i], false);
    }

    var NewData = Script.Export(Lines);
    File.WriteAllBytes(NewBinPath, NewData);
}