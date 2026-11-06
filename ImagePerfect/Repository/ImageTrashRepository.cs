using ImagePerfect.Models;
using ImagePerfect.Repository.IRepository;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Text;

namespace ImagePerfect.Repository
{
    public class ImageTrashRepository : Repository<ImageTrash>, IImageTrashRepository
    {
        private readonly MySqlConnection _connection;
        private readonly string _connectionString;

        public ImageTrashRepository(MySqlConnection db, IConfiguration config) : base(db)
        {
            _connection = db;
            _connectionString = config.GetConnectionString("DefaultConnection");
        }
        //any ImageTrash model specific database methods here
    }
}
