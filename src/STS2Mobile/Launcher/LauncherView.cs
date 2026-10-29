using System;
using Godot;
using STS2Mobile.Launcher.Components;
using STS2Mobile.Launcher.Sections;
using System.Collections.Generic;

namespace STS2Mobile.Launcher;
// Owns launcher view references and constructor wiring.
internal sealed partial class LauncherView
{
    // The step of the sign-in -> download -> play journey that Home shows.
    // Each stage decides which Home sections and actions are visible.
    private enum HomeStage
    {
        SetupGuide,
        SignIn,
        SteamGuard,
        Download,
        Ready,
        Retry,
    }

    private LoginSection Login { get; }
    private CodeSection Code { get; }
    private DownloadSection Download { get; }
    private ActionSection Actions { get; }
    private VBoxContainer HomeSections { get; }
    private ScrollContainer PrimaryScroll { get; }
    private Control FirstRunGuide { get; }
    private RichTextLabel Log { get; }
    private VBoxContainer DiagnosticsDrawer { get; }
    private Button DiagnosticsToggle { get; }

    private readonly Control _parent;
    private readonly StyledPanel _panel;
    internal Node LaunchLifetimeHost => _panel;

    private readonly ColorRect _androidCompositionRefresh;
    private float _keyboardOffset;
    private readonly float _scale;
    private readonly LauncherLayoutProfile _profile;
    private readonly LauncherViewPrimaryStatus _status;
    private readonly LauncherViewSecondaryStatus _secondaryStatus;
    private readonly Control _destinationSafeAreaSpacer;
    private readonly Button[] _destinationButtons;
    private Vector4 _systemSafeAreaInsets = new(-1, -1, -1, -1);
    private int _androidCompositionRefreshFrames;
    private LauncherDestination _destination;
    // Null while the session is connecting and every Home section is hidden.
    private HomeStage? _homeStage;
    private LauncherVersionsState _versions = LauncherVersionsState.Initial;
    private Control _keyboardFocusScrollTarget;
    private float _keyboardFocusScrollOffset = -1f;
    internal LauncherView(Control parent, LauncherLayoutProfile profile)
    {
        var dismissKeyboard = new Action<InputEvent>(DismissKeyboard);
        var shell = BuildShell(parent, profile, dismissKeyboard);
        _parent = parent;
        _panel = shell.Panel;
        _androidCompositionRefresh = shell.AndroidCompositionRefresh;
        _scale = profile.Scale;
        _profile = profile;
        var primary = BuildPrimaryColumn(profile, shell.Content);
        _status = primary.Status;
        _secondaryStatus = primary.SecondaryStatus;
        PrimaryScroll = primary.PrimaryScroll;
        FirstRunGuide = primary.FirstRunGuide;
        HomeSections = primary.HomeSections;
        Login = primary.Login;
        Code = primary.Code;
        Download = primary.Download;
        Actions = primary.Actions;
        Actions.HomeDestinationPressed += SelectDestination;
        var diagnostics = BuildLogColumn(profile, Actions.HelpDiagnosticsHost);
        Log = diagnostics.Log;
        DiagnosticsDrawer = diagnostics.Drawer;
        DiagnosticsToggle = diagnostics.Toggle;
        Actions.HelpDiagnosticsHost.AddChild(BuildHelpAttributionFooter(_profile.Scale, _profile.Compact));
        var navigation = BuildDestinationNavigation(profile);
        shell.Content.AddChild(navigation.Root);
        if (!profile.Compact)
            shell.Content.MoveChild(navigation.Root, 1);
        if (profile.Compact && OperatingSystem.IsAndroid())
        {
            _destinationSafeAreaSpacer = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
            shell.Content.AddChild(_destinationSafeAreaSpacer);
        }

        _destinationButtons = navigation.Buttons;
        UpdateSystemInsets();
        WireDestinationNavigation();
        WireCompactStatusDetailToggle();
        SelectDestination(LauncherDestination.Home);
        ShowSetupGuide();
    }

