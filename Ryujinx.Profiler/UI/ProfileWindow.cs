﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using OpenTK.Input;
using Ryujinx.Profiler.UI.SharpFontHelpers;

namespace Ryujinx.Profiler.UI
{
    public partial class ProfileWindow : GameWindow
    {
        // List all buttons for index in button array
        private enum ButtonIndex
        {
            TagTitle          = 0,
            InstantTitle      = 1,
            AverageTitle      = 2,
            TotalTitle        = 3,
            FilterBar         = 4,
            ShowHideInactive  = 5,
            Pause             = 6,

            // Update this when new buttons are added
            Count             = 7,
        }

        // Font service
        private FontService _fontService;

        // UI variables
        private ProfileButton[] _buttons;

        private bool _initComplete    = false;

        private bool _visible         = true;
        private bool _visibleChanged  = true;
        private bool _viewportUpdated = true;
        private bool _redrawPending   = true;

        private bool _showInactive    = true;
        private bool _paused          = false;

        // Layout
        private const int LineHeight      = 16;
        private const int TitleHeight     = 24;
        private const int TitleFontHeight = 16;
        private const int LinePadding     = 2;
        private const int ColumnSpacing   = 30;
        private const int FilterHeight    = 24;

        // Sorting
        private List<KeyValuePair<ProfileConfig, TimingInfo>> _unsortedProfileData;
        private IComparer<KeyValuePair<ProfileConfig, TimingInfo>> _sortAction = new ProfileSorters.TagAscending();

        // Filtering
        private string _filterText = "";
        private bool _regexEnabled = false;

        // Scrolling
        private float _scrollPos = 0;
        private float _minScroll = 0;
        private float _maxScroll = 0;

        // Profile data storage
        private List<KeyValuePair<ProfileConfig, TimingInfo>> _sortedProfileData;

        // Input
        private bool _backspaceDown       = false;
        private bool _prevBackspaceDown   = false;
        private double _backspaceDownTime = 0;

        // Event management
        private double _updateTimer;
        private double _processEventTimer;
        private bool  _profileUpdated = false;
        

        public ProfileWindow()
            : base(1280, 720)
        {
            Title    = "Profiler";
            Location = new Point(DisplayDevice.Default.Width - 1280,
                              (DisplayDevice.Default.Height - 720) - 50);

            // Large number to force an update on first update
            _updateTimer = 0xFFFF;

            Init();

            // Release context for render thread
            Context.MakeCurrent(null);
        }
        
        public void ToggleVisible()
        {
            _visible = !_visible;
            _visibleChanged = true;
        }

        private void SetSort(IComparer<KeyValuePair<ProfileConfig, TimingInfo>> filter)
        {
            _sortAction = filter;
            _profileUpdated = true;
        }

        #region OnLoad
        /// <summary>
        /// Setup OpenGL and load resources
        /// </summary>
        public void Init()
        {
            GL.ClearColor(Color.Black);
            _fontService = new FontService();
            _fontService.InitalizeTextures();
            _fontService.UpdateScreenHeight(Height);

            _buttons = new ProfileButton[(int)ButtonIndex.Count];
            _buttons[(int)ButtonIndex.TagTitle]     = new ProfileButton(_fontService, () => SetSort(new ProfileSorters.TagAscending()));
            _buttons[(int)ButtonIndex.InstantTitle] = new ProfileButton(_fontService, () => SetSort(new ProfileSorters.InstantAscending()));
            _buttons[(int)ButtonIndex.AverageTitle] = new ProfileButton(_fontService, () => SetSort(new ProfileSorters.AverageAscending()));
            _buttons[(int)ButtonIndex.TotalTitle]   = new ProfileButton(_fontService, () => SetSort(new ProfileSorters.TotalAscending()));
            _buttons[(int)ButtonIndex.FilterBar]    = new ProfileButton(_fontService, () =>
            {
                _profileUpdated = true;
                _regexEnabled = !_regexEnabled;
            });

            _buttons[(int)ButtonIndex.ShowHideInactive] = new ProfileButton(_fontService, () =>
            {
                _profileUpdated = true;
                _showInactive = !_showInactive;
            });

            _buttons[(int)ButtonIndex.Pause] = new ProfileButton(_fontService, () =>
            {
                _profileUpdated = true;
                _paused = !_paused;
            });

            Visible = _visible;
        }
        #endregion

