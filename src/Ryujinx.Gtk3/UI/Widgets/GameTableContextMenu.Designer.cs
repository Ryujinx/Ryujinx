using Gtk;
using System;

namespace Ryujinx.UI.Widgets
{
    public partial class GameTableContextMenu : Menu
    {
        private MenuItem _openSaveUserDirMenuItem;
        private MenuItem _openSaveDeviceDirMenuItem;
        private MenuItem _openSaveBcatDirMenuItem;
        private MenuItem _manageTitleUpdatesMenuItem;
        private MenuItem _manageDlcMenuItem;
        private MenuItem _manageCheatMenuItem;
        private MenuItem _openTitleModDirMenuItem;
        private MenuItem _openTitleSdModDirMenuItem;
        private Menu _extractSubMenu;
        private MenuItem _extractMenuItem;
        private MenuItem _extractRomFsMenuItem;
        private MenuItem _extractExeFsMenuItem;
        private MenuItem _extractLogoMenuItem;
        private Menu _manageSubMenu;
        private MenuItem _manageCacheMenuItem;
        private MenuItem _purgePtcCacheMenuItem;
        private MenuItem _purgeShaderCacheMenuItem;
        private MenuItem _openPtcDirMenuItem;
        private MenuItem _openShaderCacheDirMenuItem;
        private MenuItem _createShortcutMenuItem;