    internal void WireEvents(LauncherViewHandlers handlers)
    {
        Login.LoginRequested += handlers.LoginRequested;
        Login.StatusRequested += SetStatus;
        Code.CodeSubmitted += handlers.CodeSubmitted;
        Download.DownloadRequested += handlers.DownloadRequested;
        Download.GameBranchChanged += handlers.GameBranchChanged;
        Download.RefreshGameVersionsRequested += handlers.RefreshGameVersionsPressed;
        Actions.GameBranchChanged += handlers.GameBranchChanged;
        Actions.RendererModeChanged += handlers.RendererModeChanged;
        Actions.LaunchPressed += handlers.LaunchPressed;
        Actions.SafeLaunchPressed += handlers.SafeLaunchPressed;
        Actions.RetryPressed += handlers.RetryPressed;
        Actions.CheckForUpdatesPressed += handlers.CheckForUpdatesPressed;
        Actions.UpdateSelectedVersionPressed += handlers.UpdateSelectedVersionPressed;
        Actions.RefreshGameVersionsPressed += handlers.RefreshGameVersionsPressed;
        Actions.RedownloadPressed += handlers.RedownloadPressed;
        Actions.DiagnosticsPressed += handlers.DiagnosticsPressed;
        Actions.ShowLastErrorPressed += handlers.ShowLastErrorPressed;
        Actions.CopyRawLogPressed += handlers.CopyRawLogPressed;
        Actions.ReportBugPressed += handlers.ReportBugPressed;
        Actions.CheckAppUpdatesPressed += handlers.CheckAppUpdatesPressed;
        Actions.SaveSyncNowPressed += handlers.SaveSyncNowPressed;
        Actions.SavePullPressed += handlers.SavePullPressed;
        Actions.SavePushPressed += handlers.SavePushPressed;
        Actions.SaveSyncPreferenceChanged += handlers.SaveSyncPreferenceChanged;
        Actions.CopyVanillaToModdedPressed += handlers.CopyVanillaToModdedPressed;
        Actions.WorkshopSyncPressed += handlers.WorkshopSyncPressed;
        Actions.WorkshopClearPressed += handlers.WorkshopClearPressed;
        Actions.ModsSelectionChanged += handlers.ModsSelectionChanged;
    }

    // Moves Home to the given stage. Every stage sets the visibility of every
    // Home section and of the play actions, so the result never depends on
    // which stage came before it.
    private void ShowHomeStage(HomeStage stage)
    {
        _homeStage = stage;
        SelectHomeDestination();
        FirstRunGuide.Visible = stage == HomeStage.SetupGuide;
        if (stage != HomeStage.SignIn)
            HideLoginForm();
        Code.Visible = stage == HomeStage.SteamGuard;
        Download.Visible = stage == HomeStage.Download;
        if (stage != HomeStage.Ready && stage != HomeStage.Retry)
            Actions.HidePlayActions();
        ScrollCompactPrimaryToTop();
    }

    internal void ShowSetupGuide() => ShowHomeStage(HomeStage.SetupGuide);

    internal void ShowLoginForm(bool disabled)
    {
        ShowHomeStage(HomeStage.SignIn);
        Login.SetFormVisible(true, disabled);
    }

    internal void ShowCodePrompt(bool wasIncorrect)
    {
        ShowHomeStage(HomeStage.SteamGuard);
        Code.Show(wasIncorrect);
    }

    internal void ShowDownloadAction(string buttonText)
    {
        ShowHomeStage(HomeStage.Download);
        Download.Reset(buttonText);
    }

    internal void ShowLaunchActions(bool showUpdate)
    {
        Actions.ShowLaunch(showUpdate);
        ShowHomeStage(HomeStage.Ready);
    }

    internal void ShowRetry()
    {
        Actions.ShowRetry();
        ShowHomeStage(HomeStage.Retry);
    }

    // Used while the Steam session connects: nothing on Home is actionable,
    // and the setup guide keeps whatever visibility it had.
    internal void HideAllSections()
    {
        _homeStage = null;
        HideLoginForm();
        Code.Visible = false;
        Download.Visible = false;
        Actions.HidePlayActions();
    }

