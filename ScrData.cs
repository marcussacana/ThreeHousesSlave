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
            public byte[] Flags;
        }

        struct Table0Entry {

            public Table0Entry()
            {
                Lines = new uint[3];
                Voices = new uint[3];
                Portrait = 0;
                Unk = 0;
            }

            [FArray(3)]
            public uint[] Lines;

            [FArray(3)]
            public uint[] Voices;

            public uint Portrait;

            public uint Unk;
        }

        struct Table1Entry
        {
            public Table1Entry()
            {
                Lines = new uint[8];
            }

            [FArray(8)]
            public uint[] Lines;
        }

        struct Table2Entry
        {
            public Table2Entry()
            {
                Lines = new uint[8];
                Unk = new uint[4];
            }

            [FArray(8)]
            public uint[] Lines;

            [FArray(4)]
            public uint[] Unk;
        }

        struct Table3Entry
        {
            public Table3Entry()
            {
                Lines = new uint[2];
                Unk = new uint[3];
            }

            [FArray(2)]
            public uint[] Lines;

            [FArray(3)]
            public uint[] Unk;
        }
        struct Table4Entry
        {
            public Table4Entry()
            {
                Lines = new uint[1];
                Unk = new uint[3];
            }

            [FArray(1)]
            public uint[] Lines;

            [FArray(3)]
            public uint[] Unk;
        }
        struct Table5Entry
        {
            public Table5Entry()
            {
                Lines = new uint[1];
                Unk = new uint[3];
            }

            [FArray(1)]
            public uint[] Lines;

            [FArray(3)]
            public uint[] Unk;
        }

        struct Table6Entry
        {
            public Table6Entry()
            {
                Lines = new uint[7];
            }

            [FArray(7)]
            public uint[] Lines;
        }

        struct Table7Entry
        {
            public Table7Entry()
            {
                Lines = new uint[0];
                Unk = new uint[3];  
            }

            [FArray(0)]
            public uint[] Lines;

            [FArray(3)]
            public uint[] Unk;
        }

        byte[] Script;

        public ScrData(byte[] Script)
        {
            this.Script = Script;
        }

        ScrDataHeader MainHeader;

        public string[] Import()
        {
            using var MainScript = new MemoryStream(Script);

            var MainReader = new StructReader(MainScript);

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

            List<string> Strings = new List<string>();
            for (int i = 0; i < MainHeader.LanguagesCount; i++)
            {
                Strings.AddRange(ReadLanguage(LanguagesData[i]));
            }

            return Strings.ToArray();
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

        public string[] ReadLanguage(byte[] Data)
        {
            using var LanguageStream = new MemoryStream(Data);

            var LanguageHeader = new LanguageEntry();

            var LanguageReader = new StructReader(LanguageStream);
            LanguageReader.ReadStruct(ref LanguageHeader);

            if (LanguageHeader.TableCount != 8)
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
                Strings.AddRange(ReadTable(TablesData[i], i));
            }

            return Strings.ToArray();
        }

        public byte[] WriteLanguage(string[] Strings, ref int Index, int LangIndex)
        {
            using var TableBuffer = new MemoryStream();
            using var LanguageStream = new MemoryStream();
            var LanguageHeader = new LanguageEntry();
            var LanguageWriter = new StructWriter(LanguageStream);

            LanguageHeader.TableCount = 8;
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

        private string[] ReadTable(byte[] Data, int Type)
        {
            using var TableStream = new MemoryStream(Data);
            var TableReader = new StructReader(TableStream);

            var TableHeader = new Table();
            TableReader.ReadStruct(ref TableHeader);

            TableEntries[TableHeader] = new List<object>();

            TableReader.Position = TableHeader.HeaderSize;

            List<uint> Offsets = new List<uint>();
            for (int i = 0; i < TableHeader.MessagesCount; i++) 
            {
                switch (Type)
                {
                    case 0:
                        var Table0 = new Table0Entry();
                        TableReader.ReadStruct(ref Table0);
                        Offsets.AddRange(Table0.Lines);
                        TableEntries[TableHeader].Add(Table0);
                        break;
                    case 1:
                        var Table1 = new Table1Entry();
                        TableReader.ReadStruct(ref Table1);
                        Offsets.AddRange(Table1.Lines);
                        TableEntries[TableHeader].Add(Table1);
                        break;
                    case 2:
                        var Table2 = new Table2Entry();
                        TableReader.ReadStruct(ref Table2);
                        Offsets.AddRange(Table2.Lines);
                        TableEntries[TableHeader].Add(Table2);
                        break;
                    case 3:
                        var Table3 = new Table3Entry();
                        TableReader.ReadStruct(ref Table3);
                        Offsets.AddRange(Table3.Lines);
                        TableEntries[TableHeader].Add(Table3);
                        break;
                    case 4:
                        var Table4 = new Table4Entry();
                        TableReader.ReadStruct(ref Table4);
                        Offsets.AddRange(Table4.Lines);
                        TableEntries[TableHeader].Add(Table4);
                        break;
                    case 5:
                        var Table5 = new Table5Entry();
                        TableReader.ReadStruct(ref Table5);
                        Offsets.AddRange(Table5.Lines);
                        TableEntries[TableHeader].Add(Table5);
                        break;
                    case 6:
                        var Table6 = new Table6Entry();
                        TableReader.ReadStruct(ref Table6);
                        Offsets.AddRange(Table6.Lines);
                        TableEntries[TableHeader].Add(Table6);
                        break;
                    case 7:
                        var Table7 = new Table7Entry();
                        TableReader.ReadStruct(ref Table7);
                        Offsets.AddRange(Table7.Lines);
                        TableEntries[TableHeader].Add(Table7);
                        break;
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

        Dictionary<Table, List<object>> TableEntries = new Dictionary<Table, List<object>>();
        byte[] WriteTable(string[] Strings, ref int Index, Table Table)
        {
            using var Stream = new MemoryStream();
            var StringWriter = new StructWriter(Stream);

            using var TableStream = new MemoryStream();
            var TableWriter = new StructWriter(TableStream);

            var Entries = TableEntries[Table];

            TableWriter.WriteStruct(ref Table);

            while (TableWriter.Length < Table.HeaderSize)
                TableWriter.Write((byte)0xFF);

            int OffsetTableSize = Table.PointerSize * Table.MessagesCount;

            for (int i = 0; i < Table.MessagesCount; i++)
            {
                switch (Entries[i])
                {
                    case Table0Entry T0:
                        for (int x = 0; x < T0.Lines.Length; x++)
                        {
                            if (T0.Lines[x] == uint.MaxValue)
                                continue;

                            T0.Lines[x] = (uint)(StringWriter.Position + OffsetTableSize);
                            StringWriter.WriteString(Strings[Index++], StringStyle.CString);
                        }
                        TableWriter.WriteStruct(ref T0);
                        break;
                    case Table1Entry T1:
                        for (int x = 0; x < T1.Lines.Length; x++)
                        {
                            if (T1.Lines[x] == uint.MaxValue)
                                continue;

                            T1.Lines[x] = (uint)(StringWriter.Position + OffsetTableSize);
                            StringWriter.WriteString(Strings[Index++], StringStyle.CString);
                        }
                        TableWriter.WriteStruct(ref T1);
                        break;
                    case Table2Entry T2:
                        for (int x = 0; x < T2.Lines.Length; x++)
                        {
                            if (T2.Lines[x] == uint.MaxValue)
                                continue;

                            T2.Lines[x] = (uint)(StringWriter.Position + OffsetTableSize);
                            StringWriter.WriteString(Strings[Index++], StringStyle.CString);
                        }
                        TableWriter.WriteStruct(ref T2);
                        break;
                    case Table3Entry T3:
                        for (int x = 0; x < T3.Lines.Length; x++)
                        {
                            if (T3.Lines[x] == uint.MaxValue)
                                continue;

                            T3.Lines[x] = (uint)(StringWriter.Position + OffsetTableSize);
                            StringWriter.WriteString(Strings[Index++], StringStyle.CString);
                        }
                        TableWriter.WriteStruct(ref T3);
                        break;
                    case Table4Entry T4:
                        for (int x = 0; x < T4.Lines.Length; x++)
                        {
                            if (T4.Lines[x] == uint.MaxValue)
                                continue;

                            T4.Lines[x] = (uint)(StringWriter.Position + OffsetTableSize);
                            StringWriter.WriteString(Strings[Index++], StringStyle.CString);
                        }

                        TableWriter.WriteStruct(ref T4);
                        break;
                    case Table5Entry T5:
                        for (int x = 0; x < T5.Lines.Length; x++)
                        {
                            if (T5.Lines[x] == uint.MaxValue)
                                continue;

                            T5.Lines[x] = (uint)(StringWriter.Position + OffsetTableSize);
                            StringWriter.WriteString(Strings[Index++], StringStyle.CString);
                        }

                        TableWriter.WriteStruct(ref T5);
                        break;
                    case Table6Entry T6:
                        for (int x = 0; x < T6.Lines.Length; x++)
                        {
                            if (T6.Lines[x] == uint.MaxValue)
                                continue;

                            T6.Lines[x] = (uint)(StringWriter.Position + OffsetTableSize);
                            StringWriter.WriteString(Strings[Index++], StringStyle.CString);
                        }

                        TableWriter.WriteStruct(ref T6);
                        break;
                    case Table7Entry T7:
                        for (int x = 0; x < T7.Lines.Length; x++)
                        {
                            if (T7.Lines[x] == uint.MaxValue)
                                continue;

                            T7.Lines[x] = (uint)(StringWriter.Position + OffsetTableSize);
                            StringWriter.WriteString(Strings[Index++], StringStyle.CString);
                        }
                        TableWriter.WriteStruct(ref T7);
                        break;
                }
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
