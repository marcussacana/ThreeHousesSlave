using AdvancedBinary;
using System.Text;

namespace ThreeHousesSlave
{
    internal class SceneText
    {
        struct TextEntry
        {
            public uint Offset;
            public uint Length;
        };

        struct SCText
        {
            public uint Count;

            [RArray(nameof(Count)), StructField]
            public TextEntry[] Entries;
        }

        byte[] Script;
        public SceneText(byte[] Data)
        {
            this.Script = Data;
        }

        public string[] Import()
        {
            using (var stream = new MemoryStream(Script))
            {
                if (!IsValid(stream))
                    throw new InvalidDataException();

                stream.Position = 0;

                var Reader = new StructReader(stream);
                SCText OffsetTable = new SCText();
                Reader.ReadStruct(ref OffsetTable);

                List<string> Strings = new List<string>();
                for (int i = 0; i < OffsetTable.Count; i++)
                {
                    var TableEntry = OffsetTable.Entries[i];
                    Reader.Position = TableEntry.Offset;

                    var StrBuffer = new byte[TableEntry.Length + 1];
                    if (Reader.Read(StrBuffer, 0, StrBuffer.Length) != TableEntry.Length + 1)
                    {
                        throw new InvalidDataException();
                    }

                    Strings.Add(Encoding.UTF8.GetString(StrBuffer));
                }
                return Strings.ToArray();
            }
        }

        public byte[] Export(string[] Content)
        {
            using (var Stream = new MemoryStream())
            using (var OutStream = new MemoryStream())
            {
                var Entries = new TextEntry[Content.Length];
                var StrWriter = new StructWriter(Stream);

                int BaseOffset = Content.Length * 8 + 4;
                for (int i = 0; i < Content.Length; i++)
                {
                    var StringData = Encoding.UTF8.GetBytes(Content[i] + "\x0\x0");
                    var Length = (uint)(StringData.Length - 2);
                    if (StringData.Length % 2 != 0)
                    {
                        Array.Resize(ref StringData, StringData.Length + 1);
                    }

                    Entries[i] = new TextEntry() { 
                        Offset = (uint)(BaseOffset + Stream.Length),
                        Length = Length - 1
                    };

                    StrWriter.Write(StringData);
                }

                StrWriter.Flush();

                var SCFile = new SCText();
                SCFile.Count = (uint)Content.Length;
                SCFile.Entries = Entries;

                StructWriter Writer = new StructWriter(OutStream);
                Writer.WriteStruct(ref SCFile);
                Writer.Write(Stream.ToArray());

                Writer.Flush();

                return OutStream.ToArray();
            }
        }

        public static bool IsValid(Stream Stream)
        {
            try
            {
                var Reader = new StructReader(Stream);
                Reader.Position = 0;

                var Count = Reader.ReadUInt32();
                var FirstOffset = Reader.ReadUInt32();

                Reader.Position = 0;

                if (Count * 8 + 4 != FirstOffset)
                    return false;

                SCText OffsetTable = new SCText();
                Reader.ReadStruct(ref OffsetTable);

                long LastReadEndPos = 0;
                for (uint i = 0; i < OffsetTable.Count; i++)
                {
                    var Offset = OffsetTable.Entries[i].Offset;
                    var Length = OffsetTable.Entries[i].Length;
                    if (Offset > Stream.Length || Offset < FirstOffset || (LastReadEndPos != 0 && LastReadEndPos > Offset))
                        return false;

                    Reader.Position = Offset;
                    var Bytes = Reader.ReadBytes((int)Length);
                    LastReadEndPos = Reader.Position;

                    if (Bytes.Any(x=> x < 10))
                        return false;

                    Reader.Position = Offset - 1;
                    var Byte = Reader.PeekByte();
                    if (i > 0 && Byte != 0)
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
