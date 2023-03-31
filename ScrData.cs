using AdvancedBinary;

namespace ThreeHousesSlave
{
    internal class ScrData
    {

        struct ScrDataHeader
        {
            public uint LanguagesCount;

            [RArray(nameof(LanguagesCount)), StructField]
            public EntryInfo[] LanguagesInfo;
        }

        struct EntryInfo {
            public uint Offset;
            public uint Size;
        }

        struct LanguageEntry
        {
            public uint TableCount;//Should be 8

            [RArray(nameof(TableCount)), StructField]
            public EntryInfo[] TablesInfo;
        }

        struct Table
        {
            public uint Magic;//0x134C58
            public ushort Size;
            public ushort FlagSize;
            public ushort MessagesCount;
            public ushort PointerSize;
            public uint HeaderSize;

            [RArray(nameof(FlagSize))]
            public byte[] Flags;//0 = string, 1 = data
        }

        byte[] Script;

        public ScrData(byte[] Script)
        {
            this.Script = Script;
        }

        ScrDataHeader MainHeader;

        public string[][] Import()
        {
            using var MainScript = new MemoryStream(Script);

            var MainReader = new StructReader(MainScript);

            if (MainReader.PeekUInt() > 100)
                throw new InvalidDataException("Too Many Languages");

            MainHeader = new ScrDataHeader();
            MainReader.ReadStruct(ref MainHeader);

            var LanguagesData = new byte[MainHeader.LanguagesCount][];
            for (int i = 0; i < MainHeader.LanguagesCount; i++)
            {
                var LanguageInfo = MainHeader.LanguagesInfo[i];
                
                LanguagesData[i] = new byte[LanguageInfo.Size];


                MainReader.Position = LanguageInfo.Offset;
                MainReader.Read(LanguagesData[i], 0, (int)LanguageInfo.Size);
            }

            List<string[]> Strings = new List<string[]>();
            for (int i = 0; i < MainHeader.LanguagesCount; i++)
            {
                Strings.Add(ReadLanguage(LanguagesData[i]));
            }

            return Strings.ToArray();
        }

        public byte[] Export(string[][] Strings)
        {
            List<string> AllStrings = new List<string>();
            foreach (var Lang in Strings)
            {
                AllStrings.AddRange(Lang);
            }

            return Export(AllStrings.ToArray());
        }

        public byte[] Export(string[] Strings)
        {
            using var LanguageBuffer = new MemoryStream();
            using var OutBuffer = new MemoryStream();
            var OutWriter = new StructWriter(OutBuffer);

            uint HeaderSize = (MainHeader.LanguagesCount * 8) + 4;

            int StrIndex = 0;
            for (int i = 0; i < MainHeader.LanguagesCount; i++)
            {
                var LanguageData = WriteLanguage(Strings, ref StrIndex, i);

                MainHeader.LanguagesInfo[i].Offset = HeaderSize + (uint)LanguageBuffer.Length;
                MainHeader.LanguagesInfo[i].Size = (uint)LanguageData.Length;

                LanguageBuffer.Write(LanguageData);
            }


            OutWriter.WriteStruct(ref MainHeader);
            OutWriter.Write(LanguageBuffer.ToArray());

            OutWriter.Flush();

            return OutBuffer.ToArray();
        }

        long TableCount = -1;
        public string[] ReadLanguage(byte[] Data)
        {
            using var LanguageStream = new MemoryStream(Data);

            var LanguageHeader = new LanguageEntry();

            var LanguageReader = new StructReader(LanguageStream);
            LanguageReader.ReadStruct(ref LanguageHeader);

            if (TableCount == -1)
                TableCount = LanguageHeader.TableCount;

            if (TableCount != LanguageHeader.TableCount)
                throw new InvalidDataException("Language Table Count Missmatch");

            if (LanguageHeader.TableCount == 0)
                throw new InvalidDataException("Invalid Language Table Count");

            var TablesData = new byte[LanguageHeader.TableCount][];
            for (int i = 0; i < LanguageHeader.TableCount; i++)
            {
                var TableInfo = LanguageHeader.TablesInfo[i];

                TablesData[i] = new byte[TableInfo.Size];

                LanguageReader.Position = TableInfo.Offset;
                LanguageReader.Read(TablesData[i], 0, (int)TableInfo.Size);
            }

            List<string> Strings = new List<string>();
            for (int i = 0; i < LanguageHeader.TableCount; i++)
            {
                Strings.AddRange(ReadTable(TablesData[i]));
            }

            return Strings.ToArray();
        }

