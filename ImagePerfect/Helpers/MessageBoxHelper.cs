using Avalonia.Controls;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ImagePerfect.Helpers
{
    public static class MessageBoxHelper
    {
        private static MessageBoxCustomParams CreateParams(string title, string message, List<ButtonDefinition> buttons, bool canResize = false, double? minHeight = null)
        {
            var parameters = new MessageBoxCustomParams
            {
                ButtonDefinitions = buttons,
                ContentTitle = title,
                ContentMessage = message,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SizeToContent = SizeToContent.WidthAndHeight, // <-- lets it grow with content
                MinWidth = 500, // optional, so it doesn't wrap too soon
                CanResize = canResize,
            };

            if(minHeight.HasValue)
                parameters.MinHeight = minHeight.Value;

            return parameters;
        }

        public static async Task ShowAsync(string title, string message, bool canResize = false, double? minHeight = null)
        {
            await MessageBoxManager.GetMessageBoxCustom(
                CreateParams(
                    title,
                    message,
                    new List<ButtonDefinition>
                    {
                        new ButtonDefinition { Name = "Ok" }
                    },
                    canResize,
                    minHeight)
            ).ShowWindowDialogAsync(Globals.MainWindow);
        }

        public static async Task<bool> ShowYesNoAsync(string title, string message)
        {
            var messageBox = MessageBoxManager.GetMessageBoxCustom(
                CreateParams(
                    title,
                    message,
                    new List<ButtonDefinition>
                    {
                        new ButtonDefinition { Name = "Yes" },
                        new ButtonDefinition { Name = "No" }
                    }));

            var result = await messageBox.ShowWindowDialogAsync(Globals.MainWindow);

            return result == "Yes";
        }
    }
}