        #region OnResize
        /// <summary>
        /// Respond to resize events
        /// </summary>
        /// <param name="e">Contains information on the new GameWindow size.</param>
        /// <remarks>There is no need to call the base implementation.</remarks>
        protected override void OnResize(EventArgs e)
        {
            _viewportUpdated = true;
        }
        #endregion

        #region OnClose
        /// <summary>
        /// Intercept close event and hide instead
        /// </summary>
        protected override void OnClosing(CancelEventArgs e)
        {
            // Hide window
            _visible        = false;
            _visibleChanged = true;

            // Cancel close
            e.Cancel = true;

            base.OnClosing(e);
        }
        #endregion

        #region OnUpdateFrame
        /// <summary>
        /// Profile Update Loop
        /// </summary>
        /// <param name="e">Contains timing information.</param>
        /// <remarks>There is no need to call the base implementation.</remarks>
        public void Update(FrameEventArgs e)
        {
            // Backspace handling
            if (_backspaceDown)
            {
                if (!_prevBackspaceDown)
                {
                    _backspaceDownTime = 0;
                    FilterBackspace();
                }
                else
                {
                    _backspaceDownTime += e.Time;
                    if (_backspaceDownTime > 0.3)
                    {
                        _backspaceDownTime -= 0.05;
                        FilterBackspace();
                    }
                }
            }
            _prevBackspaceDown = _backspaceDown;

            // Get timing data if enough time has passed
            _updateTimer += e.Time;
            if (!_paused && (_updateTimer > Profile.GetUpdateRate()))
            {
                _updateTimer         = 0;
                _unsortedProfileData = Profile.GetProfilingData().ToList();
                _profileUpdated      = true;
            }
            
            // Filtering
            if (_profileUpdated)
            {
                if (_showInactive)
                {
                    _sortedProfileData = _unsortedProfileData;
                }
                else
                {
                    _sortedProfileData = _unsortedProfileData.FindAll(kvp => kvp.Value.Instant > 0.001f);
                }

                if (_sortAction != null)
                {
                    _sortedProfileData.Sort(_sortAction);
                }

                if (_regexEnabled)
                {
                    try
                    {
                        Regex filterRegex = new Regex(_filterText, RegexOptions.IgnoreCase);
                        if (_filterText != "")
                        {
                            _sortedProfileData = _sortedProfileData.Where((pair => filterRegex.IsMatch(pair.Key.Search))).ToList();
                        }
                    }
                    catch (ArgumentException argException)
                    {
                        // Skip filtering for invalid regex
                    }
                }
                else
                {
                    // Regular filtering
                    _sortedProfileData = _sortedProfileData.Where((pair => pair.Key.Search.ToLower().Contains(_filterText.ToLower()))).ToList();
                }

                _profileUpdated = false;
                _redrawPending  = true;
                _initComplete   = true;
            }

            // Check for events 20 times a second
            _processEventTimer += e.Time;
            if (_processEventTimer > 0.05)
            {
                ProcessEvents();
                _processEventTimer = 0;
            }
        }
        #endregion