        public byte[] WriteLanguage(string[] Strings, ref int Index, int LangIndex)
        {
            using var TableBuffer = new MemoryStream();
            using var LanguageStream = new MemoryStream();
            var LanguageHeader = new LanguageEntry();
            var LanguageWriter = new StructWriter(LanguageStream);

            LanguageHeader.TableCount = (uint)TableCount;
            LanguageHeader.TablesInfo = new EntryInfo[LanguageHeader.TableCount];

            uint HeaderSize = (LanguageHeader.TableCount * 8) + 4;

            int TableIndex = LangIndex * (int)LanguageHeader.TableCount;
            for (int i = 0; i < LanguageHeader.TableCount; i++)
            {
                var Table = TableEntries.Keys.ElementAt(i + TableIndex);
                var TableData = WriteTable(Strings, ref Index, Table);

                LanguageHeader.TablesInfo[i] = new EntryInfo() { 
                    Offset = HeaderSize + (uint)TableBuffer.Length,
                    Size = (uint)TableData.Length
                };

                TableBuffer.Write(TableData, 0, TableData.Length);

                if (TableData.Length % 4 != 0)
                    TableBuffer.Write(new byte[4 - (TableData.Length % 4)]);
            }

            LanguageWriter.WriteStruct(ref LanguageHeader);
            LanguageWriter.Write(TableBuffer.ToArray());

            LanguageWriter.Flush();

            return LanguageStream.ToArray();
        }

        private string[] ReadTable(byte[] Data)
        {
            using var TableStream = new MemoryStream(Data);
            var TableReader = new StructReader(TableStream);

            var TableHeader = new Table();
            TableReader.ReadStruct(ref TableHeader);

            if (TableHeader.Magic != 0x134C58)
                throw new InvalidDataException("Invalid Table Magic");

            TableReader.Position = TableHeader.HeaderSize;

            uint[][] EntryTable = new uint[TableHeader.MessagesCount][];
            for (int i = 0; i < EntryTable.Length; i++)
            {
                EntryTable[i] = new uint[TableHeader.PointerSize / 4];
                for (int x = 0; x < EntryTable[i].Length; x++)
                {
                    EntryTable[i][x] = TableReader.ReadUInt32();
                }
            }

            TableEntries[TableHeader] = EntryTable;

            TableReader.Position = TableHeader.HeaderSize;

            List<uint> Offsets = new List<uint>();
            for (int i = 0; i < TableHeader.MessagesCount; i++) 
            {

                var EntryInfo = EntryTable[i];
                for (var x = 0; x < EntryInfo.Length; x++)
                {
                    if (TableHeader.Flags[x] == 0)
                        Offsets.Add(EntryInfo[x]);
                }
            }

            List<string> Strings = new List<string>();
            for (int i = 0; i < Offsets.Count; i++)
            {
                if (Offsets[i] == uint.MaxValue)
                    continue;

                TableReader.Position = Offsets[i] + TableHeader.HeaderSize;
                Strings.Add(TableReader.ReadString(StringStyle.CString));
            }

            return Strings.ToArray();
        }

        Dictionary<Table, uint[][]> TableEntries = new Dictionary<Table, uint[][]>();
        byte[] WriteTable(string[] Strings, ref int Index, Table Table)
        {
            using var Stream = new MemoryStream();
            var StringWriter = new StructWriter(Stream);

            using var TableStream = new MemoryStream();
            var TableWriter = new StructWriter(TableStream);

            TableWriter.WriteStruct(ref Table);

            while (TableWriter.Length < Table.HeaderSize)
                TableWriter.Write((byte)0xFF);

            var EntryTable = TableEntries[Table];

            int OffsetTableSize = Table.PointerSize * Table.MessagesCount;

            for (int i = 0; i < Table.MessagesCount; i++)
            {
                var EntryInfo = EntryTable[i];
                for (var x = 0; x < EntryInfo.Length; x++)
                {
                    if (Table.Flags[x] == 0)
                    {
                        if (EntryInfo[x] == uint.MaxValue)
                            continue;

                        EntryInfo[x] = (uint)(StringWriter.Length + OffsetTableSize);
                        StringWriter.WriteString(Strings[Index++], StringStyle.CString);
                    }
                }

                foreach (var Data in EntryInfo)
                    TableWriter.Write(Data);
            }


            StringWriter.Flush();
            TableWriter.Flush();

            var StringTable = Stream.ToArray();

            TableWriter.Write(StringTable, 0, StringTable.Length);

            TableWriter.Flush();

            return TableStream.ToArray();
        }

        public static bool IsValid(Stream Stream)
        {
            Stream.Position = 0;
            var Data = new byte[Stream.Length];
            Stream.Read(Data, 0, Data.Length);
            try
            {
                var Tester = new ScrData(Data);
                return Tester.Import().Length > 1;
            }
            catch {
                return false;
            }
        }
    }
}
