using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ImagePerfect.Models;
using ImagePerfect.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ImagePerfect.Views
{
    public partial class MainWindow : Window
    {
        private ScrollViewer _scrollViewer;
        private List<Button> _navButtons = new();
        private int _selectedButtonIndex = -1;
        public MainWindow()
        {
            InitializeComponent();
            _scrollViewer = this.FindControl<ScrollViewer>("FoldersAndImagesScrollViewer");
        }
        /*
         * I set up custom tab nav for Open and Back on Folders and Images
         */

        //Occurs when the control has been fully constructed in the visual tree and both layout and render are complete
        //get my tab-btn add them to the list and make suer focusable is true
        private void FolderCard_Loaded(object? sender, RoutedEventArgs e)
        {
            if (sender is Control card)
            {
                // find tab-able buttons inside this item only
                var found = card.GetVisualDescendants()
                                .OfType<Button>()
                                .Where(b => b.Classes.Contains("tab-btn"))
                                .ToList();

                foreach (var b in found)
                {
                    if (!_navButtons.Contains(b) && b.IsEnabled)
                    {
                        // make sure the button is focusable
                        b.Focusable = true;
                        _navButtons.Add(b);
                    }
                }
            }
        }
        //Occurs when the control is removed from the visual tree.
        //Open Back clears the folders so clear the buttons and reset index
        private void FolderCard_Unloaded(object? sender, RoutedEventArgs e)
        {
            _navButtons.Clear();
            _selectedButtonIndex = -1;
        }

        //this method runs whenever a key is pressed
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e); // calls the Window's original key handling

            if (this.DataContext is not MainWindowViewModel vm)
                return; // nothing to do if DataContext isn't set

            const double scrollAmount = 50; // adjust as needed
            switch (e.Key)
            {
                case Key.Down:
                    _scrollViewer.Offset = _scrollViewer.Offset.WithY(_scrollViewer.Offset.Y + scrollAmount);
                    e.Handled = true; // stops the system from doing any additional Down key action
                    break;
                case Key.Up:
                    _scrollViewer.Offset = _scrollViewer.Offset.WithY(_scrollViewer.Offset.Y - scrollAmount);
                    e.Handled = true;
                    break;
                case Key.Right:
                    vm.NextPageCommand.Execute().Subscribe();
                    e.Handled = true;
                    break;
                case Key.Left:
                    vm.PreviousPageCommand.Execute().Subscribe();
                    e.Handled = true;
                    break;
                //case Key.Tab:
                //    MoveSelection(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -1 : 1);
                //    e.Handled = true;
                //    break;
                //case Key.Enter:
                //    ActivateSelectedButton();
                //    e.Handled = true;
                //    break;
            }
        }
        //direction is 1 for Tab, -1 for Shift+Tab
        private void MoveSelection(int direction)
        {
            Debug.WriteLine(_navButtons.Count);
            if (_navButtons.Count == 0)
                return;

            _selectedButtonIndex += direction;

            if (_selectedButtonIndex >= _navButtons.Count)
                _selectedButtonIndex = 0;
            //hard to see but this will wrap back to the last button if you hit ctrl + tab on the first btn
            else if (_selectedButtonIndex < 0)
                _selectedButtonIndex = _navButtons.Count - 1;

            var button = _navButtons[_selectedButtonIndex];
            button.Focus();
        }

        private void ActivateSelectedButton()
        {
            if (_selectedButtonIndex < 0 || _selectedButtonIndex >= _navButtons.Count)
                return;

            var button = _navButtons[_selectedButtonIndex];
            if (button.Command?.CanExecute(button.CommandParameter) == true)
                button.Command.Execute(button.CommandParameter);
        }

        private void SelectAllButton_Click(object? sender, RoutedEventArgs e)
        {

            // check if any item is currently unselected
            bool anyUnselected = ImagesListBox.Items
                                  .OfType<ImageViewModel>()
                                  .Any(i => !i.IsSelected);

            if (anyUnselected)
            {
                // select all
                ImagesListBox.SelectedItems.Clear();
                foreach (ImageViewModel item in ImagesListBox.Items.OfType<ImageViewModel>())
                {
                    ImagesListBox.SelectedItems.Add(item);
                    item.IsSelected = true;
                }
            }
            else
            {
                // unselect all
                foreach (ImageViewModel item in ImagesListBox.Items.OfType<ImageViewModel>())
                {
                    ImagesListBox.SelectedItems.Remove(item);
                    item.IsSelected = false;
                }
            }   
        }

        //keeps Vms IsSelected in sync with ListBox Selection
        private void ListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            foreach (var added in e.AddedItems.OfType<ImageViewModel>())
                added.IsSelected = true;

            foreach (var removed in e.RemovedItems.OfType<ImageViewModel>())
                removed.IsSelected = false;
        }

        private void Image_Rating_Zero(object? sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.FindAncestorOfType<ListBoxItem>()?.DataContext is ImageViewModel imgVm)
            {
                imgVm.ImageRating = 0;
            }
        }
        //Sets Vms ImageRating to correct Star number before add rating command is called.
        private void Star_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is StarItem star &&
                btn.FindAncestorOfType<ListBoxItem>()?.DataContext is ImageViewModel imgVm)
            {
                
                imgVm.ImageRating = star.Number;
                // Command will execute via binding in XAML
            }
        }

        private void Folder_Rating_Zero(object? sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.FindAncestorOfType<StackPanel>()?.DataContext is FolderViewModel folderVm)
            {
                folderVm.FolderRating = 0;
            }
        }
        private void Folder_Star_Click(object? sender, RoutedEventArgs e)
        {
            if(sender is Button btn && btn.DataContext is StarItem star &&
                btn.FindAncestorOfType<StackPanel>()?.DataContext is FolderViewModel folderVm)
            {
                folderVm.FolderRating = star.Number;    
            }
        }

    }
}