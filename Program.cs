using ThreeHousesSlave;

Console.Title = "Tree Houses Translation Slave Tool";
if (args == null || args.Length != 1)
{
    Console.WriteLine("Drag&Drop a extracted DATA0/DATA1 Directory to this tool executable");
    Console.WriteLine("Drag&Drop INFO0.BIN in the original directory to Repack");
    Console.WriteLine("Press a key to exit");
    Console.ReadKey();
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

    if (string.IsNullOrEmpty(ID))
        return -1;

    return long.Parse(ID);
};


var Dir = args[0];

#region Repacker

if (Dir.EndsWith("INFO0.BIN", StringComparison.InvariantCultureIgnoreCase) || Dir.EndsWith("INFO1.BIN", StringComparison.InvariantCultureIgnoreCase) || Dir.EndsWith("INFO2.BIN", StringComparison.InvariantCultureIgnoreCase))
{
    Console.WriteLine("Preparing for Repack");

    var PatchDir = Path.GetDirectoryName(Dir);
    var RomfsDir = Path.GetDirectoryName(PatchDir);

    var ModsDir = Path.Combine(RomfsDir, "mods");

    if (ModsDir.EndsWith("\\"))
        ModsDir += "\\";

    if (!Directory.Exists(ModsDir))
    {
        Directory.CreateDirectory(ModsDir);
    }

    var Mods = Directory.GetFiles(ModsDir, "*.*", SearchOption.AllDirectories);

    if (Mods.Length == 0)
    {
        Console.WriteLine("No Mods Found...");
        return;
    }

    var Info0Path = Path.Combine(PatchDir, "INFO0.bin");
    var Info1Path = Path.Combine(PatchDir, "INFO1.bin");
    var Info2Path = Path.Combine(PatchDir, "INFO2.bin");

    if (!File.Exists(Info0Path))
    {
        Console.WriteLine("INFO0.BIN Not Found");
        return;
    }

    if (!File.Exists(Info1Path))
    {
        Console.WriteLine("INFO1.BIN Not Found");
        return;
    }

    if (!File.Exists(Info2Path))
    {
        Console.WriteLine("INFO2.BIN Not Found");
        return;
    }

    var Info0Data = File.ReadAllBytes(Info0Path);
    var Info1Data = File.ReadAllBytes(Info1Path);
    var Info2Data = File.ReadAllBytes(Info2Path);

    INFOPatcher Patcher = new INFOPatcher(Info0Data, Info1Data, Info2Data);

    Patcher.Load();

    Console.WriteLine($"{Patcher.NamedMap.Count + Patcher.IndexedMap.Count} Files in the patch.");

    foreach (var Mod in Mods)
    {
        var BaseModPath = Mod.Substring(ModsDir.Length).Replace("\\", "/").TrimStart('/');
        var RomModPath = $"rom:/{BaseModPath}";
        var ReplaceModPath = $"rom:/mods/{BaseModPath}";

        var FileID = ExtractFileID(Mod);

        if (FileID == -1)
        {
            var Entry = Patcher.IndexedMap.Values
                .Where(x => 
                   x.EntryInfo.Filename.Equals(RomModPath, StringComparison.InvariantCultureIgnoreCase)
                || x.EntryInfo.Filename.Equals(ReplaceModPath, StringComparison.InvariantCultureIgnoreCase));
            if (Entry.Any())
            {
                FileID = Entry.Single().EntryID;
            }
        }

        if (FileID == -1)
        {
            var Entry = Patcher.NamedMap.Select((x, i) => (Entry: x, Index: i)).Where(x => 
               x.Entry.Filename.Equals(RomModPath, StringComparison.InvariantCultureIgnoreCase)
            || x.Entry.Filename.Equals(ReplaceModPath, StringComparison.InvariantCultureIgnoreCase));

            if (Entry.Any())
            {
                var FileEntry = Entry.Single();
                var FileInfo = new FileInfo(Mod);

                FileEntry.Entry.Filename = ReplaceModPath;
                FileEntry.Entry.DecompressedSize = FileEntry.Entry.CompressedSize = FileInfo.Length;
                FileEntry.Entry.IsCompressed = 0;

                Patcher.NamedMap[FileEntry.Index] = FileEntry.Entry;

                Console.WriteLine($"Replaced: {ReplaceModPath}");
                continue;
            }

            Console.WriteLine($"Skip: {Path.GetFileName(Mod)} has no valid file ID nor Name Entry");
            continue;
        }

        var ModFileInfo = new FileInfo(Mod);
        var IndexedEntry = new INFOPatcher.IndexedFile() {
            EntryID = FileID,
            EntryInfo = new INFOPatcher.FileInfo() {
                CompressedSize = ModFileInfo.Length,
                DecompressedSize = ModFileInfo.Length,
                IsCompressed = 0,
                Filename = ReplaceModPath
            }
        };

        Patcher.IndexedMap[FileID] = IndexedEntry;
        Console.WriteLine($"Replaced: {ReplaceModPath}");
    }

    Console.WriteLine("Generatig new INFO bins...");
    Patcher.Save(out var Info0, out var Info1, out var Info2);

    File.WriteAllBytes($"{Info0Path}.new", Info0);
    File.WriteAllBytes($"{Info1Path}.new", Info1);
    File.WriteAllBytes($"{Info2Path}.new", Info2);
    return;
}

