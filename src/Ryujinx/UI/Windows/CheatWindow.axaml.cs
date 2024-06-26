using Avalonia.Collections;
using LibHac.Tools.FsSystem;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Models;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE.HOS;
using Ryujinx.UI.App.Common;
using Ryujinx.UI.Common.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Ryujinx.Ava.UI.Windows
{
    public partial class CheatWindow : StyleableWindow
    {
        private readonly string _enabledCheatsPath;
        public bool NoCheatsFound { get; }

        public AvaloniaList<CheatNode> LoadedCheats { get; }

        public string Heading { get; }
        public string BuildId { get; }

        public CheatWindow()
        {
            DataContext = this;

            InitializeComponent();

            Title = $"Ryujinx {Program.Version} - " + LocaleManager.Instance[LocaleKeys.CheatWindowTitle];
        }

        public CheatWindow(VirtualFileSystem virtualFileSystem, string titleId, string titleName, string titlePath)
        {
            LoadedCheats = new AvaloniaList<CheatNode>();
            IntegrityCheckLevel checkLevel = ConfigurationState.Instance.System.EnableFsIntegrityChecks
                ? IntegrityCheckLevel.ErrorOnInvalid
                : IntegrityCheckLevel.None;

            Heading = LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.CheatWindowHeading, titleName, titleId.ToUpper());
            BuildId = ApplicationData.GetBuildId(virtualFileSystem, checkLevel, titlePath);

            InitializeComponent();

            string modsBasePath = ModLoader.GetModsBasePath();
            string titleModsPath = ModLoader.GetApplicationDir(modsBasePath, titleId);
            ulong titleIdValue = ulong.Parse(titleId, NumberStyles.HexNumber);

            _enabledCheatsPath = Path.Combine(titleModsPath, "cheats", "enabled.txt");

            string[] enabled = Array.Empty<string>();

            if (File.Exists(_enabledCheatsPath))
            {
                enabled = File.ReadAllLines(_enabledCheatsPath);
            }

            int cheatAdded = 0;

            var mods = new ModLoader.ModCache();

            ModLoader.QueryContentsDir(mods, new DirectoryInfo(Path.Combine(modsBasePath, "contents")), titleIdValue);

            string currentCheatFile = string.Empty;
            string buildId = string.Empty;

            CheatNode currentGroup = null;

            foreach (var cheat in mods.Cheats)
            {
                if (cheat.Path.FullName != currentCheatFile)
                {
                    currentCheatFile = cheat.Path.FullName;
                    string parentPath = currentCheatFile.Replace(titleModsPath, "");

                    buildId = Path.GetFileNameWithoutExtension(currentCheatFile).ToUpper();
                    currentGroup = new CheatNode("", buildId, parentPath, true);

                    LoadedCheats.Add(currentGroup);
                }

                var model = new CheatNode(cheat.Name, buildId, "", false, enabled.Contains($"{buildId}-{cheat.Name}"));
                currentGroup?.SubNodes.Add(model);

                cheatAdded++;
            }

            if (cheatAdded == 0)
            {
                NoCheatsFound = true;
            }

            DataContext = this;

            Title = $"Ryujinx {Program.Version} - " + LocaleManager.Instance[LocaleKeys.CheatWindowTitle];
        }

        public void Save()
        {
            if (NoCheatsFound)
            {
                return;
            }

            List<string> enabledCheats = new();

            foreach (var cheats in LoadedCheats)
            {
                foreach (var cheat in cheats.SubNodes)
                {
                    if (cheat.IsEnabled)
                    {
                        enabledCheats.Add(cheat.BuildIdKey);
                    }
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_enabledCheatsPath));

            File.WriteAllLines(_enabledCheatsPath, enabledCheats);

            Close();
        }
    }
}
