using AdvancedBinary;

namespace ThreeHousesSlave
{
    internal class INFOPatcher
    {
        byte[] IndexedTableData;
        byte[] NamedTableData;
        byte[] EntryCountTableData;

        public INFOPatcher(byte[] Info0, byte[] Info1, byte[] Info2) {
            IndexedTableData = Info0;
            NamedTableData = Info1;
            EntryCountTableData = Info2;
        }

        public struct IndexedFile
        {
            public long EntryID;

            [StructField]
            public FileInfo EntryInfo;
        }

        public struct FileInfo
        {
            public long DecompressedSize;
            public long CompressedSize;
            public long IsCompressed;

            [FString(256, TrimNull = true)]
            public string Filename;
        }

        public Dictionary<long, IndexedFile> IndexedMap = new Dictionary<long, IndexedFile>();

        public List<FileInfo> NamedMap = new List<FileInfo>();

        public void Load()
        {
            using var Info0Buffer = new MemoryStream(IndexedTableData);
            using var Info1Buffer = new MemoryStream(NamedTableData);
            using var Info2Buffer = new MemoryStream(EntryCountTableData);

            var IndexedReader = new StructReader(Info0Buffer);
            var NamedReader = new StructReader(Info1Buffer);
            var CountReader = new StructReader(Info2Buffer);

            var IndexedCount = CountReader.ReadUInt64();
            var NamedCount = CountReader.ReadUInt64();

            for (ulong i = 0; i < IndexedCount; i++)
            {
                var File = new IndexedFile();
                IndexedReader.ReadStruct(ref File);

                IndexedMap[File.EntryID] = File;
            }

            for (ulong i = 0; i < NamedCount; i++)
            {
                var File = new FileInfo();
                NamedReader.ReadStruct(ref File);

                NamedMap.Add(File);
            }
        }

        public void Save(out byte[] Info0, out byte[] Info1, out byte[] Info2)
        {
            using var Info0Buffer = new MemoryStream();
            using var Info1Buffer = new MemoryStream();
            using var Info2Buffer = new MemoryStream();

            var IndexedWriter = new StructWriter(Info0Buffer);
            var NamedWriter = new StructWriter(Info1Buffer);
            var CountWriter = new StructWriter(Info2Buffer);


            var Entries = IndexedMap.Values.ToArray();
            var IndexedCount = Entries.LongCount();
            for (long i = 0; i < IndexedCount; i++)
            {
                var Entry = Entries[i];
                IndexedWriter.WriteStruct(ref Entry);
            }

            var NamedCount = NamedMap.LongCount();
            for (var i = 0; i < NamedCount; i++)
            {
                var File = NamedMap[i];
                NamedWriter.WriteStruct(ref File);
            }

            CountWriter.Write(IndexedCount);
            CountWriter.Write(NamedCount);

            IndexedWriter.Flush();
            NamedWriter.Flush();
            CountWriter.Flush();

            Info0 = Info0Buffer.ToArray();
            Info1 = Info1Buffer.ToArray();
            Info2 = Info2Buffer.ToArray();
        }
    }
}
