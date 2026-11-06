using ImagePerfect.Repository.IRepository;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ImagePerfect.Models
{
    public class ImageTrashMethods
    {
        private readonly IUnitOfWork _unitOfWork;

        public ImageTrashMethods(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<bool> Add(ImageTrash trashedImage)
        {
            return await _unitOfWork.ImageTrash.Add(trashedImage, false);
        }
    }
}
