using Avalonia.Controls.Shapes;
using ImagePerfect.Repository.IRepository;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using System;
using System.IO;
using System.Reactive;
using System.Threading.Tasks;
//https://dotnettutorials.net/lesson/unit-of-work-csharp-mvc/

namespace ImagePerfect.Repository
{
    /* 
      DESIGN OVERVIEW

      UnitOfWork is not injected directly via DI — instead, it's constructed manually inside each ViewModel method that needs DB access.

      Each call to CreateAsync:

        - Opens a new database connection from the MySqlDataSource pool.

        - Creates all repositories (FolderRepository, ImageRepository, etc.) that share this single connection.

        - When DisposeAsync() is called (automatically via await using), that connection is returned to the pool.

     This makes the Unit of Work lifetime short and self-contained — basically scoped to one ViewModel operation, like “load images”, “update folder tags”, etc.

     That's effectively a Scoped lifetime, but since you're in a desktop app (no HTTP requests), you're manually managing the scope.

     This ensures no long-lived open connections, no connection leaks, and transaction safety if you ever extend UoW to handle explicit transactions.
    */

    /*
     - MySqlDataSource (Singleton -- This means the application maintains one connection pool shared by all database operations): Provides connection pooling.
     - IConfiguration (Singleton -- The same config instance is shared across the app.): Shared app configuration.
     - MainWindowViewModel — (Transient, so each time the app requests it, a new instance is created (though in practice, you only build it once at startup)

     - UnitOfWork: Manages a single DB connection + repositories.
        - Created per ViewModel operation via CreateAsync()
        - Disposed after operation → returns connection to pool.
     - FolderMethods / ImageMethods: Thin wrappers to simplify repo calls.
     - ViewModels: Create short-lived UnitOfWork scopes when needed.
    
      Lifetime Summary:
       MySqlDataSource → Singleton
       IConfiguration → Singleton
       UnitOfWork → Scoped manually per ViewModel method
    
      Suitable for Avalonia MVVM desktop apps:
       - Keeps DB access efficient and safe
       - Avoids global connections
       - Keeps ViewModels clean and testable
     */
    public class UnitOfWork : IUnitOfWork, IAsyncDisposable
    {
        private readonly MySqlDataSource _dataSource;
        private readonly IConfiguration _configuration;
        private MySqlConnection? _connection;
        private bool _disposed = false;

        public IFolderRepository Folder { get; private set; } = null!;
        public IImageRepository Image { get; private set; } = null!;
        public ISettingsRepository Settings { get; private set; } = null!;
        public ISaveDirectoryRepository SaveDirectory { get; private set; } = null!;
        public UnitOfWork(MySqlDataSource dataSource, IConfiguration config)
        {
            _dataSource = dataSource;
            _configuration = config;
        }

        //Factory method to create and initialize automatically
        public static async Task<UnitOfWork> CreateAsync(MySqlDataSource dataSource, IConfiguration config)
        {
            UnitOfWork uow = new UnitOfWork(dataSource, config);
            await uow.InitializeInternalAsync();
            return uow;
        }

        private async Task InitializeInternalAsync()
        {
            _connection = await _dataSource.OpenConnectionAsync();

            Folder = new FolderRepository(_connection);
            Image = new ImageRepository(_connection, _configuration);
            Settings = new SettingsRepository(_connection);
            SaveDirectory = new SaveDirectoryRepository(_connection);
        }

        public async ValueTask DisposeAsync()
        {
            if (!_disposed && _connection is not null)
            {
                //DisposeAsync() -> calls CloseAsync() -> returns to pool -> frees managed resources
                await _connection.DisposeAsync();
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}
