﻿/*
 * Switcheroo - The incremental-search task switcher for Windows.
 * http://www.switcheroo.io/
 * Copyright 2009, 2010 James Sulak
 * Copyright 2014 Regin Larsen
 * 
 * Switcheroo is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * Switcheroo is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with Switcheroo.  If not, see <http://www.gnu.org/licenses/>.
 */

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ManagedWinapi;
using ManagedWinapi.Windows;
using Switcheroo.Core;
using Switcheroo.Core.Matchers;
using Switcheroo.Properties;
using Application = System.Windows.Application;
using MenuItem = System.Windows.Forms.MenuItem;
using MessageBox = System.Windows.MessageBox;

namespace Switcheroo
{
    public partial class MainWindow : Window
    {
        private WindowCloser _windowCloser;
        private List<AppWindowViewModel> _unfilteredWindowList;
        private ObservableCollection<AppWindowViewModel> _filteredWindowList;
        private NotifyIcon _notifyIcon;
        private HotKey _hotkey;

        public static readonly RoutedUICommand CloseWindowCommand = new RoutedUICommand();
        public static readonly RoutedUICommand SwitchToWindowCommand = new RoutedUICommand();
        public static readonly RoutedUICommand ScrollListDownCommand = new RoutedUICommand();
        public static readonly RoutedUICommand ScrollListUpCommand = new RoutedUICommand();
        public static readonly RoutedUICommand ScrollListPageDownCommand = new RoutedUICommand();
        public static readonly RoutedUICommand ScrollListPageUpCommand = new RoutedUICommand();
        public static readonly RoutedUICommand ScrollListHomeCommand = new RoutedUICommand();
        public static readonly RoutedUICommand ScrollListEndCommand = new RoutedUICommand();
        private OptionsWindow _optionsWindow;
        private AboutWindow _aboutWindow;
        private AltTabHook _altTabHook;
        private SystemWindow _foregroundWindow;
        private bool _altTabAutoSwitch;
        private bool _sortWinList = false;

        public MainWindow()
        {
            InitializeComponent();

            SetUpKeyBindings();

            SetUpNotifyIcon();

            SetUpHotKey();

            SetUpAltTabHook();

            CheckForUpdates();

            Theme.SuscribeWindow(this);

            //Theme.LoadTheme(); 

            Opacity = 0;
        }

        /// =================================

        #region Private Methods

        /// =================================

