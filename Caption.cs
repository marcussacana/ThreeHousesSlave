using AdvancedBinary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;

namespace ThreeHousesSlave
{
    internal class Caption
    {

        public struct CaptionHeader {
            public uint Magic;

            [PArray]
            public uint[] Offsets;
        }

        public struct CaptionEntry
        {
            public float StartTime;
            public float Duration;

            [CString]
            public string Text;
        }

        byte[] Script;
        public Caption(byte[] Script)
        {
            this.Script = Script;
        }

        CaptionEntry[] Entries;

        public string[] Import()
        {
            var Stream = new MemoryStream(Script);
            if (!IsValid(Stream))
                throw new InvalidDataException();

            Stream.Position = 0;

            var Reader = new StructReader(Stream);
            CaptionHeader Header = new CaptionHeader();
            Reader.ReadStruct(ref Header);

            Entries = new CaptionEntry[Header.Offsets.Length];
            for (int i = 0; i < Entries.Length; i++)
            {
                Reader.Position = Header.Offsets[i];

                var Entry = new CaptionEntry();
                Reader.ReadStruct(ref Entry);

                Entries[i] = Entry;
            }

            return Entries.Select(x => x.Text).ToArray();
        }

        public byte[] Export(string[] Content)
        {
            if (Entries == null)
                throw new Exception("No Caption file Open");

            if (Content.Length != Entries.Length)
                throw new Exception("Strings count doesn't match with the caption entrie count");

            for (int i = 0; i < Content.Length; i++)
            {
                Entries[i].Text = Content[i];
            }

            int HeaderSize = 8 + (Entries.Length * 4);


            var Header = new CaptionHeader();

            Header.Magic = 0x2962;
            Header.Offsets = new uint[Entries.Length];


            using var EntriesBuffer = new MemoryStream();
            var EntriesWriter = new StructWriter(EntriesBuffer);

            for (int i = 0; i < Entries.Length; i++)
            {
                Header.Offsets[i] = (uint)(HeaderSize + EntriesWriter.Position);

                var Entry = Entries[i];
                EntriesWriter.WriteStruct(ref Entry);

                EntriesWriter.Write((byte)0);
                while (EntriesWriter.Position % 4 != 0)
                    EntriesWriter.Write((byte)0);
            }


            EntriesWriter.Flush();

            using var OutStream = new MemoryStream();
            var OutWriter = new StructWriter(OutStream);

            OutWriter.WriteStruct(ref Header);
            OutWriter.Write(EntriesBuffer.ToArray());


            return OutStream.ToArray();
        }

        public static bool IsValid(Stream Stream)
        {
            try {
                Stream.Position = 0;

                var Reader = new StructReader(Stream);

                if (Reader.PeekInt() != 0x62290000)
                    return false;

                CaptionHeader Header = new CaptionHeader();
                Reader.ReadStruct(ref Header);

                for (int i = 0; i < Header.Offsets.Length; i++)
                {
                    var Offset = Header.Offsets[i];
                    if (Offset >= Stream.Length || Offset < Stream.Position)
                        return false;
                }

                return true;
            } 
            catch { 
                return false; 
            }
        }
    }
}
