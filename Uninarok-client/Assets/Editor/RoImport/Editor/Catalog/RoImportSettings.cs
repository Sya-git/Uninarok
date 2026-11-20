using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Uninarok.RoImport.Core.Catalog.Tables;
using Uninarok.RoImport.Editor.Pipeline;

namespace Uninarok.RoImport.Editor.Catalog
{
    /// <summary>
    /// The project's Ragnarok import settings (SPEC-0008 FR-12; PLAN-0008 4.8, D8), kept in
    /// <c>ProjectSettings/UninarokRoImport.asset</c> and committed, so a fresh clone uses the same item table as the team.
    /// A project without the file uses <see cref="DefaultLanguage"/>. <see cref="RoImportOptions.ItemTableLanguage"/>
    /// overrides the setting for one run.
    /// </summary>
    [FilePath("ProjectSettings/UninarokRoImport.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class RoImportSettings : ScriptableSingleton<RoImportSettings>
    {
        public const RoItemTableLanguage DefaultLanguage = RoItemTableLanguage.Portuguese;

        [SerializeField] RoItemTableLanguage itemTableLanguage = DefaultLanguage;

        public RoItemTableLanguage ItemTableLanguage
        {
            get => itemTableLanguage;
            set
            {
                if (itemTableLanguage == value) return;
                itemTableLanguage = value;
                Save(true);
            }
        }

        /// <summary>The item table file under <c>System/</c> for a language (<c>iteminfo_new.lub</c>, <c>english/…</c>, <c>spanish/…</c>).</summary>
        public static string ItemTableFile(RoItemTableLanguage language) => RoItemTable.FileName(language);

        /// <summary>The language of one run: the override when given, else the setting.</summary>
        public static RoItemTableLanguage LanguageFor(RoImportOptions options) => Effective(options?.ItemTableLanguage, instance.ItemTableLanguage);

        /// <summary>The override beats the setting (FR-12).</summary>
        public static RoItemTableLanguage Effective(RoItemTableLanguage? overrideLanguage, RoItemTableLanguage setting) => overrideLanguage ?? setting;
    }

    /// <summary>The settings page <c>Project/Uninarok/Ragnarok Import</c> (PLAN-0008 4.8): the item table language.</summary>
    sealed class RoImportSettingsProvider : SettingsProvider
    {
        public const string PagePath = "Project/Uninarok/Ragnarok Import";

        static readonly GUIContent LanguageLabel = new GUIContent("Item table language",
            "Which System/…/iteminfo_new.lub the sprite catalog reads (ground items and equipment). Changing it re-imports the records that depend on it.");

        RoImportSettingsProvider() : base(PagePath, SettingsScope.Project, new[] { "Ragnarok", "item table", "language", "catalog" }) { }

        [SettingsProvider]
        static SettingsProvider Create() => new RoImportSettingsProvider();

        public override void OnGUI(string searchContext)
        {
            var settings = RoImportSettings.instance;
            EditorGUILayout.Space();
            var language = (RoItemTableLanguage)EditorGUILayout.EnumPopup(LanguageLabel, settings.ItemTableLanguage);
            if (language != settings.ItemTableLanguage) settings.ItemTableLanguage = language;
            EditorGUILayout.LabelField("File", "System/" + RoImportSettings.ItemTableFile(language), EditorStyles.miniLabel);
        }
    }
}
