﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using Artemis.Core;
using Artemis.Core.Modules;
using Artemis.Core.Services;
using Artemis.UI.Events;
using Artemis.UI.Ninject.Factories;
using Artemis.UI.Screens.Home;
using Artemis.UI.Screens.News;
using Artemis.UI.Screens.Settings;
using Artemis.UI.Screens.SurfaceEditor;
using Artemis.UI.Screens.Workshop;
using Artemis.UI.Shared;
using MaterialDesignExtensions.Controls;
using MaterialDesignExtensions.Model;
using MaterialDesignThemes.Wpf;
using Ninject;
using Stylet;

namespace Artemis.UI.Screens.Sidebar
{
    public sealed class SidebarViewModel : Screen, IHandle<RequestSelectSidebarItemEvent>, IDisposable
    {
        private readonly Timer _activeModulesUpdateTimer;
        private readonly IKernel _kernel;
        private readonly IModuleVmFactory _moduleVmFactory;
        private readonly IPluginManagementService _pluginManagementService;
        private readonly IModuleService _moduleService;
        private string _activeModules;
        private bool _isSidebarOpen;
        private IScreen _selectedItem;
        private BindableCollection<INavigationItem> _sidebarItems;
        private Dictionary<INavigationItem, Module> _sidebarModules;

        public SidebarViewModel(IKernel kernel, ISettingsService settingsService, IEventAggregator eventAggregator, IModuleVmFactory moduleVmFactory, IPluginManagementService pluginManagementService, IModuleService moduleService)
        {
            _kernel = kernel;
            _moduleVmFactory = moduleVmFactory;
            _pluginManagementService = pluginManagementService;
            _moduleService = moduleService;

            SidebarModules = new Dictionary<INavigationItem, Module>();
            SidebarItems = new BindableCollection<INavigationItem>();
            PinSidebar = settingsService.GetSetting("UI.PinSidebar", false);
            PinSidebar.AutoSave = true;

            _activeModulesUpdateTimer = new Timer(1000);
            eventAggregator.Subscribe(this);
        }

        public PluginSetting<bool> PinSidebar { get; }

        public BindableCollection<INavigationItem> SidebarItems
        {
            get => _sidebarItems;
            set => SetAndNotify(ref _sidebarItems, value);
        }

        public Dictionary<INavigationItem, Module> SidebarModules
        {
            get => _sidebarModules;
            set => SetAndNotify(ref _sidebarModules, value);
        }

        public string ActiveModules
        {
            get => _activeModules;
            set => SetAndNotify(ref _activeModules, value);
        }

        public IScreen SelectedItem
        {
            get => _selectedItem;
            set => SetAndNotify(ref _selectedItem, value);
        }

        public bool IsSidebarOpen
        {
            get => _isSidebarOpen;
            set
            {
                SetAndNotify(ref _isSidebarOpen, value);
                if (value)
                    ActiveModulesUpdateTimerOnElapsed(this, EventArgs.Empty);
            }
        }

        public void SetupSidebar()
        {
            UpdateSidebarItems();

            // Set the sidebar as open if it's pinned
            if (PinSidebar.Value)
                IsSidebarOpen = true;

            // Select the top item, which will be one of the defaults
            Task.Run(() => SelectSidebarItem(SidebarItems[0]));
        }

        private void UpdateSidebarItems()
        {
            SidebarItems.Clear();
            SidebarModules.Clear();

            // Add all default sidebar items
            SidebarItems.Add(new FirstLevelNavigationItem { Icon = PackIconKind.Home, Label = "Home" });
            SidebarItems.Add(new FirstLevelNavigationItem { Icon = PackIconKind.Newspaper, Label = "News" });
            SidebarItems.Add(new FirstLevelNavigationItem { Icon = PackIconKind.TestTube, Label = "Workshop" });
            SidebarItems.Add(new FirstLevelNavigationItem { Icon = PackIconKind.Edit, Label = "Surface Editor" });
            SidebarItems.Add(new FirstLevelNavigationItem { Icon = PackIconKind.Settings, Label = "Settings" });

            // Add all activated modules
            SidebarItems.Add(new DividerNavigationItem());
            List<Module> modules = _pluginManagementService.GetFeaturesOfType<Module>().ToList();

            foreach (IGrouping<ModulePriorityCategory, Module> category in modules.OrderByDescending(m => m.PriorityCategory).GroupBy(m => m.PriorityCategory))
            {
                SidebarItems.Add(new SubheaderNavigationItem { Subheader = category.Key.ToString() });
                foreach(Module module in category.OrderBy(m => m.Priority))
                {
                    AddModule(module);
                }
            }
        }

