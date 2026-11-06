using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace ImagePerfect.Models
{
    [Table("images_trash")]
    public class ImageTrash
    {
        [Key]
        [Column("ImageId")]
        public int ImageId { get; set; }

        [Column("ImagePath")]
        public string ImagePath { get; set; }

        [Column("FileName")]
        public string FileName { get; set; }

        [Column("ImageRating")]
        public int ImageRating { get; set; }

        [Column("ImageFolderPath")]
        public string ImageFolderPath { get; set; } = string.Empty;

        [Column("ImageMetaDataScanned")]
        public bool ImageMetaDataScanned { get; set; }

        [Column("FolderId")]
        public int FolderId { get; set; }

        [Column("DateTaken")]
        public DateTime? DateTaken { get; set; }

        [NotMapped]
        public DateTime DateTrashed { get; set; }
    }
}