        #region OnRenderFrame
        /// <summary>
        /// Profile Render Loop
        /// </summary>
        /// <remarks>There is no need to call the base implementation.</remarks>
        public void Draw()
        {
            if (_visibleChanged)
            {
                Visible = _visible;
                _visibleChanged = false;
            }

            if (!_visible || !_initComplete)
            {
                return;
            }
            
            // Update viewport
            if (_viewportUpdated)
            {
                GL.Viewport(0, 0, Width, Height);

                GL.MatrixMode(MatrixMode.Projection);
                GL.LoadIdentity();
                GL.Ortho(0, Width, 0, Height, 0.0, 4.0);

                _fontService.UpdateScreenHeight(Height);

                _viewportUpdated = false;
                _redrawPending   = true;
            }

            if (!_redrawPending)
            {
                return;
            }

            // Frame setup
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.ClearColor(Color.Black);

            _fontService.fontColor = Color.White;
            int verticalIndex   = 0;

            float width;
            float maxWidth = 0;
            float yOffset  = _scrollPos - TitleHeight;
            float xOffset  = 10;

            // Background lines to make reading easier
            #region Background Lines
            GL.Enable(EnableCap.ScissorTest);
            GL.Scissor(0, FilterHeight, Width, Height - TitleHeight - FilterHeight);
            GL.Begin(PrimitiveType.Triangles);
            GL.Color3(0.2f, 0.2f, 0.2f);
            for (int i = 0; i < _sortedProfileData.Count; i += 2)
            {
                float top    = GetLineY(yOffset, LineHeight, LinePadding, false, i - 1);
                float bottom = GetLineY(yOffset, LineHeight, LinePadding, false, i);

                // Skip rendering out of bounds bars
                if (top < 0 || bottom > Height)
                    continue;

                GL.Vertex2(0, bottom);
                GL.Vertex2(0, top);
                GL.Vertex2(Width, top);

                GL.Vertex2(Width, top);
                GL.Vertex2(Width, bottom);
                GL.Vertex2(0, bottom);
            }
            GL.End();
            _maxScroll = (LineHeight + LinePadding) * (_sortedProfileData.Count - 1);
            #endregion

            // Display category
            #region Category
            verticalIndex = 0;
            foreach (var entry in _sortedProfileData)
            {
                float y = GetLineY(yOffset, LineHeight, LinePadding, true, verticalIndex++);
                width = _fontService.DrawText(entry.Key.Category, xOffset, y, LineHeight);
                if (width > maxWidth)
                {
                    maxWidth = width;
                }
            }
            GL.Disable(EnableCap.ScissorTest);

            width = _fontService.DrawText("Category", xOffset, Height - TitleFontHeight, TitleFontHeight);
            if (width > maxWidth)
                maxWidth = width;

            xOffset += maxWidth + ColumnSpacing;
            #endregion

            // Display session group
            #region Session Group
            maxWidth      = 0;
            verticalIndex = 0;

            GL.Enable(EnableCap.ScissorTest);
            foreach (var entry in _sortedProfileData)
            {
                float y = GetLineY(yOffset, LineHeight, LinePadding, true, verticalIndex++);
                width = _fontService.DrawText(entry.Key.SessionGroup, xOffset, y, LineHeight);
                if (width > maxWidth)
                {
                    maxWidth = width;
                }
            }
            GL.Disable(EnableCap.ScissorTest);

            width = _fontService.DrawText("Group", xOffset, Height - TitleFontHeight, TitleFontHeight);
            if (width > maxWidth)
                maxWidth = width;

            xOffset += maxWidth + ColumnSpacing;
            #endregion

            // Display session item

            #region Session Item
            maxWidth      = 0;
            verticalIndex = 0;
            GL.Enable(EnableCap.ScissorTest);
            foreach (var entry in _sortedProfileData)
            {
                float y = GetLineY(yOffset, LineHeight, LinePadding, true, verticalIndex++);
                width = _fontService.DrawText(entry.Key.SessionItem, xOffset, y, LineHeight);
                if (width > maxWidth)
                {
                    maxWidth = width;
                }
            }
            GL.Disable(EnableCap.ScissorTest);

            width = _fontService.DrawText("Item", xOffset, Height - TitleFontHeight, TitleFontHeight);
            if (width > maxWidth)
                maxWidth = width;

            xOffset += maxWidth + ColumnSpacing;
            _buttons[(int)ButtonIndex.TagTitle].UpdateSize(0, Height - TitleFontHeight, 0, (int)xOffset, TitleFontHeight);
            #endregion

            // Time bars
            DrawBars(xOffset, yOffset);

            _fontService.DrawText("Blue: Instant,  Green: Avg,  Red: Total", xOffset, Height - TitleFontHeight, TitleFontHeight);
            xOffset = Width - 360;

            // Display timestamps

            #region Timestamps
            verticalIndex = 0;
            GL.Enable(EnableCap.ScissorTest);
            foreach (var entry in _sortedProfileData)
            {
                float y = GetLineY(yOffset, LineHeight, LinePadding, true, verticalIndex++);
                _fontService.DrawText($"{Profile.ConvertTicksToMS(entry.Value.Instant):F3} ({entry.Value.InstantCount})", xOffset, y, LineHeight);
                _fontService.DrawText($"{Profile.ConvertTicksToMS(entry.Value.AverageTime):F3}", ColumnSpacing + 120 + xOffset, y, LineHeight);
                _fontService.DrawText($"{Profile.ConvertTicksToMS(entry.Value.TotalTime):F3}", ColumnSpacing + ColumnSpacing + 200 + xOffset, y, LineHeight);
            }
            GL.Disable(EnableCap.ScissorTest);

            float yHeight = Height - TitleFontHeight;

            _fontService.DrawText("Instant (ms, count)", xOffset, yHeight, TitleFontHeight);
            _buttons[(int)ButtonIndex.InstantTitle].UpdateSize((int)xOffset, (int)yHeight, 0, (int)(ColumnSpacing + 100), TitleFontHeight);

            _fontService.DrawText("Average (ms)", ColumnSpacing + 120 + xOffset, yHeight, TitleFontHeight);
            _buttons[(int)ButtonIndex.AverageTitle].UpdateSize((int)(ColumnSpacing + 120 + xOffset), (int)yHeight, 0, (int)(ColumnSpacing + 100), TitleFontHeight);

            _fontService.DrawText("Total (ms)", ColumnSpacing + ColumnSpacing + 200 + xOffset, yHeight, TitleFontHeight);
            _buttons[(int)ButtonIndex.TotalTitle].UpdateSize((int)(ColumnSpacing + ColumnSpacing + 200 + xOffset), (int)yHeight, 0, Width, TitleFontHeight);
            #endregion

            #region Bottom bar
            // Show/Hide Inactive
            float widthShowHideButton = _buttons[(int)ButtonIndex.ShowHideInactive].UpdateSize($"{(_showInactive ? "Hide" : "Show")} Inactive", 5, 5, 4, 16);

            // Play/Pause
            width = _buttons[(int)ButtonIndex.Pause].UpdateSize(_paused ? "Play" : "Pause", 15 + (int)widthShowHideButton, 5, 4, 16) + widthShowHideButton;

            // Filter bar
            _fontService.DrawText($"{(_regexEnabled ? "Regex " : "Filter")}: {_filterText}", 25 + width, 7, 16);
            _buttons[(int) ButtonIndex.FilterBar].UpdateSize((int)(25 + width), 0, 0, Width, FilterHeight);
            #endregion

            // Draw buttons
            foreach (ProfileButton button in _buttons)
            {
                button.Draw();
            }
            
            // Dividing lines
            #region Dividing lines
            GL.Color3(Color.White);
            GL.Begin(PrimitiveType.Lines);
            // Top divider
            GL.Vertex2(0, Height -TitleHeight);
            GL.Vertex2(Width, Height - TitleHeight);

            // Bottom divider
            GL.Vertex2(0, FilterHeight);
            GL.Vertex2(Width, FilterHeight);

            // Bottom vertical divider
            GL.Vertex2(widthShowHideButton + 10, 0);
            GL.Vertex2(widthShowHideButton + 10, FilterHeight);

            GL.Vertex2(width + 20, 0);
            GL.Vertex2(width + 20, FilterHeight);
            GL.End();
            #endregion

            _redrawPending = false;
            SwapBuffers();
        }
        #endregion