        private void InitializeComponent()
        {
            //
            // _openSaveUserDirMenuItem
            //
            _openSaveUserDirMenuItem = new MenuItem("Open User Save Directory")
            {
                TooltipText = "Open the directory which contains Application's User Saves.",
            };
            _openSaveUserDirMenuItem.Activated += OpenSaveUserDir_Clicked;

            //
            // _openSaveDeviceDirMenuItem
            //
            _openSaveDeviceDirMenuItem = new MenuItem("Open Device Save Directory")
            {
                TooltipText = "Open the directory which contains Application's Device Saves.",
            };
            _openSaveDeviceDirMenuItem.Activated += OpenSaveDeviceDir_Clicked;

            //
            // _openSaveBcatDirMenuItem
            //
            _openSaveBcatDirMenuItem = new MenuItem("Open BCAT Save Directory")
            {
                TooltipText = "Open the directory which contains Application's BCAT Saves.",
            };
            _openSaveBcatDirMenuItem.Activated += OpenSaveBcatDir_Clicked;

            //
            // _manageTitleUpdatesMenuItem
            //
            _manageTitleUpdatesMenuItem = new MenuItem("Manage Title Updates")
            {
                TooltipText = "Open the Title Update management window",
            };
            _manageTitleUpdatesMenuItem.Activated += ManageTitleUpdates_Clicked;

            //
            // _manageDlcMenuItem
            //
            _manageDlcMenuItem = new MenuItem("Manage DLC")
            {
                TooltipText = "Open the DLC management window",
            };
            _manageDlcMenuItem.Activated += ManageDlc_Clicked;

            //
            // _manageCheatMenuItem
            //
            _manageCheatMenuItem = new MenuItem("Manage Cheats")
            {
                TooltipText = "Open the Cheat management window",
            };
            _manageCheatMenuItem.Activated += ManageCheats_Clicked;

            //
            // _openTitleModDirMenuItem
            //
            _openTitleModDirMenuItem = new MenuItem("Open Mods Directory")
            {
                TooltipText = "Open the directory which contains Application's Mods.",
            };
            _openTitleModDirMenuItem.Activated += OpenTitleModDir_Clicked;

            //
            // _openTitleSdModDirMenuItem
            //
            _openTitleSdModDirMenuItem = new MenuItem("Open Atmosphere Mods Directory")
            {
                TooltipText = "Open the alternative SD card atmosphere directory which contains the Application's Mods.",
            };
            _openTitleSdModDirMenuItem.Activated += OpenTitleSdModDir_Clicked;

            //
            // _extractSubMenu
            //
            _extractSubMenu = new Menu();

            //
            // _extractMenuItem
            //
            _extractMenuItem = new MenuItem("Extract Data")
            {
                Submenu = _extractSubMenu
            };

            //
            // _extractRomFsMenuItem
            //
            _extractRomFsMenuItem = new MenuItem("RomFS")
            {
                TooltipText = "Extract the RomFS section from Application's current config (including updates).",
            };
            _extractRomFsMenuItem.Activated += ExtractRomFs_Clicked;

            //
            // _extractExeFsMenuItem
            //
            _extractExeFsMenuItem = new MenuItem("ExeFS")
            {
                TooltipText = "Extract the ExeFS section from Application's current config (including updates).",
            };
            _extractExeFsMenuItem.Activated += ExtractExeFs_Clicked;

            //
            // _extractLogoMenuItem
            //
            _extractLogoMenuItem = new MenuItem("Logo")
            {
                TooltipText = "Extract the Logo section from Application's current config (including updates).",
            };
            _extractLogoMenuItem.Activated += ExtractLogo_Clicked;

            //
            // _manageSubMenu
            //
            _manageSubMenu = new Menu();

            //
            // _manageCacheMenuItem
            //
            _manageCacheMenuItem = new MenuItem("Cache Management")
            {
                Submenu = _manageSubMenu,
            };

            //
            // _purgePtcCacheMenuItem
            //
            _purgePtcCacheMenuItem = new MenuItem("Queue PPTC Rebuild")
            {
                TooltipText = "Trigger PPTC to rebuild at boot time on the next game launch.",
            };
            _purgePtcCacheMenuItem.Activated += PurgePtcCache_Clicked;

            //
            // _purgeShaderCacheMenuItem
            //
            _purgeShaderCacheMenuItem = new MenuItem("Purge Shader Cache")
            {
                TooltipText = "Delete the Application's shader cache.",
            };
            _purgeShaderCacheMenuItem.Activated += PurgeShaderCache_Clicked;

            //
            // _openPtcDirMenuItem
            //
            _openPtcDirMenuItem = new MenuItem("Open PPTC Directory")
            {
                TooltipText = "Open the directory which contains the Application's PPTC cache.",
            };
            _openPtcDirMenuItem.Activated += OpenPtcDir_Clicked;

            //
            // _openShaderCacheDirMenuItem
            //
            _openShaderCacheDirMenuItem = new MenuItem("Open Shader Cache Directory")
            {
                TooltipText = "Open the directory which contains the Application's shader cache.",
            };
            _openShaderCacheDirMenuItem.Activated += OpenShaderCacheDir_Clicked;

            //
            // _createShortcutMenuItem
            //
            _createShortcutMenuItem = new MenuItem("Create Application Shortcut")
            {
                TooltipText = OperatingSystem.IsMacOS() ? "Create a shortcut in macOS's Applications folder that launches the selected Application" : "Create a Desktop Shortcut that launches the selected Application."
            };
            _createShortcutMenuItem.Activated += CreateShortcut_Clicked;

            ShowComponent();
        }

        private void ShowComponent()
        {
            _extractSubMenu.Append(_extractExeFsMenuItem);
            _extractSubMenu.Append(_extractRomFsMenuItem);
            _extractSubMenu.Append(_extractLogoMenuItem);

            _manageSubMenu.Append(_purgePtcCacheMenuItem);
            _manageSubMenu.Append(_purgeShaderCacheMenuItem);
            _manageSubMenu.Append(_openPtcDirMenuItem);
            _manageSubMenu.Append(_openShaderCacheDirMenuItem);

            Add(_createShortcutMenuItem);
            Add(new SeparatorMenuItem());
            Add(_openSaveUserDirMenuItem);
            Add(_openSaveDeviceDirMenuItem);
            Add(_openSaveBcatDirMenuItem);
            Add(new SeparatorMenuItem());
            Add(_manageTitleUpdatesMenuItem);
            Add(_manageDlcMenuItem);
            Add(_manageCheatMenuItem);
            Add(_openTitleModDirMenuItem);
            Add(_openTitleSdModDirMenuItem);
            Add(new SeparatorMenuItem());
            Add(_manageCacheMenuItem);
            Add(_extractMenuItem);

            ShowAll();
        }
    }
}
