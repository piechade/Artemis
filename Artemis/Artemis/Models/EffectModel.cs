﻿using System;
using System.Drawing;
using Artemis.Managers;

namespace Artemis.Models
{
    public abstract class EffectModel : IDisposable
    {
        public delegate void SettingsUpdateHandler(EffectSettings settings);

        public bool Initialized;

        protected KeyboardManager KeyboardManager;

        public MainManager MainManager;
        public string Name;

        protected EffectModel(MainManager mainManager, KeyboardManager keyboardManager)
        {
            MainManager = mainManager;
            KeyboardManager = keyboardManager;
        }

        public abstract void Dispose();

        // Called on creation
        public abstract void Enable();

        // Called every iteration
        public abstract void Update();

        // Called after every update
        public abstract Bitmap GenerateBitmap();
    }
}