    private void HideLoginForm()
    {
        if (Login.Visible)
            Login.SetFormVisible(false, disabled: true);
    }

    internal void ShowHomeHelpAction() => Actions.ShowHomeHelpAction();

    internal void SetActionPreferences(LauncherPreferences.ActionPreferences preferences, IReadOnlyList<LauncherBranchCatalog.BranchOption> branches)
    {
        Actions.SetRendererMode(preferences.RendererMode);
        SetVersions(_versions.WithSelection(preferences.GameBranch, branches));
    }

    // The single place the version controls are drawn; each setter below
    // changes one part of the state and redraws both sections from all of it.
    private void SetVersions(LauncherVersionsState versions)
    {
        _versions = versions;
        Download.SetVersions(versions);
        Actions.SetVersions(versions);
    }

    internal void SetGameBranch(string branch) => SetVersions(_versions.WithSelection(branch, _versions.Branches));
    internal void SetGameBranchOptions(IReadOnlyList<LauncherBranchCatalog.BranchOption> branches)
        => SetVersions(_versions.WithSelection(_versions.Branch, branches));
    internal void SetVersionCheckOutcome(LauncherVersionCheckOutcome outcome) => SetVersions(_versions with { CheckOutcome = outcome });

    // Starting a check discards the previous result; finishing keeps the new one.
    internal void SetUpdateCheckBusy(bool busy)
        => SetVersions(_versions with
        {
            CheckingForUpdates = busy,
            CheckOutcome = busy ? LauncherVersionCheckOutcome.None : _versions.CheckOutcome,
        });

    internal void SetRefreshGameVersionsBusy(bool busy) => SetVersions(_versions with { RefreshingList = busy });
    internal void SetSelectedVersionOperationRunning(bool running)
        => SetVersions(_versions with { SelectedVersionOperationRunning = running });

    internal void SetWorkshopButtonsDisabled(bool disabled) => Actions.SetWorkshopButtonsDisabled(disabled);
    internal void SetLaunchControlsDisabled(bool disabled) => Actions.SetLaunchControlsDisabled(disabled);
    internal void SetSaveSyncPresentation(LauncherSaveSyncPresentation presentation, bool busy)
        => Actions.SetSaveSyncPresentation(presentation, busy);
    internal void SetModsPresentationChecking()
    {
        Actions.SetModsPresentationChecking();
        RefreshPrimaryScrollRange();
    }

    internal void SetModsPresentation(LauncherModsPresentation presentation)
    {
        Actions.SetModsPresentation(presentation);
        RefreshPrimaryScrollRange();
    }

    internal void SetPowerVrCompatibility(bool required) => Actions.SetPowerVrCompatibility(required);
    internal void SetRendererMode(string mode) => Actions.SetRendererMode(mode);
    internal void ClearLoginPasswordAndDisable()
    {
        Login.SetDisabled(true);
        Login.ClearPassword();
    }

    internal void ShowDownloadProgress(string text)
    {
        if (_homeStage != HomeStage.Download)
            ShowHomeStage(HomeStage.Download);
        Download.ShowProgress(text);
    }

    internal void SetDownloadProgress(double percentage, string text) => Download.SetProgress(percentage, text);
    internal void ResetDownload() => Download.Reset();
    internal void ResetDownload(string buttonText) => Download.Reset(buttonText);
    internal void SetDownloadButtonDisabled(bool disabled) => Download.SetButtonDisabled(disabled);
    private void ScrollCompactPrimaryToTop()
    {
        if (_profile.Compact)
            PrimaryScroll.ScrollVertical = 0;
    }

    // ScrollContainer caches content extents in its minimum-size calculation,
    // so content that changes size needs both calls to refresh the scrollbar.
    private void RefreshPrimaryScrollRange()
    {
        PrimaryScroll.UpdateMinimumSize();
        PrimaryScroll.QueueSort();
    }
}
