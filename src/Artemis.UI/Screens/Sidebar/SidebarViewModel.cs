﻿using System;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Artemis.Core;
using Artemis.Core.Services;
using Artemis.UI.Events;
using Artemis.UI.Extensions;
using Artemis.UI.Ninject.Factories;
using Artemis.UI.Screens.Home;
using Artemis.UI.Screens.ProfileEditor;
using Artemis.UI.Screens.Settings;
using Artemis.UI.Screens.Sidebar.Dialogs;
using Artemis.UI.Screens.SurfaceEditor;
using Artemis.UI.Screens.Workshop;
using Artemis.UI.Shared.Services;
using GongSolutions.Wpf.DragDrop;
using MaterialDesignThemes.Wpf;
using Ninject;
using RGB.NET.Core;
using Stylet;

namespace Artemis.UI.Screens.Sidebar
{
    public sealed class SidebarViewModel : Conductor<SidebarCategoryViewModel>.Collection.AllActive, IHandle<RequestSelectSidebarItemEvent>, IDropTarget
    {
        private readonly IKernel _kernel;
        private readonly ISidebarVmFactory _sidebarVmFactory;
        private readonly IRgbService _rgbService;
        private readonly IProfileService _profileService;
        private readonly IProfileEditorService _profileEditorService;
        private readonly IDialogService _dialogService;
        private ArtemisDevice _headerDevice;
        private SidebarScreenViewModel _selectedSidebarScreen;
        private MainScreenViewModel _selectedScreen;
        private readonly SidebarScreenViewModel<ProfileEditorViewModel> _profileEditor;
        private readonly DefaultDropHandler _defaultDropHandler;
        private bool _activatingProfile;

        public SidebarViewModel(IKernel kernel,
            IEventAggregator eventAggregator,
            ISidebarVmFactory sidebarVmFactory,
            IRgbService rgbService,
            IProfileService profileService,
            IProfileEditorService profileEditorService,
            IDialogService dialogService)
        {
            _kernel = kernel;
            _sidebarVmFactory = sidebarVmFactory;
            _rgbService = rgbService;
            _profileService = profileService;
            _profileEditorService = profileEditorService;
            _dialogService = dialogService;
            _profileEditor = new SidebarScreenViewModel<ProfileEditorViewModel>(PackIconKind.Wrench, "Profile Editor");
            _defaultDropHandler = new DefaultDropHandler();

            eventAggregator.Subscribe(this);

            SidebarScreens = new BindableCollection<SidebarScreenViewModel>
            {
                new SidebarScreenViewModel<HomeViewModel>(PackIconKind.Home, "Home"),
                new SidebarScreenViewModel<WorkshopViewModel>(PackIconKind.TestTube, "Workshop"),
                new SidebarScreenViewModel<SurfaceEditorViewModel>(PackIconKind.Devices, "Surface Editor"),
                new SidebarScreenViewModel<SettingsViewModel>(PackIconKind.Cog, "Settings")
            };
            SelectedSidebarScreen = SidebarScreens.First();
            UpdateProfileCategories();
            UpdateHeaderDevice();
        }

        private void UpdateHeaderDevice()
        {
            HeaderDevice = _rgbService.Devices.FirstOrDefault(d => d.DeviceType == RGBDeviceType.Keyboard && d.Layout is {IsValid: true});
        }

        public ArtemisDevice HeaderDevice
        {
            get => _headerDevice;
            set => SetAndNotify(ref _headerDevice, value);
        }

        public BindableCollection<SidebarScreenViewModel> SidebarScreens { get; }

        public MainScreenViewModel SelectedScreen
        {
            get => _selectedScreen;
            private set => SetAndNotify(ref _selectedScreen, value);
        }

        public SidebarScreenViewModel SelectedSidebarScreen
        {
            get => _selectedSidebarScreen;
            set
            {
                if (SetAndNotify(ref _selectedSidebarScreen, value))
                    ActivateScreenViewModel(_selectedSidebarScreen);
            }
        }

        public bool ActivatingProfile
        {
            get => _activatingProfile;
            set => SetAndNotify(ref _activatingProfile, value);
        }

