
namespace ImagePerfect.Repository.IRepository
{
    public interface IUnitOfWork
    {
        IFolderRepository Folder { get; }
        IImageRepository Image { get; }
        IImageTrashRepository ImageTrash { get; }
        ISettingsRepository Settings { get; }
        ISaveDirectoryRepository SaveDirectory { get; }
    }
}
