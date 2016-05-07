﻿using System.IO;
using System.Windows.Forms;
using Artemis.Managers;
using Artemis.Properties;
using Artemis.ViewModels.Abstract;

namespace Artemis.Modules.Games.CounterStrike
{
    public sealed class CounterStrikeViewModel : GameViewModel<CounterStrikeDataModel>
    {
        public CounterStrikeViewModel(MainManager mainManager, EffectManager effectManager,
            KeyboardManager keyboardManager)
            : base(
                mainManager, effectManager,
                new CounterStrikeModel(mainManager, new CounterStrikeSettings(), keyboardManager))
        {
            DisplayName = "CS:GO";

            // Create effect model and add it to MainManager
            EffectManager.EffectModels.Add(GameModel);
            PlaceConfigFile();
        }

        public void BrowseDirectory()
        {
            var dialog = new FolderBrowserDialog {SelectedPath = ((CounterStrikeSettings) GameSettings).GameDirectory};
            var result = dialog.ShowDialog();
            if (result != DialogResult.OK)
                return;

            ((CounterStrikeSettings) GameSettings).GameDirectory = dialog.SelectedPath;
            NotifyOfPropertyChange(() => GameSettings);

            GameSettings.Save();
            PlaceConfigFile();
        }

        public void PlaceConfigFile()
        {
            if (((CounterStrikeSettings) GameSettings).GameDirectory == string.Empty)
                return;
            if (Directory.Exists(((CounterStrikeSettings) GameSettings).GameDirectory + "/csgo/cfg"))
            {
                var cfgFile = Resources.csgoGamestateConfiguration.Replace("{{port}}",
                    MainManager.GameStateWebServer.Port.ToString());
                File.WriteAllText(
                    ((CounterStrikeSettings) GameSettings).GameDirectory + "/csgo/cfg/gamestate_integration_artemis.cfg",
                    cfgFile);

                return;
            }

            MainManager.DialogService.ShowErrorMessageBox("Please select a valid CS:GO directory\n\n" +
                                                          @"By default CS:GO is in \SteamApps\common\Counter-Strike Global Offensive");
            ((CounterStrikeSettings) GameSettings).GameDirectory = string.Empty;
            NotifyOfPropertyChange(() => GameSettings);

            GameSettings.Save();
        }
    }
}