﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Artemis.Events;
using Artemis.Models;
using Artemis.Modules.Effects.ProfilePreview;
using Artemis.Settings;
using Caliburn.Micro;
using Ninject;
using NLog;
using LogManager = NLog.LogManager;

namespace Artemis.Managers
{
    public class EffectManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly IEventAggregator _events;
        private EffectModel _activeEffect;
        private bool _clearing;

        public EffectManager(IEventAggregator events)
        {
            Logger.Info("Intializing EffectManager");
            _events = events;

            EffectModels = new List<EffectModel>();
            //ProfilePreviewModel = new ProfilePreviewModel(MainManager.Value);
            Logger.Info("Intialized EffectManager");
        }

        [Inject]
        public Lazy<MainManager> MainManager { get; set; }

        public EffectModel PauseEffect { get; set; }

        /// <summary>
        ///     Used by ViewModels to show a preview of the profile currently being edited
        /// </summary>
        public ProfilePreviewModel ProfilePreviewModel { get; set; }

        /// <summary>
        ///     Holds all the effects the program has
        /// </summary>
        public List<EffectModel> EffectModels { get; set; }

        public EffectModel ActiveEffect
        {
            get { return _activeEffect; }
            private set
            {
                _activeEffect = value;
                _events.PublishOnUIThread(new ActiveEffectChanged(value?.Name));
            }
        }

        /// <summary>
        ///     Returns all enabled overlays
        /// </summary>
        public IEnumerable<OverlayModel> EnabledOverlays
        {
            get { return EffectModels.OfType<OverlayModel>().Where(o => o.Enabled); }
        }

        /// <summary>
        ///     Returns all enabled games
        /// </summary>
        public IEnumerable<GameModel> EnabledGames
        {
            get { return EffectModels.OfType<GameModel>().Where(g => g.Enabled); }
        }

        /// <summary>
        ///     Loads the last active effect from settings and enables it.
        /// </summary>
        /// <returns>Whether enabling was successful or not.</returns>
        public EffectModel GetLastEffect()
        {
            Logger.Debug("Getting last effect: {0}", General.Default.LastEffect);
            if (General.Default.LastEffect == null)
                return null;

            var effect = EffectModels.FirstOrDefault(e => e.Name == General.Default.LastEffect);

            // Fall back to the first effect found, in case settings are messed up
            return effect;
        }

        /// <summary>
        ///     Disables the current effect and changes it to the provided effect.
        /// </summary>
        /// <param name="effectModel"></param>
        /// <param name="force">Changes the effect, even if it's already running (effectively restarting it)</param>
        public void ChangeEffect(EffectModel effectModel, bool force = false)
        {
            if (effectModel is OverlayModel)
                throw new ArgumentException("Can't set an Overlay effect as the active effect");

            // Game models are only used if they are enabled
            var gameModel = effectModel as GameModel;
            if (gameModel != null)
                if (!gameModel.Enabled)
                    return;

            if (ActiveEffect != null)
                if (effectModel.Name == ActiveEffect.Name && !force)
                    return;

            Logger.Debug("Changing effect to: {0}, force: {1}", effectModel?.Name, force);
            // If the main manager is running, pause it and safely change the effect
            if (MainManager.Value.Running)
            {
                ChangeEffectWithPause(effectModel);
                return;
            }

            // If it's not running start it, and let the next recursion handle changing the effect
            MainManager.Value.Start(effectModel);
        }

        private void ChangeEffectWithPause(EffectModel effectModel)
        {
            var tryCount = 0;
            while (PauseEffect != null)
            {
                Thread.Sleep(500);
                tryCount++;
                if (tryCount > 20)
                    throw new Exception("Couldn't change effect before the time expired");
            }

            // Don't interrupt an ongoing effect change
            if (PauseEffect != null)
            {
                Logger.Debug("Change effect with pause cancelled");
                return;
            }
            Logger.Debug("Changing effect with pause: {0}", effectModel?.Name);

            PauseEffect = effectModel;
            MainManager.Value.Pause();
            MainManager.Value.PauseCallback += ChangeEffectPauseCallback;
        }

        private void ChangeEffectPauseCallback()
        {
            MainManager.Value.PauseCallback -= ChangeEffectPauseCallback;

            // Change effect logic
            ActiveEffect?.Dispose();

            ActiveEffect = PauseEffect;
            ActiveEffect?.Enable();

            MainManager.Value.Unpause();
            PauseEffect = null;

            Logger.Debug("Finishing change effect with pause");
            if (ActiveEffect is GameModel || ActiveEffect is ProfilePreviewModel)
                return;

            // Non-game effects are stored as the new LastEffect.
            General.Default.LastEffect = ActiveEffect?.Name;
            General.Default.Save();
        }

        /// <summary>
        ///     Clears the current effect
        /// </summary>
        public void ClearEffect()
        {
            if (_clearing)
                return;

            // Don't mess with the ActiveEffect if in the process of changing the effect.
            if (PauseEffect != null)
                return;

            if (ActiveEffect == null)
                return;

            _clearing = true;
            Logger.Debug("Clearing active effect");
            MainManager.Value.Pause();
            MainManager.Value.PauseCallback += ClearEffectPauseCallback;
        }

        private void ClearEffectPauseCallback()
        {
            MainManager.Value.PauseCallback -= ClearEffectPauseCallback;
            if (PauseEffect != null)
            {
                Logger.Debug("Cancelling clearing effect");
                return;
            }

            ActiveEffect.Dispose();
            ActiveEffect = null;

            General.Default.LastEffect = null;
            General.Default.Save();

            _clearing = false;

            Logger.Debug("Finishing clearing active effect");
            MainManager.Value.Unpause();
        }

        /// <summary>
        ///     Disables the given game
        /// </summary>
        /// <param name="activeEffect"></param>
        public void DisableGame(EffectModel activeEffect)
        {
            Logger.Debug("Disabling game: {0}", activeEffect?.Name);
            if (GetLastEffect() == null)
                ClearEffect();
            else
                ChangeEffect(GetLastEffect());
        }

        /// <summary>
        ///     Disables the current ActiveEffect if it's a game that is disabled.
        /// </summary>
        public void DisableInactiveGame()
        {
            if (!(ActiveEffect is GameModel))
                return;
            if (EnabledGames.Contains(ActiveEffect))
                return;

            DisableGame(ActiveEffect);
        }
    }
}