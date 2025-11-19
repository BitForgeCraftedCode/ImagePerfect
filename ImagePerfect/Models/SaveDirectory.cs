using Avalonia;
using ImagePerfect.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ImagePerfect.Models
{
    [Table("saved_directory")]
    public class SaveDirectory
    {
        [Key]
        [Column("SavedDirectoryId")]
        public int SavedDirectoryId { get; set; } = 1;

        [Column("SavedDirectory")]
        public string SavedDirectory { get; set; }

        [Column("SavedFolderPage")]
        public int SavedFolderPage { get; set; }

        [Column("SavedTotalFolderPages")]
        public int SavedTotalFolderPages { get; set; }

        [Column("SavedImagePage")]
        public int SavedImagePage { get; set; }

        [Column("SavedTotalImagePages")]
        public int SavedTotalImagePages { get; set; }

        [Column("XVector")]
        public double XVector { get; set; }

        [Column("YVector")]
        public double YVector { get; set; }

        [NotMapped]
        //Display name needs to be a prop so it can be shown in UI
        public string DisplayName { get; set; } = string.Empty;

        [NotMapped]
        public Vector SavedOffsetVector { get; set; } = new Vector();
        //filter variables
        [NotMapped]
        public ExplorerViewModel.Filters SavedCurrentFilter { get; set; } = ExplorerViewModel.Filters.None;
        [NotMapped]
        public string SavedSelectedLetterForFilter { get; set; } = "A";
        [NotMapped]
        public int SavedSelectedRatingForFilter { get; set; } = 0;
        [NotMapped]
        public int SavedSelectedYearForFilter { get; set; } = 0;
        [NotMapped]
        public int SavedSelectedMonthForFilter { get; set; } = 0;
        [NotMapped]
        public DateTimeOffset SavedStartDateForFilter { get; set; }
        [NotMapped]
        public DateTimeOffset SavedEndDateForFilter { get; set; }
        [NotMapped]
        public string SavedTagForFilter { get; set; } = string.Empty;
        [NotMapped]
        public string SavedTextForFilter { get; set; } = string.Empty;
        [NotMapped]
        public int SavedComboFolderFilterRating { get; set; } = 10;
        [NotMapped]
        public string SavedComboFolderFilterTagOne { get; set; } = string.Empty;
        [NotMapped]
        public string SavedComboFolderFilterTagTwo { get; set; } = string.Empty;
        [NotMapped]
        public bool SavedFilterInCurrentDirectory { get; set; } = true;
        [NotMapped]
        public bool SavedLoadFoldersAscending { get; set; } = true;
        [NotMapped]
        public List<FolderViewModel> SavedDirectoryFolders { get; } = new(); //runtime-only cache
        [NotMapped]
        public List<ImageViewModel> SavedDirectoryImages { get; } = new(); //runtime-only cache
    }
}