        private void ActivateScreenViewModel(SidebarScreenViewModel screenViewModel)
        {
            SelectedScreen = screenViewModel.CreateInstance(_kernel);
            OnSelectedScreenChanged();
            if (screenViewModel != _profileEditor)
                SelectProfileConfiguration(null);
        }

        private void UpdateProfileCategories()
        {
            foreach (ProfileCategory profileCategory in _profileService.ProfileCategories.OrderBy(p => p.Order))
                AddProfileCategoryViewModel(profileCategory);
        }

        public async Task AddCategory()
        {
            object result = await _dialogService.ShowDialog<SidebarCategoryCreateViewModel>();
            if (result is ProfileCategory profileCategory)
                AddProfileCategoryViewModel(profileCategory);

            ((BindableCollection<SidebarCategoryViewModel>) Items).Sort(p => p.ProfileCategory.Order);
        }

        public void OpenUrl(string url)
        {
            Core.Utilities.OpenUrl(url);
        }

        public void Handle(RequestSelectSidebarItemEvent message)
        {
            SidebarScreenViewModel requested = null;
            if (message.DisplayName != null)
                requested = SidebarScreens.FirstOrDefault(s => s.DisplayName == message.DisplayName);
            else
                requested = message.ViewModel;

            if (requested != null)
                SelectedSidebarScreen = requested;
        }

        public SidebarCategoryViewModel AddProfileCategoryViewModel(ProfileCategory profileCategory)
        {
            SidebarCategoryViewModel viewModel = _sidebarVmFactory.SidebarCategoryViewModel(profileCategory);
            Items.Add(viewModel);
            return viewModel;
        }

        public void RemoveProfileCategoryViewModel(SidebarCategoryViewModel viewModel)
        {
            Items.Remove(viewModel);
        }

        public void SelectProfileConfiguration(ProfileConfiguration profileConfiguration)
        {
            foreach (SidebarCategoryViewModel sidebarCategoryViewModel in Items)
                sidebarCategoryViewModel.SelectedProfileConfiguration = sidebarCategoryViewModel.Items.FirstOrDefault(i => i.ProfileConfiguration == profileConfiguration);

            if (_profileEditorService.SuspendEditing)
                _profileEditorService.SuspendEditing = false;

            Task.Run(() =>
            {
                try
                {
                    ActivatingProfile = true;
                    _profileEditorService.ChangeSelectedProfileConfiguration(profileConfiguration);
                }
                finally
                {
                    ActivatingProfile = false;
                }

                if (profileConfiguration == null)
                    return;

                Execute.PostToUIThread(() =>
                {
                    // Little workaround to clear the selected item in the menu, ugly but oh well
                    if (_selectedSidebarScreen != _profileEditor)
                    {
                        _selectedSidebarScreen = null;
                        NotifyOfPropertyChange(nameof(SelectedSidebarScreen));
                    }

                    SelectedSidebarScreen = _profileEditor;
                });
            });
        }

        #region Overrides of Screen

        /// <inheritdoc />
        protected override void OnInitialActivate()
        {
            base.OnInitialActivate();
        }

        /// <inheritdoc />
        protected override void OnClose()
        {
            base.OnClose();
        }

        #endregion

        #region Events

        public event EventHandler SelectedScreenChanged;

        private void OnSelectedScreenChanged()
        {
            SelectedScreenChanged?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region Implementation of IDropTarget

        /// <inheritdoc />
        public void DragOver(IDropInfo dropInfo)
        {
            _defaultDropHandler.DragOver(dropInfo);
        }

        /// <inheritdoc />
        public void Drop(IDropInfo dropInfo)
        {
            _defaultDropHandler.Drop(dropInfo);
            for (int index = 0; index < Items.Count; index++)
                Items[index].ProfileCategory.Order = index;

            // Bit dumb but gets the job done
            foreach (SidebarCategoryViewModel viewModel in Items)
                _profileService.SaveProfileCategory(viewModel.ProfileCategory);
        }

        #endregion
    }
}