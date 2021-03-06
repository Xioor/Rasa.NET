﻿using System.Collections.Generic;

namespace Rasa.Packets.Game.Server
{
    using Data;
    using Memory;

    public class BeginCharacterSelectionPacket : PythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.BeginCharacterSelection;

        public string FamilyName { get; set; }
        public bool HasCharacters { get; set; }
        public uint UserId { get; set; }
        public List<Race> EnabledRaceList { get; } = new List<Race>();
        public bool CanSkipBootcamp { get; set; }

        public BeginCharacterSelectionPacket(string familyName, bool hasCharacters, uint userId, bool canSkipBootcamp = true)
        {
            FamilyName = familyName;
            HasCharacters = hasCharacters;
            UserId = userId;
            CanSkipBootcamp = canSkipBootcamp;

            EnabledRaceList.Add(Race.Human);
            EnabledRaceList.Add(Race.Forean);
            EnabledRaceList.Add(Race.Brann);
            EnabledRaceList.Add(Race.Thrax);
        }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            FamilyName = pr.ReadUnicodeString();
            HasCharacters = pr.ReadBool();
            UserId = (uint) pr.ReadInt();

            var raceCount = pr.ReadTuple();
            for (var i = 0; i < raceCount; ++i)
                EnabledRaceList.Add((Race) pr.ReadInt());

            CanSkipBootcamp = pr.ReadBool();
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(5);
            pw.WriteUnicodeString(FamilyName);
            pw.WriteBool(HasCharacters);
            pw.WriteUInt(UserId);

            pw.WriteTuple(EnabledRaceList.Count);

            foreach (var race in EnabledRaceList)
                pw.WriteInt((int) race);

            pw.WriteBool(CanSkipBootcamp);
        }
    }
}
