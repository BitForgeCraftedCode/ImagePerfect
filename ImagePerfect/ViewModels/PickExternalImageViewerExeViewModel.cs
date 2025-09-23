using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ImagePerfect.Helpers;
using ImagePerfect.Repository.IRepository;
using ReactiveUI;

namespace ImagePerfect.ViewModels
{
	public class PickExternalImageViewerExeViewModel : ViewModelBase
	{
        private readonly IUnitOfWork _unitOfWork;
        private readonly MainWindowViewModel _mainWindowViewModel;

        public PickExternalImageViewerExeViewModel(IUnitOfWork unitOfWork, MainWindowViewModel mainWindowViewModel)
        {
            _unitOfWork = unitOfWork;
            _mainWindowViewModel = mainWindowViewModel;
            _SelectExternalImageViewerExeInteraction = new Interaction<string, List<string>?>();
            SelectExternalImageViewerExeCommand = ReactiveCommand.CreateFromTask(SelectExternalImageViewerExe);
        }

        private List<string>? _ExternalImageViewerExePath;
        private readonly Interaction<string, List<string>?> _SelectExternalImageViewerExeInteraction;

        public Interaction<string, List<string>?> SelectExternalImageViewerExeInteraction { get { return _SelectExternalImageViewerExeInteraction; } }

        public ReactiveCommand<Unit, Unit> SelectExternalImageViewerExeCommand { get; }

        private async Task SelectExternalImageViewerExe()
        {
            _ExternalImageViewerExePath = await _SelectExternalImageViewerExeInteraction.Handle("Select Image Viewer Exe");
            //list will be empty if Cancel is pressed exit method
            if (_ExternalImageViewerExePath.Count == 0)
            {
                return;
            }

            Debug.WriteLine(PathHelper.FormatPathFromFilePicker(_ExternalImageViewerExePath[0]));
        }
    }
}