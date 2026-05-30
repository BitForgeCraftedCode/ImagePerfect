using ImagePerfect.Models;
using ImagePerfect.Repository.IRepository;
using MySqlConnector;

namespace ImagePerfect.Repository
{
    public class SaveDirectoryRepository : Repository<SaveDirectory>, ISaveDirectoryRepository
    {
        private readonly MySqlConnection _connection;

        public SaveDirectoryRepository(MySqlConnection db) : base(db)
        {
            _connection = db;
        }

        //any SaveDirectory model specific database methods here
    }
}
