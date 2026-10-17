using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Commandfall.Core
{
    /// <summary>
    /// Deterministic, versioned binary serialization for configs, commands and full match state, plus a 64-bit state hash
    /// used by clients to verify they are in sync with the host.
    /// </summary>
    public static class Serialization
    {
        public const byte FormatVersion = 2;
        /// <summary>Format of Full Roster configs and states (Classic data keeps <see cref="FormatVersion"/> so old replays still load).</summary>
        public const byte RosterFormatVersion = 3;

        // ------------------------------------------------------------------ primitives
        static void W(BinaryWriter w, Hex h) { w.Write((sbyte)h.Q); w.Write((sbyte)h.R); }
        static Hex RHex(BinaryReader r) { int q = r.ReadSByte(); int rr = r.ReadSByte(); return new Hex(q, rr); }
        static void WStr(BinaryWriter w, string s) { w.Write(s ?? ""); }

        // ------------------------------------------------------------------ config
        public static void Write(BinaryWriter w, MatchConfig c)
        {
            w.Write(c.IsRoster ? RosterFormatVersion : FormatVersion);
            WStr(w, c.MapId); w.Write(c.FirstSeat); w.Write(c.ClockSeconds); w.Write(c.ClockGraceMs); w.Write(c.ActivationLimit);
            w.Write((byte)c.Stalemate); w.Write(c.DefenderSeat); w.Write((byte)c.TechMode); w.Write(c.PointBudget); w.Write(c.MaxUnits); WStr(w, c.Label);
            w.Write(c.FogOfWar);
            for (int i = 0; i < 2; i++)
            {
                var p = c.Players[i];
                WStr(w, p.DisplayName); w.Write(p.IsAI); w.Write((byte)p.AIDifficulty);
                w.Write((byte)p.Caps.Weapon); w.Write((byte)p.Caps.Defense); w.Write((byte)p.Caps.Utility); w.Write((byte)p.Caps.Vehicle);
                if (c.IsRoster) w.Write((byte)p.Caps.Unlock);
                Write(w, p.Army);
            }
            if (c.IsRoster) { w.Write((byte)c.Ruleset); w.Write((byte)c.Domain); w.Write(c.RoundLimit); }
        }

        public static void Write(BinaryWriter w, ArmyRoster a)
        {
            WStr(w, a.Name); w.Write((byte)a.Faction); w.Write((byte)a.Doctrine); w.Write((byte)a.Units.Count);
            foreach (var e in a.Units)
            {
                WStr(w, e.UnitId); w.Write((byte)e.Equipment.Count);
                foreach (var eq in e.Equipment) WStr(w, eq);
            }
        }

        public static ArmyRoster ReadArmy(BinaryReader r)
        {
            var a = new ArmyRoster { Name = r.ReadString(), Faction = (FactionId)r.ReadByte(), Doctrine = (Doctrine)r.ReadByte() };
            int n = r.ReadByte();
            for (int i = 0; i < n; i++)
            {
                var e = new RosterEntry { UnitId = r.ReadString() };
                int m = r.ReadByte();
                for (int j = 0; j < m; j++) e.Equipment.Add(r.ReadString());
                a.Units.Add(e);
            }
            return a;
        }

        public static MatchConfig ReadConfig(BinaryReader r)
        {
            byte v = r.ReadByte();
            if (v != FormatVersion && v != RosterFormatVersion) throw new InvalidDataException($"Unsupported config format {v}");
            var c = new MatchConfig
            {
                MapId = r.ReadString(), FirstSeat = r.ReadInt32(), ClockSeconds = r.ReadInt32(), ClockGraceMs = r.ReadInt32(),
                ActivationLimit = r.ReadInt32(), Stalemate = (StalemateRule)r.ReadByte(), DefenderSeat = r.ReadInt32(),
                TechMode = (TechMode)r.ReadByte(), PointBudget = r.ReadInt32(), MaxUnits = r.ReadInt32(), Label = r.ReadString(),
            };
            c.FogOfWar = r.ReadBoolean();
            for (int i = 0; i < 2; i++)
            {
                var p = new PlayerSetup { DisplayName = r.ReadString(), IsAI = r.ReadBoolean(), AIDifficulty = (AIDifficulty)r.ReadByte() };
                p.Caps = new TechCaps(r.ReadByte(), r.ReadByte(), r.ReadByte(), r.ReadByte());
                if (v == RosterFormatVersion) p.Caps.Unlock = r.ReadByte();
                p.Army = ReadArmy(r);
                c.Players[i] = p;
            }
            if (v == RosterFormatVersion) { c.Ruleset = (Ruleset)r.ReadByte(); c.Domain = (BattleDomain)r.ReadByte(); c.RoundLimit = r.ReadInt32(); }
            return c;
        }

        // ------------------------------------------------------------------ command
        public static void Write(BinaryWriter w, Command c)
        {
            w.Write((byte)c.Type); w.Write((byte)c.Seat); w.Write(c.UnitId); W(w, c.Target); W(w, c.Secondary);
            w.Write((byte)c.Ability); w.Write(c.Option); w.Write(c.ExpectedRevision); w.Write(c.Nonce); w.Write(c.ElapsedMs);
            w.Write((byte)c.Placements.Count);
            foreach (var p in c.Placements) { w.Write(p.UnitId); W(w, p.Hex); }
            if (HasPicks(c)) { w.Write((byte)c.Picks.Count); foreach (var h in c.Picks) W(w, h); }
        }

        /// <summary>Only Full Roster ability commands carry picks, so Classic commands keep their old byte layout.</summary>
        static bool HasPicks(Command c) => c.Type == CommandType.Ability && (c.Ability == AbilityId.UnitActive || c.Ability == AbilityId.Rescue);

        public static Command ReadCommand(BinaryReader r)
        {
            var c = new Command
            {
                Type = (CommandType)r.ReadByte(), Seat = r.ReadByte(), UnitId = r.ReadInt32(), Target = RHex(r), Secondary = RHex(r),
                Ability = (AbilityId)r.ReadByte(), Option = r.ReadInt32(), ExpectedRevision = r.ReadInt32(), Nonce = r.ReadUInt32(), ElapsedMs = r.ReadInt32(),
            };
            int n = r.ReadByte();
            for (int i = 0; i < n; i++) c.Placements.Add(new Placement(r.ReadInt32(), RHex(r)));
            if (HasPicks(c)) { int np = r.ReadByte(); for (int i = 0; i < np; i++) c.Picks.Add(RHex(r)); }
            return c;
        }

        // ------------------------------------------------------------------ state
        public static void Write(BinaryWriter w, MatchState s, bool includeConfig)
        {
            bool roster = s.IsRoster;
            w.Write(roster ? RosterFormatVersion : FormatVersion);
            w.Write(includeConfig);
            if (includeConfig) Write(w, s.Config);
            w.Write((byte)s.Phase); w.Write(s.ActivationIndex); w.Write((byte)s.ActiveSeat); w.Write(s.Revision); w.Write(s.NextUnitId);
            w.Write((sbyte)s.Winner); w.Write((byte)s.VictoryReason);
            for (int i = 0; i < 2; i++)
            {
                var p = s.Players[i];
                w.Write((byte)p.Faction); w.Write((byte)p.Doctrine); w.Write(p.DeploymentCommitted); w.Write(p.ClockRemainingMs); w.Write(p.CommanderId);
            }
            w.Write((short)s.Units.Count);
            foreach (var u in s.Units)
            {
                w.Write(u.Id); w.Write((byte)u.Owner); WStr(w, u.DefId); w.Write((byte)u.Equipment.Count);
                foreach (var e in u.Equipment) WStr(w, e);
                w.Write((sbyte)u.Hp); w.Write(u.Alive); w.Write(u.Deployed); W(w, u.Position); w.Write(u.Guarded); w.Write(u.Converted); w.Write(u.WeaponReadyAt);
                w.Write((byte)u.Statuses.Count);
                foreach (var st in u.Statuses) { w.Write((byte)st.Type); w.Write(st.ExpiresAt); w.Write(st.SourceUnitId); }
                w.Write((byte)u.Abilities.Count);
                foreach (var a in u.Abilities) { w.Write((byte)a.Ability); w.Write((sbyte)a.Charges); w.Write(a.ReadyAt); }
                if (roster) WriteRosterUnit(w, u);
            }
            w.Write((byte)s.Bulwarks.Count);
            foreach (var b in s.Bulwarks) { W(w, b.Hex); w.Write((byte)b.Owner); w.Write(b.SourceUnitId); w.Write((sbyte)b.Hp); w.Write((sbyte)b.MaxHp); w.Write(b.ExpiresAt); }
            w.Write((byte)s.Smoke.Count);
            foreach (var sm in s.Smoke) { W(w, sm.Hex); w.Write(sm.ExpiresAt); }
            w.Write((byte)s.Wrecks.Count);
            foreach (var wr in s.Wrecks) { W(w, wr.Hex); w.Write((byte)wr.Faction); w.Write((byte)wr.Archetype); }
            w.Write((byte)s.RecentNonces.Count);
            foreach (var n in s.RecentNonces) w.Write(n);
            if (roster) WriteRosterState(w, s);
        }

        static void WriteRosterUnit(BinaryWriter w, UnitState u)
        {
            w.Write(u.Activations); w.Write(u.ActivatedRound); w.Write((byte)u.Cooldown);
            byte flags = (byte)((u.MovedThisActivation ? 1 : 0) | (u.ActedThisActivation ? 2 : 0) | (u.AttackedThisActivation ? 4 : 0)
                                | (u.UsedActiveThisActivation ? 8 : 0) | (u.LastActivationMoved ? 16 : 0));
            w.Write(flags);
            w.Write(u.MovedRound); w.Write(u.AttackedRound); w.Write(u.AttackedByEnemyRound);
            w.Write(u.BattleFlags); w.Write(u.RoundFlags); w.Write(u.RoundFlagsRound); w.Write(u.SpawnedBy); w.Write((byte)u.RescueProgress);
            w.Write((byte)u.RStatuses.Count);
            foreach (var st in u.RStatuses)
            {
                w.Write((byte)st.Type); w.Write((short)st.Amount); w.Write((byte)st.Uses); w.Write(st.SourceId); w.Write((byte)st.Expire);
                w.Write(st.ExpireAt); w.Write((byte)st.Range); w.Write(st.Mode);
            }
        }

        static void ReadRosterUnit(BinaryReader r, UnitState u)
        {
            u.Activations = r.ReadInt32(); u.ActivatedRound = r.ReadInt32(); u.Cooldown = r.ReadByte();
            byte f = r.ReadByte();
            u.MovedThisActivation = (f & 1) != 0; u.ActedThisActivation = (f & 2) != 0; u.AttackedThisActivation = (f & 4) != 0;
            u.UsedActiveThisActivation = (f & 8) != 0; u.LastActivationMoved = (f & 16) != 0;
            u.MovedRound = r.ReadInt32(); u.AttackedRound = r.ReadInt32(); u.AttackedByEnemyRound = r.ReadInt32();
            u.BattleFlags = r.ReadInt32(); u.RoundFlags = r.ReadInt32(); u.RoundFlagsRound = r.ReadInt32(); u.SpawnedBy = r.ReadInt32(); u.RescueProgress = r.ReadByte();
            int n = r.ReadByte();
            for (int i = 0; i < n; i++)
                u.RStatuses.Add(new RStatus
                {
                    Type = (RStatusType)r.ReadByte(), Amount = r.ReadInt16(), Uses = r.ReadByte(), SourceId = r.ReadInt32(), Expire = (RExpire)r.ReadByte(),
                    ExpireAt = r.ReadInt32(), Range = r.ReadByte(), Mode = r.ReadByte(),
                });
        }

        static void WriteRosterState(BinaryWriter w, MatchState s)
        {
            w.Write(s.Round); w.Write(s.ActiveUnitId);
            for (int i = 0; i < 2; i++) { w.Write(s.KillsThisRound[i]); w.Write(s.BoardsThisRound[i]); w.Write(s.MomentumRound[i]); }
            w.Write((byte)s.Barriers.Count);
            foreach (var b in s.Barriers) { W(w, b.Hex); w.Write((byte)b.Owner); w.Write(b.SourceUnitId); w.Write((short)b.Hp); w.Write((short)b.MaxHp); w.Write(b.Blocking); w.Write(b.ExpireRound); }
            w.Write((byte)s.Mines.Count);
            foreach (var m in s.Mines) { W(w, m.Hex); w.Write((byte)m.Owner); w.Write(m.SourceUnitId); w.Write((byte)m.Damage); w.Write(m.RevealedUntilRound); }
            w.Write((byte)s.Ignited.Count);
            foreach (var g in s.Ignited) { W(w, g.Hex); w.Write(g.ExpireRound); }
            w.Write((byte)s.Objectives.Count);
            foreach (var o in s.Objectives) { W(w, o.Hex); w.Write((sbyte)o.Owner); w.Write(o.EverHeld); }
            foreach (var wr in s.Wrecks) w.Write(wr.Round);
        }

        static void ReadRosterState(BinaryReader r, MatchState s)
        {
            s.Round = r.ReadInt32(); s.ActiveUnitId = r.ReadInt32();
            for (int i = 0; i < 2; i++) { s.KillsThisRound[i] = r.ReadInt32(); s.BoardsThisRound[i] = r.ReadInt32(); s.MomentumRound[i] = r.ReadInt32(); }
            int nb = r.ReadByte();
            for (int i = 0; i < nb; i++)
                s.Barriers.Add(new RosterBarrier { Hex = RHex(r), Owner = r.ReadByte(), SourceUnitId = r.ReadInt32(), Hp = r.ReadInt16(), MaxHp = r.ReadInt16(), Blocking = r.ReadBoolean(), ExpireRound = r.ReadInt32() });
            int nm = r.ReadByte();
            for (int i = 0; i < nm; i++)
                s.Mines.Add(new RosterMine { Hex = RHex(r), Owner = r.ReadByte(), SourceUnitId = r.ReadInt32(), Damage = r.ReadByte(), RevealedUntilRound = r.ReadInt32() });
            int ni = r.ReadByte();
            for (int i = 0; i < ni; i++) s.Ignited.Add(new IgnitedHex { Hex = RHex(r), ExpireRound = r.ReadInt32() });
            int no = r.ReadByte();
            for (int i = 0; i < no; i++) s.Objectives.Add(new ObjectiveState { Hex = RHex(r), Owner = r.ReadSByte(), EverHeld = r.ReadBoolean() });
            for (int i = 0; i < s.Wrecks.Count; i++) { var wr = s.Wrecks[i]; wr.Round = r.ReadInt32(); s.Wrecks[i] = wr; }
        }

        public static MatchState ReadState(BinaryReader r, MatchConfig configIfAbsent = null)
        {
            byte v = r.ReadByte();
            if (v != FormatVersion && v != RosterFormatVersion) throw new InvalidDataException($"Unsupported state format {v}");
            bool roster = v == RosterFormatVersion;
            bool hasConfig = r.ReadBoolean();
            var s = new MatchState { Config = hasConfig ? ReadConfig(r) : configIfAbsent };
            s.Phase = (MatchPhase)r.ReadByte(); s.ActivationIndex = r.ReadInt32(); s.ActiveSeat = r.ReadByte(); s.Revision = r.ReadInt32(); s.NextUnitId = r.ReadInt32();
            s.Winner = r.ReadSByte(); s.VictoryReason = (VictoryReason)r.ReadByte();
            for (int i = 0; i < 2; i++)
            {
                s.Players[i] = new PlayerState
                {
                    Seat = i, Faction = (FactionId)r.ReadByte(), Doctrine = (Doctrine)r.ReadByte(), DeploymentCommitted = r.ReadBoolean(),
                    ClockRemainingMs = r.ReadInt32(), CommanderId = r.ReadInt32(),
                };
            }
            int un = r.ReadInt16();
            for (int i = 0; i < un; i++)
            {
                var u = new UnitState { Id = r.ReadInt32(), Owner = r.ReadByte(), DefId = r.ReadString() };
                int ne = r.ReadByte();
                for (int j = 0; j < ne; j++) u.Equipment.Add(r.ReadString());
                if (roster) RosterArmy.Recompute(u, s.Config?.Players[u.Owner].Caps);
                else UnitFactory.Recompute(u, s.Players[u.Owner].Doctrine);
                u.Hp = r.ReadSByte(); u.Alive = r.ReadBoolean(); u.Deployed = r.ReadBoolean(); u.Position = RHex(r); u.Guarded = r.ReadBoolean();
                u.Converted = r.ReadBoolean(); u.WeaponReadyAt = r.ReadInt32();
                int ns = r.ReadByte();
                for (int j = 0; j < ns; j++) u.Statuses.Add(new StatusEffect((StatusType)r.ReadByte(), r.ReadInt32(), r.ReadInt32()));
                int na = r.ReadByte();
                for (int j = 0; j < na; j++) u.Abilities.Add(new AbilitySlot { Ability = (AbilityId)r.ReadByte(), Charges = r.ReadSByte(), ReadyAt = r.ReadInt32() });
                if (roster) ReadRosterUnit(r, u);
                s.Units.Add(u);
            }
            int nb = r.ReadByte();
            for (int i = 0; i < nb; i++)
                s.Bulwarks.Add(new Bulwark { Hex = RHex(r), Owner = r.ReadByte(), SourceUnitId = r.ReadInt32(), Hp = r.ReadSByte(), MaxHp = r.ReadSByte(), ExpiresAt = r.ReadInt32() });
            int nsm = r.ReadByte();
            for (int i = 0; i < nsm; i++) s.Smoke.Add(new SmokeCloud { Hex = RHex(r), ExpiresAt = r.ReadInt32() });
            int nw = r.ReadByte();
            for (int i = 0; i < nw; i++) s.Wrecks.Add(new Wreck { Hex = RHex(r), Faction = (FactionId)r.ReadByte(), Archetype = (Archetype)r.ReadByte() });
            int nn = r.ReadByte();
            for (int i = 0; i < nn; i++) s.RecentNonces.Add(r.ReadUInt32());
            if (roster) ReadRosterState(r, s);
            return s;
        }

        // ------------------------------------------------------------------ convenience
        public static byte[] ToBytes(Action<BinaryWriter> write)
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms, Encoding.UTF8))
            {
                write(w);
                w.Flush();
                return ms.ToArray();
            }
        }

        public static T FromBytes<T>(byte[] data, Func<BinaryReader, T> read)
        {
            using (var ms = new MemoryStream(data))
            using (var r = new BinaryReader(ms, Encoding.UTF8)) return read(r);
        }

        public static byte[] StateBytes(MatchState s, bool includeConfig = false) => ToBytes(w => Write(w, s, includeConfig));
        public static byte[] ConfigBytes(MatchConfig c) => ToBytes(w => Write(w, c));
        public static byte[] CommandBytes(Command c) => ToBytes(w => Write(w, c));

        /// <summary>FNV-1a 64-bit hash of the state (excluding config). Identical on every machine for identical states.</summary>
        public static ulong StateHash(MatchState s) => Fnv1a(StateBytes(s));

        public static ulong Fnv1a(byte[] data)
        {
            ulong h = 14695981039346656037UL;
            foreach (var b in data) { h ^= b; h *= 1099511628211UL; }
            return h;
        }

        /// <summary>Hash of the content registry so mismatched builds refuse to play together.</summary>
        public static ulong ContentHash()
        {
            var sb = new StringBuilder(GameContent.ContentVersion);
            foreach (var u in GameContent.Units.Values.OrderBy(x => x.Id, StringComparer.Ordinal))
                sb.Append($"|{u.Id}:{u.Cost}:{u.MaxHp}:{u.Move}:{u.Armor}:{u.Weapon.Damage}:{u.Weapon.MinRange}-{u.Weapon.MaxRange}:{(int)u.Weapon.Traits}:{u.Weapon.Pierce}:{u.Weapon.CooldownTurns}");
            foreach (var e in GameContent.Equipment.Values.OrderBy(x => x.Id, StringComparer.Ordinal))
                sb.Append($"|{e.Id}:{e.Tier}:{e.Cost}:{e.MaxHpBonus}:{e.ArmorBonus}:{e.MoveBonus}:{e.RangeBonus}:{e.DamageBonus}:{(int)e.Flags}:{e.Weapon?.Damage}");
            foreach (var a in GameContent.Abilities.Values.OrderBy(x => x.Id)) sb.Append($"|{a.Id}:{a.Range}:{a.CooldownTurns}");
            sb.Append('|').Append(RosterContent.ContentVersion);
            foreach (var u in RosterContent.Units.Values.Concat(RosterContent.Tokens.Values).OrderBy(x => x.Id, StringComparer.Ordinal))
            {
                var p = u.Roster;
                sb.Append($"|{u.Id}:{u.MaxHp}:{u.Armor}:{(int)p.Pattern}{p.MoveHexes}:{u.Weapon.Damage}:{u.Weapon.MinRange}-{u.Weapon.MaxRange}:{p.ActiveCode}:{p.ActiveRange}:{p.ActiveCooldown}:{p.Tier}");
                var a = p.Active;
                if (a != null) sb.Append($":{a.Damage}/{a.Pierce}/{a.Splash}/{a.MaxTargets}/{a.DashRange}/{a.StrikeDamage}/{a.Range}/{a.Special}/{a.Grants.Length}");
                if (p.Passive != null) sb.Append($":{p.Passive.Kind}/{p.Passive.Amount}/{p.Passive.Range}");
            }
            foreach (var m in MapLibrary.Maps.Values.Where(x => !x.Id.StartsWith("test")).OrderBy(x => x.Id, StringComparer.Ordinal)) sb.Append('|').Append(m.LayoutSignature());
            return Fnv1a(Encoding.UTF8.GetBytes(sb.ToString()));
        }
    }
}
