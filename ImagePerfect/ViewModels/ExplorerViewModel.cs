using System;
using System.Collections.Generic;
using ReactiveUI;

namespace ImagePerfect.ViewModels
{
    /*
     * VM for Pagination Refresh and Filters
     */
	public class ExplorerViewModel : ViewModelBase
	{
        private readonly MainWindowViewModel _mainWindowViewModel;
        public ExplorerViewModel(MainWindowViewModel mainWindowViewModel)
        {
            _mainWindowViewModel = mainWindowViewModel;
        }
    }
}