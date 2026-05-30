
namespace ImagePerfect.Repository.IRepository
{
    public interface IUnitOfWork
    {
        IFolderRepository Folder { get; }
        IImageRepository Image { get; }
        ISettingsRepository Settings { get; }
        ISaveDirectoryRepository SaveDirectory { get; }
    }
}