#endregion

#region TextDetection

if (!Directory.Exists(Dir))
{
    Console.WriteLine("Invalid Directory");
    return;
}

var Files = Directory.GetFiles(Dir, "*.bin", SearchOption.AllDirectories);
var TxtFiles = Directory.GetFiles(Dir, "*.txt", SearchOption.AllDirectories);

if (!Files.Any(x => x.EndsWith("_str.bin"))
    && !Files.Any(x => x.EndsWith("_scene.bin"))
    && !Files.Any(x => x.EndsWith("_caption.bin"))
    && !Files.Any(x => x.EndsWith("_scrdata.bin"))
    && !Files.Any(x => x.EndsWith("_map.bin"))
    && !Files.Any(x => Path.GetFileName(x).StartsWith("TEXT_TALK"))
    && !Files.Any(x => Path.GetFileName(x).StartsWith("SC")))
{
    Console.WriteLine("Running Text Detection...");
    Parallel.ForEach(Files.OrderBy(x => ExtractFileID(x)), new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount }, (FilePath, Loop, Index) =>
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
            else if (ScrData.IsValid(Stream))
            {
                NewName = $"{ExtractFileID(FilePath)}_scrdata.bin";
            }
        }

        if (NewName == null || ExtractFileID(FilePath) == -1)
            return;


        var NewFilePath = Path.Combine(Path.GetDirectoryName(FilePath) ?? throw new NullReferenceException(nameof(FilePath)), NewName);
        File.Move(FilePath, NewFilePath);

        Console.WriteLine("New File name: " + Path.GetFileName(NewFilePath));
    });
    return;
}

#endregion

#region TextExtraction

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

var AutoImport = (byte[] Script) => {

    using (var Stream = new MemoryStream(Script))
    {
        if (TextSFile.IsValid(Stream))
        {
            var TextS = new TextSFile(Script);
            return (object)TextS.Import();

        }
        else if (SceneText.IsValid(Stream))
        {
            var Scene = new SceneText(Script);
            return Scene.Import();
        }
        else if (Caption.IsValid(Stream))
        {
            var Cap = new Caption(Script);
            return Cap.Import();
        }
        else if (ScrData.IsValid(Stream))
        {
            var Scr = new ScrData(Script);
            return Scr.Import();
        }

        return null;
    }

};

var AutoExport = (byte[] Script, object Content) => {

    using (var Stream = new MemoryStream(Script))
    {
        if (TextSFile.IsValid(Stream))
        {
            var TextS = new TextSFile(Script);
            TextS.Import();
            return TextS.Export((string[])Content);
        }
        else if (SceneText.IsValid(Stream))
        {
            var Scene = new SceneText(Script);
            Scene.Import();
            return Scene.Export((string[])Content);
        }
        else if (Caption.IsValid(Stream))
        {
            var Cap = new Caption(Script);
            Cap.Import();
            return Cap.Export((string[])Content);
        }
        else if (ScrData.IsValid(Stream))
        {
            var Scr = new ScrData(Script);
            Scr.Import();
            return Scr.Export((string[][])Content);
        }

        return null;
    }

};


