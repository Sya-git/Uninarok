using System;
using System.IO;
using Uninarok.RoImport.Core.Formats;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Source;

namespace Uninarok.RoImport.Core.Catalog.Tables
{
    /// <summary>The groups of tables a catalog category needs (PLAN-0008 4.2); a run loads the union of its categories.</summary>
    [Flags]
    public enum RoCatalogNeeds
    {
        None = 0,
        Jobs = 1,
        ViewIds = 2,
        Items = 4,
        Emotes = 8,
        Skills = 16,
        Scales = 32,
        Own = 64,
        All = Jobs | ViewIds | Items | Emotes | Skills | Scales | Own,
    }

    /// <summary>
    /// Every table the catalog reads, loaded once per run and only for the groups asked for (PLAN-0008 4.2; FR-1 to FR-7,
    /// FR-9, FR-10, FR-12). Lub tables come from the data source (<c>luafiles514/lua files/…</c>) and the <c>System</c>
    /// source (item and scale tables), through <see cref="RoLubReader"/> and the table readers; the own tables come from
    /// the CSV folder. A group whose file is missing or unreadable is reported once (<see cref="RoIssueCode.SourceMissing"/>
    /// or <see cref="RoIssueCode.CorruptFile"/>, item = the file), stays null and is listed in <see cref="Missing"/>; the
    /// other groups load. Never throws for bad data.
    /// </summary>
    public sealed class RoCatalogTables
    {
        public const string TablesFolder = "luafiles514/lua files/";
        const string Referrer = "catalog tables";

        RoCatalogTables() { }

        public RoJobTables Jobs { get; private set; }
        public RoViewIdTables ViewIds { get; private set; }
        public RoItemTable Items { get; private set; }
        public RoItemTableLanguage ItemLanguage { get; private set; }
        public RoEmoteTable Emotes { get; private set; }
        public RoSkillTable Skills { get; private set; }
        public RoMonsterScaleTable Scales { get; private set; }
        public RoOwnTables Own { get; private set; }

        /// <summary>The groups asked for whose tables could not be read.</summary>
        public RoCatalogNeeds Missing { get; private set; }

        /// <summary>True when every group of <paramref name="needs"/> is loaded.</summary>
        public bool Has(RoCatalogNeeds needs) => (Loaded & needs) == needs;

        RoCatalogNeeds Loaded { get; set; }

        /// <param name="ownTablesFolder">The folder of the own CSV tables on disk; null for <see cref="RoOwnTables.Folder"/> under the current directory (the project).</param>
        public static RoCatalogTables Load(IRoSource data, IRoSource system, RoCatalogNeeds needs, RoItemTableLanguage language,
                                           RoReport report, string ownTablesFolder = null)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (report == null) throw new ArgumentNullException(nameof(report));
            var t = new RoCatalogTables { ItemLanguage = language };
            var loader = new Loader(data, system, report);
            string reason = null;

            if ((needs & RoCatalogNeeds.Jobs) != 0)
            {
                string npc = loader.Table("datainfo/" + RoJobTables.NpcIdentityFile);
                string job = npc == null ? null : loader.Table("datainfo/" + RoJobTables.JobIdentityFile);
                string names = job == null ? null : loader.Table("datainfo/" + RoJobTables.JobNameFile);
                if (names != null && RoJobTables.TryRead(npc, job, names, out var jobs, out reason)) t.Jobs = jobs;
                else if (names != null) loader.Corrupt("datainfo/" + RoJobTables.JobNameFile, reason);
                t.Mark(RoCatalogNeeds.Jobs, t.Jobs != null);
            }

            if ((needs & RoCatalogNeeds.ViewIds) != 0)
            {
                string accIds = loader.Table("datainfo/" + RoViewIdTables.AccessoryIdFile);
                string accNames = accIds == null ? null : loader.Table("datainfo/" + RoViewIdTables.AccNameFile);
                string robeIds = accNames == null ? null : loader.Table("datainfo/" + RoViewIdTables.RobeIdFile);
                string robeNames = robeIds == null ? null : loader.Table("datainfo/" + RoViewIdTables.RobeNameFile);
                string weapons = robeNames == null ? null : loader.Table("datainfo/" + RoViewIdTables.WeaponTableFile);
                if (weapons != null && RoViewIdTables.TryRead(accIds, accNames, robeIds, robeNames, weapons, out var views, out reason)) t.ViewIds = views;
                else if (weapons != null) loader.Corrupt("datainfo/" + FileOf(reason), reason);
                t.Mark(RoCatalogNeeds.ViewIds, t.ViewIds != null);
            }

            if ((needs & RoCatalogNeeds.Items) != 0)
            {
                string file = RoItemTable.FileName(language);
                string lua = loader.System(file, RoLubEncoding.Utf8);
                if (lua != null && RoItemTable.TryRead(lua, file, out var items, out reason)) t.Items = items;
                else if (lua != null) loader.Corrupt(file, reason, system: true);
                t.Mark(RoCatalogNeeds.Items, t.Items != null);
            }

            if ((needs & RoCatalogNeeds.Emotes) != 0)
            {
                string lua = loader.Table("emotion/" + RoEmoteTable.File);
                if (lua != null && RoEmoteTable.TryRead(lua, out var emotes, out reason)) t.Emotes = emotes;
                else if (lua != null) loader.Corrupt("emotion/" + RoEmoteTable.File, reason);
                t.Mark(RoCatalogNeeds.Emotes, t.Emotes != null);
            }

