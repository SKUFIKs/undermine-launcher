using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using System;
using Avalonia.Interactivity;
using UnderMine.Models;

namespace UnderMine.IU
{
    public partial class SettingsWindow : Window
    {
        private bool labelsCreated = false;
        private UserData _userData;
        public SettingsWindow()
        {
            InitializeComponent();
            Load_Settings();
        }
        private void MemorySlider_ValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            
        }

        private async void Load_Settings()
        {
            _userData = await UserData.Load();
            MemorySlider.Value = _userData.MemorySettings;
            
            MemorySlider.ValueChanged += (s, e) =>
            {
                _userData.MemorySettings = (int)e.NewValue;
            };
        }

        private void InstallationOverviewButton_OnClick(object? sender, RoutedEventArgs e)
        {
            
        }

        private async void SaveSettingsButton_OnClick(object? sender, RoutedEventArgs e)
        {
            if (_userData != null)
                await _userData.Save();
        }

        private async void DefaultSettingsButton_OnClick(object? sender, RoutedEventArgs e)
        {
            /*if (_userData != null)
                return;*/
            _userData.MemorySettings = 4096;
            _userData.GraphicSettings = "Avarege";
            
            MemorySlider.Value=_userData.MemorySettings;
            //GraphicsComboBox.SelectedItem = _userData.GraphicSettings;
            
            await _userData.Save();
            
            MemorySlider.ValueChanged += (s, e) =>
            {
                _userData.MemorySettings = (int)e.NewValue;
            };
        }

        private void ExitButton_OnClick(object? sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void BackButton_OnClick(object? sender, RoutedEventArgs e)
        {
           
        }
    }
}