if (!TxtFiles.Any(x => x.EndsWith("_str.txt")) 
    && !TxtFiles.Any(x => x.EndsWith("_scene.txt"))
    && !TxtFiles.Any(x => x.EndsWith("_caption.txt"))
    && !TxtFiles.Any(x => x.EndsWith("_map.txt"))
    && !TxtFiles.Any(x => x.Contains("_scrdata_"))
    && !TxtFiles.Any(x => Path.GetFileName(x).StartsWith("TEXT_TALK"))
    && !TxtFiles.Any(x => Path.GetFileName(x).StartsWith("SC")))
{
    Console.WriteLine("Running Text Extraction...");


    foreach (var FilePath in Files.Where(x => x.EndsWith("_str.bin")))
    {
        Console.WriteLine($"Dumping {Path.GetFileName(FilePath)}...");
        string TxtPath = Path.ChangeExtension(FilePath, "txt");
        var Data = File.ReadAllBytes(FilePath);
        var Script = new TextSFile(Data);
        var Lines = Script.Import();
        var Escaped = Lines.Select(x => Escaper(x, true)).ToArray();
        File.WriteAllLines(TxtPath, Escaped);
    }

    foreach (var FilePath in Files.Where(x => x.EndsWith("_scene.bin")))
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

    foreach (var FilePath in Files.Where(x => x.EndsWith("_scrdata.bin")).OrderBy(x => ExtractFileID(x)))
    {
        Console.WriteLine($"Dumping {Path.GetFileName(FilePath)}...");
        var Data = File.ReadAllBytes(FilePath);
        var Script = new ScrData(Data);
        var LangLines = Script.Import();
        for (int i = 0; i < LangLines.Length; i++)
        {
            var OutPath = Path.Combine(Path.GetDirectoryName(FilePath), Path.GetFileNameWithoutExtension(FilePath) + $"_{i}.txt");
            var Lines = LangLines[i];
            var Escaped = Lines.Select(x => Escaper(x, true)).ToArray();
            File.WriteAllLines(OutPath, Escaped);
        }
    }

    var UnkBinTypes = Files.Where(x => Path.GetFileName(x).StartsWith("TEXT_TALK"))
        .Concat(Files.Where(x => Path.GetFileName(x).StartsWith("SC")))
        .Concat(Files.Where(x => Path.GetFileName(x).StartsWith("TEXT_NR"))).Distinct();

    foreach (var FilePath in UnkBinTypes)
    {
        Console.WriteLine($"Trying Dump {Path.GetFileName(FilePath)}...");
        var Result = AutoImport(File.ReadAllBytes(FilePath));
        if (Result == null)
            continue;

        if (Result is string[][] LangLines)
        {
            for (int i = 0; i < LangLines.Length; i++)
            {
                var OutPath = Path.Combine(Path.GetDirectoryName(FilePath), Path.GetFileNameWithoutExtension(FilePath) + $"_{i}.txt");
                var Lines = LangLines[i];
                var Escaped = Lines.Select(x => Escaper(x, true)).ToArray();
                File.WriteAllLines(OutPath, Escaped);
            }
        }
        else if (Result is string[] Lines)
        {
            var Escaped = Lines.Select(x => Escaper(x, true)).ToArray();
            string TxtPath = Path.ChangeExtension(FilePath, "txt");
            File.WriteAllLines(TxtPath, Escaped);
        }
    }

    return;
}

#endregion

#region TextInsertion

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

foreach (var FilePath in Files.Where(x => x.Contains("_scrdata.bin")).OrderBy(x => ExtractFileID(x)))
{
    Console.WriteLine($"Inserting {Path.GetFileName(FilePath)}...");
    string BinPath = Path.ChangeExtension(FilePath, "bin");
    string NewBinPath = BinPath + ".new";
    var Data = File.ReadAllBytes(BinPath);
    var Script = new ScrData(Data);
    var LangLines = Script.Import();

    for (int i = 0; i < LangLines.Length; i++)
    {
        var TxtPath = Path.Combine(Path.GetDirectoryName(FilePath), Path.GetFileNameWithoutExtension(FilePath) + $"_{i}.txt");

        if (!File.Exists(TxtPath))
            continue;

        var NewLines = File.ReadAllLines(TxtPath);
        ref var Lines = ref LangLines[i];
        for (int x = 0; x < Lines.Length; x++)
        {
            Lines[x] = Escaper(NewLines[x], false);
        }

    }

    var NewData = Script.Export(LangLines);
    File.WriteAllBytes(NewBinPath, NewData);
}


var UnkTypes = Files.Where(x => Path.GetFileName(x).StartsWith("TEXT_TALK"))
    .Concat(Files.Where(x => Path.GetFileName(x).StartsWith("SC")))
    .Concat(Files.Where(x => Path.GetFileName(x).StartsWith("TEXT_NR"))).Distinct();


foreach (var FilePath in UnkTypes)
{
    Console.WriteLine($"Trying Insert {Path.GetFileName(FilePath)}...");
    var NewBinPath = FilePath + ".new";
    var OriBin = File.ReadAllBytes(FilePath);
    var Imported = AutoImport(OriBin);

    try
    {

        if (Imported is string[][] LangLines)
        {
            for (int i = 0; i < LangLines.Length; i++)
            {
                var TxtPath = Path.Combine(Path.GetDirectoryName(FilePath), Path.GetFileNameWithoutExtension(FilePath) + $"_{i}.txt");

                if (!File.Exists(TxtPath))
                    continue;

                var NewLines = File.ReadAllLines(TxtPath);
                ref var Lines = ref LangLines[i];
                for (int x = 0; x < Lines.Length; x++)
                {
                    Lines[x] = Escaper(NewLines[x], false);
                }
            }

            var Data = AutoExport(OriBin, LangLines);
            if (Data != null)
            {
                File.WriteAllBytes(NewBinPath, Data!);
            }
        }
        else if (Imported is string[] Lines)
        {
            var TextFile = Path.ChangeExtension(FilePath, "txt");
            var NewText = File.ReadAllLines(TextFile);

            for (int i = 0; i < Lines.Length; i++)
            {
                Lines[i] = Escaper(NewText[i], false);
            }

            var Data = AutoExport(OriBin, Lines);
            if (Data != null)
            {
                File.WriteAllBytes(NewBinPath, Data!);
            }
        }
    }
    catch {
        Console.WriteLine("Failed...");
    }
}

#endregion