            if ((needs & RoCatalogNeeds.Skills) != 0)
            {
                string lua = loader.Table("skillinfoz/" + RoSkillTable.File);
                if (lua != null && RoSkillTable.TryRead(lua, out var skills, out reason)) t.Skills = skills;
                else if (lua != null) loader.Corrupt("skillinfoz/" + RoSkillTable.File, reason);
                t.Mark(RoCatalogNeeds.Skills, t.Skills != null);
            }

            if ((needs & RoCatalogNeeds.Scales) != 0)
            {
                string lua = loader.System(RoMonsterScaleTable.File, RoLubEncoding.Cp949);
                if (lua != null && RoMonsterScaleTable.TryRead(lua, out var scales, out reason)) t.Scales = scales;
                else if (lua != null) loader.Corrupt(RoMonsterScaleTable.File, reason, system: true);
                t.Mark(RoCatalogNeeds.Scales, t.Scales != null);
            }

            if ((needs & RoCatalogNeeds.Own) != 0)
            {
                string folder = ownTablesFolder ?? Path.GetFullPath(RoOwnTables.Folder);
                string bodies = loader.Disk(Path.Combine(folder, RoOwnTables.ClassBodiesFile), RoOwnTables.ClassBodiesFile);
                string shields = bodies == null ? null : loader.Disk(Path.Combine(folder, RoOwnTables.ShieldNamesFile), RoOwnTables.ShieldNamesFile);
                if (shields != null && RoOwnTables.TryRead(bodies, shields, out var own, out reason)) t.Own = own;
                else if (shields != null) report.Warn(RoIssueCode.CorruptFile, FileOf(reason), Referrer, reason);
                t.Mark(RoCatalogNeeds.Own, t.Own != null);
            }

            return t;
        }

        /// <summary>Tables already read, for the planner tests (PLAN-0008 §6); a null group is missing.</summary>
        internal static RoCatalogTables Create(RoJobTables jobs = null, RoViewIdTables viewIds = null, RoItemTable items = null,
                                               RoEmoteTable emotes = null, RoSkillTable skills = null, RoMonsterScaleTable scales = null,
                                               RoOwnTables own = null)
        {
            var t = new RoCatalogTables { Jobs = jobs, ViewIds = viewIds, Items = items, Emotes = emotes, Skills = skills, Scales = scales, Own = own };
            t.Mark(RoCatalogNeeds.Jobs, jobs != null);
            t.Mark(RoCatalogNeeds.ViewIds, viewIds != null);
            t.Mark(RoCatalogNeeds.Items, items != null);
            t.Mark(RoCatalogNeeds.Emotes, emotes != null);
            t.Mark(RoCatalogNeeds.Skills, skills != null);
            t.Mark(RoCatalogNeeds.Scales, scales != null);
            t.Mark(RoCatalogNeeds.Own, own != null);
            return t;
        }

        void Mark(RoCatalogNeeds group, bool loaded)
        {
            if (loaded) Loaded |= group;
            else Missing |= group;
        }

        // The file a reader's reason starts with ("accname.lub: line 3: …" → "accname.lub").
        static string FileOf(string reason)
        {
            int colon = reason?.IndexOf(':') ?? -1;
            return colon > 0 ? reason.Substring(0, colon) : reason ?? string.Empty;
        }

        sealed class Loader
        {
            readonly IRoSource _data, _system;
            readonly RoReport _report;

            public Loader(IRoSource data, IRoSource system, RoReport report)
            {
                _data = data;
                _system = system;
                _report = report;
            }

            public string Table(string relative) => Lub(_data, TablesFolder + relative, RoLubEncoding.Cp949, TablesFolder + relative);

            public string System(string relative, RoLubEncoding encoding) =>
                _system == null ? Missing("System/" + relative, "no System folder") : Lub(_system, relative, encoding, "System/" + relative);

            public string Disk(string path, string name)
            {
                try
                {
                    if (!File.Exists(path)) return Missing(name, $"not found: {path}");
                    return File.ReadAllText(path);
                }
                catch (Exception e)
                {
                    _report.Warn(RoIssueCode.CorruptFile, name, Referrer, $"{name} could not be read: {e.Message}");
                    return null;
                }
            }

            public void Corrupt(string relative, string reason, bool system = false) =>
                _report.Warn(RoIssueCode.CorruptFile, (system ? "System/" : TablesFolder) + relative, Referrer, reason);

            string Lub(IRoSource source, string relative, RoLubEncoding encoding, string shown)
            {
                var path = RoPath.Parse(relative);
                if (!source.TryGetFile(path, out var file)) return Missing(shown, "the table file is missing");
                byte[] bytes;
                try
                {
                    bytes = file.ReadAllBytes();
                }
                catch (Exception e)
                {
                    _report.Warn(RoIssueCode.CorruptFile, shown, Referrer, $"{shown} could not be read: {e.Message}");
                    return null;
                }
                if (RoLubReader.TryRead(path, bytes, encoding, out string lua, out string reason)) return lua;
                _report.Warn(RoIssueCode.CorruptFile, shown, Referrer, $"{shown}: {reason}");
                return null;
            }

            string Missing(string item, string message)
            {
                _report.Warn(RoIssueCode.SourceMissing, item, Referrer, $"{item}: {message}");
                return null;
            }
        }
    }
}
