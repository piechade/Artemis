﻿using System;
using System.Drawing;
using Artemis.Core.Plugins.Models;
using Artemis.Core.ProfileElements;
using RGB.NET.Core;

namespace Artemis.Core.Plugins.Abstract
{
    public abstract class ProfileModule : Module
    {
        protected ProfileModule(PluginInfo pluginInfo) : base(pluginInfo)
        {
        }

        public Profile ActiveProfile { get; private set; }

        /// <inheritdoc />
        public override void Update(double deltaTime)
        {
            lock (this)
            {
                // Update the profile
                ActiveProfile?.Update(deltaTime);
            }
        }

        /// <inheritdoc />
        public override void Render(double deltaTime, RGBSurface surface, Graphics graphics)
        {
            lock (this)
            {
                // Render the profile
                ActiveProfile?.Render(deltaTime, surface, graphics);
            }
        }

        public void ChangeActiveProfile(Profile profile)
        {
            lock (this)
            {
                if (profile == null)
                    throw new ArgumentNullException(nameof(profile));

                ActiveProfile?.Deactivate();

                ActiveProfile = profile;
                ActiveProfile.Activate();
            }
        }
    }
}