        // ReSharper disable once UnusedMember.Global - Called by view
        public void SelectItem(WillSelectNavigationItemEventArgs args)
        {
            if (args.NavigationItemToSelect == null)
            {
                SelectedItem = null;
                return;
            }

            SelectSidebarItem(args.NavigationItemToSelect);
        }

        public void AddModule(Module module)
        {
            // Ensure the module is not already in the list
            if (SidebarModules.Any(io => io.Value == module))
                return;

            FirstLevelNavigationItem sidebarItem = new()
            {
                Icon = PluginUtilities.GetPluginIcon(module.Plugin, module.DisplayIcon),
                Label = module.DisplayName
            };
            SidebarItems.Add(sidebarItem);
            SidebarModules.Add(sidebarItem, module);
        }

        public void RemoveModule(Module module)
        {
            // If not in the list there's nothing to do
            if (SidebarModules.All(io => io.Value != module))
                return;

            KeyValuePair<INavigationItem, Module> existing = SidebarModules.First(io => io.Value == module);
            SidebarItems.Remove(existing.Key);
            SidebarModules.Remove(existing.Key);
        }

        private void ActiveModulesUpdateTimerOnElapsed(object sender, EventArgs e)
        {
            if (!IsSidebarOpen)
                return;

            int activeModules = SidebarModules.Count(m => m.Value.IsActivated);
            ActiveModules = activeModules == 1 ? "1 active module" : $"{activeModules} active modules";
        }

        private void SelectSidebarItem(INavigationItem sidebarItem)
        {
            // A module was selected if the dictionary contains the selected item
            if (SidebarModules.ContainsKey(sidebarItem))
                ActivateModule(sidebarItem);
            else if (sidebarItem is FirstLevelNavigationItem navigationItem)
                ActivateViewModel(navigationItem.Label);
            else
                SelectedItem = null;
        }

        private void ActivateViewModel(string label)
        {
            if (label == "Home")
                ActivateViewModel<HomeViewModel>();
            else if (label == "News")
                ActivateViewModel<NewsViewModel>();
            else if (label == "Workshop")
                ActivateViewModel<WorkshopViewModel>();
            else if (label == "Surface Editor")
                ActivateViewModel<SurfaceEditorViewModel>();
            else if (label == "Settings")
                ActivateViewModel<SettingsViewModel>();
        }

        private void ActivateViewModel<T>()
        {
            SelectedItem = (IScreen) _kernel.Get<T>();
        }

        private void ActivateModule(INavigationItem sidebarItem)
        {
            SelectedItem = SidebarModules.ContainsKey(sidebarItem) ? _moduleVmFactory.CreateModuleRootViewModel(SidebarModules[sidebarItem]) : null;
        }

        #region IDisposable

        public void Dispose()
        {
            _activeModulesUpdateTimer?.Dispose();
        }

        #endregion

        #region Event handlers

        private void OnFeatureEnabled(object sender, PluginFeatureEventArgs e)
        {
            if (e.PluginFeature is Module)
                UpdateSidebarItems();
        }

        private void OnFeatureDisabled(object sender, PluginFeatureEventArgs e)
        {
            if (e.PluginFeature is Module)
                UpdateSidebarItems();
        }

        private void OnModulePriorityUpdated(object sender, EventArgs e)
        {
            UpdateSidebarItems();
        }

        public void Handle(RequestSelectSidebarItemEvent message)
        {
            ActivateViewModel(message.Label);
        }

        #endregion

        #region Overrides of Screen

        /// <inheritdoc />
        protected override void OnInitialActivate()
        {
            _activeModulesUpdateTimer.Start();
            _activeModulesUpdateTimer.Elapsed += ActiveModulesUpdateTimerOnElapsed;

            _pluginManagementService.PluginFeatureEnabled += OnFeatureEnabled;
            _pluginManagementService.PluginFeatureDisabled += OnFeatureDisabled;
            _moduleService.ModulePriorityUpdated += OnModulePriorityUpdated;

            SetupSidebar();
            
            base.OnInitialActivate();
        }

        /// <inheritdoc />
        protected override void OnClose()
        {
            _activeModulesUpdateTimer.Stop();
            _activeModulesUpdateTimer.Elapsed -= ActiveModulesUpdateTimerOnElapsed;

            _pluginManagementService.PluginFeatureEnabled -= OnFeatureEnabled;
            _pluginManagementService.PluginFeatureDisabled -= OnFeatureDisabled;
            _moduleService.ModulePriorityUpdated -= OnModulePriorityUpdated;
            base.OnClose();
        }

        #endregion
    }
}