        private void FilterBackspace()
        {
            if (_filterText.Length <= 1)
            {
                _filterText = "";
            }
            else
            {
                _filterText = _filterText.Remove(_filterText.Length - 1, 1);
            }
        }

        private float GetLineY(float offset, float lineHeight, float padding, bool centre, int line)
        {
            return Height + offset - lineHeight - padding - ((lineHeight + padding) * line) + ((centre) ? padding : 0);
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            _filterText += e.KeyChar;
            _profileUpdated = true;
        }

        protected override void OnKeyDown(KeyboardKeyEventArgs e)
        {
            if (e.Key == Key.BackSpace)
            {
                _profileUpdated = _backspaceDown = true;
                return;
            }
            base.OnKeyUp(e);
        }

        protected override void OnKeyUp(KeyboardKeyEventArgs e)
        {
            if (e.Key == Key.BackSpace)
            {
                _backspaceDown = false;
                return;
            }
            base.OnKeyUp(e);
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            foreach (ProfileButton button in _buttons)
            {
                if (button.ProcessClick(e.X, Height - e.Y))
                    return;
            }
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            _scrollPos += e.Delta * -30;
            if (_scrollPos < _minScroll)
                _scrollPos = _minScroll;
            if (_scrollPos > _maxScroll)
                _scrollPos = _maxScroll;

            _redrawPending = true;
        }
    }
}