        private void SetUpKeyBindings()
        {
            // Enter and Esc bindings are not executed before the keys have been released.
            // This is done to prevent that the window being focused after the key presses
            // to get 'KeyUp' messages.

            KeyDown += (sender, args) =>
            {
                // Opacity is set to 0 right away so it appears that action has been taken right away...
                if (args.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                {
                    Opacity = 0;
                }
                else if (args.Key == Key.Escape)
                {
                    Opacity = 0;
                }
                else if (args.SystemKey == Key.O)
                {
                    Options();
                }
                else if (args.SystemKey == Key.W)
                {
                    ExportToJSON();
                }
                else if (args.SystemKey == Key.Q && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
                {
                    _altTabAutoSwitch = false;
                    tb.Text = "";
                    tb.IsEnabled = true;
                    tb.Focus();
                }
                else if (args.SystemKey == Key.S && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
                {
                    Toggle_sortWinList();
                    LoadData(InitialFocus.NextItem);
                }
                else if ((args.SystemKey == Key.Up || args.SystemKey == Key.K) && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
                {
                    PreviousItem();
                }
                else if ((args.SystemKey == Key.Down || args.SystemKey == Key.J) && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
                {
                    NextItem();
                }
                else if (args.SystemKey == Key.D1 && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
                {
                    SwitchToIndex(0);
                }
                else if (args.SystemKey == Key.D2 && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
                {
                    SwitchToIndex(1);
                }
                else if (args.SystemKey == Key.D3 && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
                {
                    SwitchToIndex(2);
                }
                else if (args.SystemKey == Key.D4 && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
                {
                    SwitchToIndex(3);
                }
                else if (args.SystemKey == Key.D5 && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
                {
                    SwitchToIndex(4);
                }
                else if (args.SystemKey == Key.D6 && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
                {
                    SwitchToIndex(5);
                }
                else if (args.SystemKey == Key.D7 && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
                {
                    SwitchToIndex(6);
                }
                else if (args.SystemKey == Key.D8 && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
                {
                    SwitchToIndex(7);
                }
                else if (args.SystemKey == Key.D9 && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
                {
                    SwitchToIndex(8);
                }
                else if (args.SystemKey == Key.D0 && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
                {
                    SwitchToIndex(9);
                }
            };

            KeyUp += (sender, args) =>
            {
                // ... But only when the keys are release, the action is actually executed
                if (args.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                {
                    Switch();
                }
                else if (args.Key == Key.Escape)
                {
                    HideWindow();
                }
                else if (args.SystemKey == Key.LeftAlt && !Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && _altTabAutoSwitch )
                {
                    Switch();
                }
                else if (args.Key == Key.LeftAlt && _altTabAutoSwitch)
                {
                    Switch();
                }
            };
        }

        private void SwitchToIndex(int i)
        {
            if (i < lb.Items.Count)
            {
                lb.SelectedIndex = i;
                ScrollSelectedItemIntoView();
                Switch();
                HideWindow();
            }
        }

        private void SetUpHotKey()
        {
            _hotkey = new HotKey();
            _hotkey.LoadSettings();

            Application.Current.Properties["hotkey"] = _hotkey;

            _hotkey.HotkeyPressed += hotkey_HotkeyPressed;
            try
            {
                _hotkey.Enabled = Settings.Default.EnableHotKey;
            }
            catch (HotkeyAlreadyInUseException)
            {
                var boxText = "The current hotkey for activating Switcheroo is in use by another program." +
                              Environment.NewLine +
                              Environment.NewLine +
                              "You can change the hotkey by right-clicking the Switcheroo icon in the system tray and choosing 'Options'.";
                MessageBox.Show(boxText, "Hotkey already in use", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SetUpAltTabHook()
        {
            _altTabHook = new AltTabHook();
            _altTabHook.Pressed += AltTabPressed;
        }

        private void SetUpNotifyIcon()
        {
            var icon = Properties.Resources.icon;
            
            var runOnStartupMenuItem = new MenuItem("Run on &Startup", (s, e) => RunOnStartup(s as MenuItem))
            {
                Checked = new AutoStart().IsEnabled
            };

            var sortAZMenuItem = new MenuItem("Alpha&betical Sort", (s, e) => sortAZMenuItem_Click(s as MenuItem));

            var exportToJSON_MenuItem = new MenuItem("Export to &Json", (s, e) => exportToJSON_MenuItem_Click(s as MenuItem));

            _notifyIcon = new NotifyIcon
            {
                Text = "Switcheroo",
                Icon = icon,
                Visible = true,
                ContextMenu = new System.Windows.Forms.ContextMenu(new[]
                {
                    new MenuItem("&Options", (s, e) => Options()),
                    runOnStartupMenuItem,
                    sortAZMenuItem,
                    exportToJSON_MenuItem,
                    new MenuItem("&About", (s, e) => About()),
                    new MenuItem("E&xit", (s, e) => Quit())
                })
            };

            _notifyIcon.MouseClick += new System.Windows.Forms.MouseEventHandler(NotifyIconMouseClick);
        }

        void NotifyIconMouseClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (Visibility != Visibility.Visible)
                {
                   _foregroundWindow = SystemWindow.ForegroundWindow;
                    Show();
                    Activate();
                    LoadData(InitialFocus.NextItem);
                    tb.IsEnabled = true;
                    tb.Text = "";
                    Keyboard.Focus(tb);
                    Opacity = 1;
                }
            }
        }

        private static void RunOnStartup(MenuItem menuItem)
        {
            try
            {
                var autoStart = new AutoStart
                {
                    IsEnabled = !menuItem.Checked
                };
                menuItem.Checked = autoStart.IsEnabled;
            }
            catch (AutoStartException e)
            {
                MessageBox.Show(e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private static void CheckForUpdates()
        {
            var currentVersion = Assembly.GetEntryAssembly().GetName().Version;
            if (currentVersion == new Version(0, 0, 0, 0))
            {
                return;
            }

            var timer = new DispatcherTimer();

            timer.Tick += async (sender, args) =>
            {
                timer.Stop();
                var latestVersion = await GetLatestVersion();
                if (latestVersion != null && latestVersion > currentVersion)
                {
                    var result = MessageBox.Show(
                        string.Format(
                            "Switcheroo v{0} is available (you have v{1}).\r\n\r\nDo you want to download it?",
                            latestVersion, currentVersion),
                        "Update Available", MessageBoxButton.YesNo, MessageBoxImage.Information);
                    if (result == MessageBoxResult.Yes)
                    {
                        Process.Start("https://github.com/kvakulo/Switcheroo/releases/latest");
                    }
                }
                else
                {
                    timer.Interval = new TimeSpan(24, 0, 0);
                    timer.Start();
                }
            };

            timer.Interval = new TimeSpan(0, 0, 0);
            timer.Start();
        }

        private static async Task<Version> GetLatestVersion()
        {
            try
            {
                var versionAsString =
                    await
                        new WebClient().DownloadStringTaskAsync(
                            "https://raw.github.com/kvakulo/Switcheroo/update/version.txt");
                Version newVersion;
                if (Version.TryParse(versionAsString, out newVersion))
                {
                    return newVersion;
                }
            }
            catch (WebException)
            {
            }
            return null;
        }

        /// <summary>
        /// Export JSON of currently running windows.
        /// JSON Structure: arrays of window titles are grouped under unique process names which are sorted alphabetically.
        /// </summary>
        private void ExportToJSON()
        {
            _unfilteredWindowList = new WindowFinder().GetWindows().Select(window => new AppWindowViewModel(window)).ToList();
            _unfilteredWindowList = _unfilteredWindowList.OrderBy(x => x.ProcessTitle).ToList();

            var winsDict = _unfilteredWindowList.GroupBy(x => x.ProcessTitle).ToDictionary(x => x.Key, x => x.Select(y => y.WindowTitle));

			string JSON_output = JsonConvert.SerializeObject(winsDict);

            SaveFileDialog SaveFileDialog1 = new SaveFileDialog();
            SaveFileDialog1.Title = "Save list to";
            SaveFileDialog1.DefaultExt = "txt";
            SaveFileDialog1.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
            SaveFileDialog1.ShowDialog();

            if (SaveFileDialog1.FileName != "")
            {
                using (System.IO.StreamWriter file = new System.IO.StreamWriter(@SaveFileDialog1.FileName, false))
                {
                    file.Write(JSON_output);
                }
            }
        }

        /// <summary>
        /// Populates the window list with the current running windows.
        /// </summary>
        private void LoadData(InitialFocus focus)
        {

			_unfilteredWindowList = new WindowFinder().GetWindows()
                .Select(window => new AppWindowViewModel(window))
                .ToList();

            var firstWindow = _unfilteredWindowList.FirstOrDefault();

            var foregroundWindowMovedToBottom = false;
            
            // Move first window to the bottom of the list if it's related to the foreground window
            if (firstWindow != null && AreWindowsRelated(firstWindow.AppWindow, _foregroundWindow))
            {
                _unfilteredWindowList.RemoveAt(0);
                _unfilteredWindowList.Add(firstWindow);
                foregroundWindowMovedToBottom = true;
            }

            _filteredWindowList = new ObservableCollection<AppWindowViewModel>(_unfilteredWindowList);
            _windowCloser = new WindowCloser();

            for (var i = 0; i < _unfilteredWindowList.Count; i++)
            {
                _unfilteredWindowList[i].FormattedTitle = new XamlHighlighter().Highlight(new[] { new StringPart(_unfilteredWindowList[i].AppWindow.Title) });
                _unfilteredWindowList[i].FormattedProcessTitle =
                    new XamlHighlighter().Highlight(new[] { new StringPart(_unfilteredWindowList[i].AppWindow.ProcessTitle) });
            }

            if (_sortWinList == true)
            {
                _unfilteredWindowList = _unfilteredWindowList.OrderBy(x => x.FormattedProcessTitle).ToList();
            }
            
            var _itemsCountToHighlight = Math.Min(_unfilteredWindowList.Count, 10);
            for (var i = 0; i < _itemsCountToHighlight; i++)
            {
                _unfilteredWindowList[i].FormattedTitle = new XamlHighlighter().Highlight(new[] { new StringPart("" + (i + 1) + " ", true) }) + _unfilteredWindowList[i].FormattedTitle ;
            }

            if (_sortWinList == true)
            {
                lb.DataContext = null;
                lb.DataContext = _unfilteredWindowList;
            }
            else
            {
                lb.DataContext = _filteredWindowList;
            }

            FocusItemInList(focus, foregroundWindowMovedToBottom);

            if ( tb.IsEnabled ) tb.Clear();
            tb.Focus();
            CenterWindow();
            ScrollSelectedItemIntoView();
        }

        private static bool AreWindowsRelated(SystemWindow window1, SystemWindow window2)
        {
            return window1.HWnd == window2.HWnd || window1.Process.Id == window2.Process.Id;
        }

        private void FocusItemInList(InitialFocus focus, bool foregroundWindowMovedToBottom)
        {
            if (focus == InitialFocus.PreviousItem)
            {
                var previousItemIndex = lb.Items.Count - 1;
                if (foregroundWindowMovedToBottom)
                {
                    previousItemIndex--;
                }

                lb.SelectedIndex = previousItemIndex > 0 ? previousItemIndex : 0;
            }
            else
            {
                lb.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// Place the Switcheroo window in the center of the screen
        /// </summary>
        private void CenterWindow()
        {
            // Reset height every time to ensure that resolution changes take effect
            Border.MaxHeight = SystemParameters.PrimaryScreenHeight;

            // Force a rendering before repositioning the window
            SizeToContent = SizeToContent.Manual;
            SizeToContent = SizeToContent.WidthAndHeight;

            // Position the window in the center of the screen
            Left = (SystemParameters.PrimaryScreenWidth/2) - (ActualWidth/2);
            Top = (SystemParameters.PrimaryScreenHeight/2) - (ActualHeight/2);
        }

        /// <summary>
        /// Switches the window associated with the selected item.
        /// </summary>
        private void Switch()
        {
            foreach (var item in lb.SelectedItems)
            {
                var win = (AppWindowViewModel)item;
                win.AppWindow.SwitchToLastVisibleActivePopup();
            }

            HideWindow();
        }

        private void HideWindow()
        {
            if (_windowCloser != null)
            {
                _windowCloser.Dispose();
                _windowCloser = null;
            }

            _altTabAutoSwitch = false;
            Opacity = 0;
            Dispatcher.BeginInvoke(new Action(Hide), DispatcherPriority.Input);
        }

        #endregion

        /// =================================

        #region Right-click menu functions

        /// =================================
        /// <summary>
        /// Show Options dialog.
        /// </summary>
        private void Options()
        {
            if (_optionsWindow == null)
            {
                _optionsWindow = new OptionsWindow
                {
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };
                _optionsWindow.Closed += (sender, args) => _optionsWindow = null;
                _optionsWindow.ShowDialog();
            }
            else
            {
                _optionsWindow.Activate();
            }
        }

        /// <summary>
        /// Show About dialog.
        /// </summary>
        private void About()
        {
            if (_aboutWindow == null)
            {
                _aboutWindow = new AboutWindow
                {
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };
                _aboutWindow.Closed += (sender, args) => _aboutWindow = null;
                _aboutWindow.ShowDialog();
            }
            else
            {
                _aboutWindow.Activate();
            }
        }

        /// <summary>
        /// Quit Switcheroo
        /// </summary>
        private void Quit()
        {
            _notifyIcon.Dispose();
            _notifyIcon = null;
            _hotkey.Dispose();
            Application.Current.Shutdown();
        }

        /// <summary>
        /// Toggle alphabetical order program sort
        /// </summary>
        private void sortAZMenuItem_Click(MenuItem menuItem)
        {
            Toggle_sortWinList();
            menuItem.Checked = _sortWinList;
        }

        /// <summary>
        /// Context menu "Export to Json"
        /// </summary>
        private void exportToJSON_MenuItem_Click(MenuItem menuItem)
        {
            ExportToJSON();
        }

        #endregion

        /// =================================

        #region Event Handlers

        /// =================================
        private void OnClose(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            HideWindow();
        }

        private void hotkey_HotkeyPressed(object sender, EventArgs e)
        {
            if (!Settings.Default.EnableHotKey)
            {
                return;
            }

            if (Visibility != Visibility.Visible)
            {
                tb.IsEnabled = true;

                _foregroundWindow = SystemWindow.ForegroundWindow;
                Show();
                Activate();
                Keyboard.Focus(tb);
                LoadData(InitialFocus.NextItem);
                Opacity = 1;
            }
            else
            {
                HideWindow();
            }
        }

        private void AltTabPressed(object sender, AltTabHookEventArgs e)
        {
            if (!Settings.Default.AltTabHook)
            {
                // Ignore Alt+Tab presses if the hook is not activated by the user
                return;
            }

            _foregroundWindow = SystemWindow.ForegroundWindow;

            if (_foregroundWindow.ClassName == "MultitaskingViewFrame")
            {
                // If Windows' task switcher is on the screen then don't do anything
                return;
            }

            e.Handled = true;

            if (Visibility != Visibility.Visible)
            {
                tb.IsEnabled = true;

                ActivateAndFocusMainWindow();

                Keyboard.Focus(tb);
                if (e.ShiftDown)
                {
                    LoadData(InitialFocus.PreviousItem);
                }
                else
                {
                    LoadData(InitialFocus.NextItem);
                }

                if (Settings.Default.AutoSwitch && !e.CtrlDown)
                {
                    _altTabAutoSwitch = true;
                    tb.IsEnabled = false;
                    tb.Text = "Press Alt + Q to search";
                }

                Opacity = 1;
            }
            else
            {
                if (e.ShiftDown)
                {
                    PreviousItem();
                }
                else
                {
                    NextItem();
                }
            }
        }

        private void ActivateAndFocusMainWindow()
        {
            // What happens below looks a bit weird, but for Switcheroo to get focus when using the Alt+Tab hook,
            // it is needed to simulate an Alt keypress will bring Switcheroo to the foreground. Otherwise Switcheroo
            // will become the foreground window, but the previous window will retain focus, and receive keep getting
            // the keyboard input.
            // http://www.codeproject.com/Tips/76427/How-to-bring-window-to-top-with-SetForegroundWindo

            var thisWindowHandle = new WindowInteropHelper(this).Handle;
            var thisWindow = new AppWindow(thisWindowHandle);

            var altKey = new KeyboardKey(Keys.Alt);
            var altKeyPressed = false;

            // Press the Alt key if it is not already being pressed
            if ((altKey.AsyncState & 0x8000) == 0)
            {
                altKey.Press();
                altKeyPressed = true;
            }

            // Bring the Switcheroo window to the foreground
            Show();
            SystemWindow.ForegroundWindow = thisWindow;
            Activate();

            // Release the Alt key if it was pressed above
            if (altKeyPressed)
            {
                altKey.Release();
            }
        }

        private void TextChanged(object sender, TextChangedEventArgs args)
        {
            if (!tb.IsEnabled)
            {
                return;
            }

            var query = tb.Text;

            var context = new WindowFilterContext<AppWindowViewModel>
            {
                Windows = _unfilteredWindowList,
                ForegroundWindowProcessTitle = new AppWindow(_foregroundWindow.HWnd).ProcessTitle
            };

            var filterResults = new WindowFilterer().Filter(context, query).ToList();

            foreach (var filterResult in filterResults)
            {
                filterResult.AppWindow.FormattedTitle =
                    GetFormattedTitleFromBestResult(filterResult.WindowTitleMatchResults);
                filterResult.AppWindow.FormattedProcessTitle =
                    GetFormattedTitleFromBestResult(filterResult.ProcessTitleMatchResults);
            }

            _filteredWindowList = new ObservableCollection<AppWindowViewModel>(filterResults.Select(r => r.AppWindow));
            lb.DataContext = _filteredWindowList;
            if (lb.Items.Count > 0)
            {
                lb.SelectedItem = lb.Items[0];
            }
        }

        private static string GetFormattedTitleFromBestResult(IList<MatchResult> matchResults)
        {
            var bestResult = matchResults.FirstOrDefault(r => r.Matched) ?? matchResults.First();
            return new XamlHighlighter().Highlight(bestResult.StringParts);
        }

        private void OnEnterPressed(object sender, ExecutedRoutedEventArgs e)
        {
            Switch();
            e.Handled = true;
        }

        private void ListBoxItem_MouseLBClick(object sender, MouseButtonEventArgs e)
        {
            if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                Switch();
            }
            e.Handled = true;
        }
        
        private void MenuItem_Click_toFront(object sender, RoutedEventArgs e)
        {
            Switch();
        }

        private async void MenuItem_Click_toClose(object sender, RoutedEventArgs e)
        {
            var windows = lb.SelectedItems.Cast<AppWindowViewModel>().ToList();
            foreach (var win in windows)
            {
                bool isClosed = await _windowCloser.TryCloseAsync(win);
                if (isClosed)
                    RemoveWindow(win);
            }

            if (lb.Items.Count == 0)
                HideWindow();
        }

        private void MenuItem_Duplicate(object sender, RoutedEventArgs e)
        {
            foreach (var item in lb.SelectedItems)
            {
                var torun = _unfilteredWindowList[lb.SelectedIndex].AppWindow.ExecutablePath.ToString();
                System.Diagnostics.Process.Start(torun);
            }

            HideWindow();
        }

        private async void CloseWindow(object sender, ExecutedRoutedEventArgs e)
        {
            var windows = lb.SelectedItems.Cast<AppWindowViewModel>().ToList();
            foreach (var win in windows)
            {
                bool isClosed = await _windowCloser.TryCloseAsync(win);
                if(isClosed)
                    RemoveWindow(win);
            }

            if (lb.Items.Count == 0)
                HideWindow();

            e.Handled = true;
        }

        private void RemoveWindow(AppWindowViewModel window)
        {
            int index = _filteredWindowList.IndexOf(window);
            if (index < 0)
                return;

            if (lb.SelectedIndex == index)
            {
                if (_filteredWindowList.Count > index + 1)
                    lb.SelectedIndex++;
                else
                {
                    if (index > 0)
                        lb.SelectedIndex--;
                }
            }

            _filteredWindowList.Remove(window);
            _unfilteredWindowList.Remove(window);
        }

        private void ScrollListUp(object sender, ExecutedRoutedEventArgs e)
        {
            PreviousItem();
            e.Handled = true;
        }

        private void PreviousItem()
        {
            if (lb.Items.Count > 0)
            {
                if (lb.SelectedIndex != 0)
                {
                    lb.SelectedIndex--;
                }
                else
                {
                    lb.SelectedIndex = lb.Items.Count - 1;
                }

                ScrollSelectedItemIntoView();
            }
        }

        private void ScrollListDown(object sender, ExecutedRoutedEventArgs e)
        {
            NextItem();
            e.Handled = true;
        }

        private void NextItem()
        {
            if (lb.Items.Count > 0)
            {
                if (lb.SelectedIndex != lb.Items.Count - 1)
                {
                    lb.SelectedIndex++;
                }
                else
                {
                    lb.SelectedIndex = 0;
                }

                ScrollSelectedItemIntoView();
            }
        }
        
        private void ScrollListPageUp(object sender, ExecutedRoutedEventArgs e)
        {
            double n = NumOfVisibleRows();

            if (lb.SelectedIndex - n >= 0)
                lb.SelectedIndex = Convert.ToInt32(lb.SelectedIndex - n);
            else
                lb.SelectedIndex = 0;
            ScrollSelectedItemIntoView();

            e.Handled = true;
        }

        private void ScrollListPageDown(object sender, ExecutedRoutedEventArgs e)
        {
            double n = NumOfVisibleRows();

            if (n + lb.SelectedIndex <= lb.Items.Count - 1)
                lb.SelectedIndex = Convert.ToInt32(n);
            else
                lb.SelectedIndex = lb.Items.Count - 1;
            ScrollSelectedItemIntoView();

            e.Handled = true;
        }

        private double NumOfVisibleRows()
        {
            return Math.Round(lb.ActualHeight / SearchGrid.ActualHeight); 
        }

        private void ScrollListHome(object sender, ExecutedRoutedEventArgs e)
        {
            lb.SelectedIndex = 0;
            ScrollSelectedItemIntoView();

            e.Handled = true;
        }

        private void ScrollListEnd(object sender, ExecutedRoutedEventArgs e)
        {
            lb.SelectedIndex = lb.Items.Count-1;
            ScrollSelectedItemIntoView();

            e.Handled = true;
        }
        
        private void ScrollSelectedItemIntoView()
        {
            var selectedItem = lb.SelectedItem;
            if (selectedItem != null)
            {
                lb.ScrollIntoView(selectedItem);
            }
        }

        private void MainWindow_OnLostFocus(object sender, EventArgs e)
        {
            HideWindow();
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            DisableSystemMenu();
        }

        private void DisableSystemMenu()
        {
            var windowHandle = new WindowInteropHelper(this).Handle;
            var window = new SystemWindow(windowHandle);
            window.Style = window.Style & ~WindowStyleFlags.SYSMENU;
        }

        private void ShowHelpTextBlock_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var duration = new Duration(TimeSpan.FromSeconds(0.150));
            var newHeight = HelpPanel.Height > 0 ? 0 : +17;
            HelpPanel.BeginAnimation(HeightProperty, new DoubleAnimation(HelpPanel.Height, newHeight, duration));
        }

        #endregion

        private enum InitialFocus
        {
            NextItem,
            PreviousItem
        }
        
        void Toggle_sortWinList()
        {
            _sortWinList = !_sortWinList;
            Toggle_MenuItem("Alpha&betical Sort");
        }

        void Toggle_MenuItem(String text)
        {
            foreach (MenuItem mi in _notifyIcon.ContextMenu.MenuItems) {
                if((String)mi.Text == text) 
                {
                    mi.Checked = !mi.Checked;
                }
            }
        }
        
    }
}
