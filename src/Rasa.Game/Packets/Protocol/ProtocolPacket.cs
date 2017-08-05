﻿using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;

namespace Rasa.Packets.Protocol
{
    using Data;
    using Memory;

    public class ProtocolPacket : IBasePacket
    {
        public ClientMessageOpcode Type { get; private set; }

        public ushort Size { get; private set; }
        public byte Channel { get; private set; }
        public bool Compress { get; private set; }
        public IClientMessage Message { get; set; }

        public void Read(BinaryReader br)
        {
            if (br.BaseStream.Length < 4)
                throw new Exception("Fragmented receive, should not happen! (4 size header)");

            Size = br.ReadUInt16();
            Channel = br.ReadByte();

            br.ReadByte(); // padding

            if (Size > br.BaseStream.Length)
                throw new Exception("Fragmented receive, should not happen!");

            if (Channel != 0) // 0 == ReliableStreamChannel (no extra data)
            {
                br.ReadInt32(); // Sequence number? if (previousValue - newValue < 0) { process packet; previousValue = newValue; }
                br.ReadInt32(); // 0xDEADBEEF
                br.ReadInt32(); // skip
            }

            var packetBeginPosition = (int) br.BaseStream.Position;

            using (var reader = new ProtocolBufferReader(br, ProtocolBufferFlags.DontFragment))
            {
                reader.ReadProtocolFlags();

                ushort type;
                bool compress;

                reader.ReadPacketType(out type, out compress);

                Type = (ClientMessageOpcode) type;
                Compress = compress;

                reader.ReadXORCheck((int) br.BaseStream.Position - packetBeginPosition);
            }

            var xorCheckPosition = (int) br.BaseStream.Position;

            var readBr = br;

            if (Compress)
            {
                var someType = br.ReadByte(); // 0 = No compression
                if (someType >= 2)
                    throw new Exception("Invalid compress type received!");

                if (someType == 1)
                {
                    Debugger.Break(); // TODO: test
                    var uncompressedSize = br.ReadInt32();
                    var compressedData = br.ReadBytes(Size - ((int)br.BaseStream.Position - packetBeginPosition));
                    var uncompressedData = new byte[uncompressedSize]; // TODO: later: avoid allocating byte[], reuse the byte[] in the buffer

                    using (var compressedStream = new MemoryStream(compressedData))
                        using (var deflateStream = new DeflateStream(compressedStream, CompressionMode.Decompress))
                            deflateStream.Read(uncompressedData, 0, uncompressedSize);

                    readBr = new BinaryReader(new MemoryStream(uncompressedData));
                }
            }

            // ReSharper disable SwitchStatementMissingSomeCases
            switch (Type)
            {
                case ClientMessageOpcode.Login:
                    Message = new LoginMessage();
                    break;

                case ClientMessageOpcode.Move:
                    Message = new MoveMessage();
                    break;

                case ClientMessageOpcode.CallServerMethod:
                    Message = new CallServerMethodMessage();
                    break;

                case ClientMessageOpcode.Ping:
                    Message = new PingMessage();
                    break;

                default:
                    throw new Exception($"Unable to handle packet type {Type}, because it's a Server -> Client packet!");
            }
            // ReSharper restore SwitchStatementMissingSomeCases

            using (var reader = new ProtocolBufferReader(readBr, ProtocolBufferFlags.DontFragment))
            {
                reader.ReadProtocolFlags();

                // Subtype and Message.Read()
                reader.ReadDebugByte(41);

                if ((Message.SubtypeFlags & ClientMessageSubtypeFlag.HasSubtype) == ClientMessageSubtypeFlag.HasSubtype)
                {
                    Message.RawSubtype = reader.ReadByte();
                    if (Message.RawSubtype < Message.MinSubtype || Message.RawSubtype > Message.MaxSubtype)
                        throw new Exception("Invalid Subtype found!");
                }

                Message.Read(reader);

                reader.ReadDebugByte(42);

                reader.ReadXORCheck((int)br.BaseStream.Position - xorCheckPosition);
            }
        }

        public void Write(BinaryWriter bw)
        {
            var sizePosition = bw.BaseStream.Position;

            bw.Write((ushort) 0); // Size placeholder

            bw.Write(Channel);
            bw.Write((byte) 0); // padding

            if (Channel != 0) // 0 == ReliableStreamChannel
            {
                bw.Write(0); // sequence num?
                bw.Write(0xDEADBEEF); // const
                bw.Write(0); // padding
            }



            var currentPosition = bw.BaseStream.Position;

            bw.BaseStream.Position = sizePosition;

            bw.Write((ushort) (currentPosition - sizePosition));

            bw.BaseStream.Position = currentPosition;

            throw new NotImplementedException();
        }
    }
}