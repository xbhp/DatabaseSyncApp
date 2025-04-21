using DatabaseSyncApp.Core;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using Microsoft.Data.SqlClient;
using System;
using System.Data;

namespace DatabaseSyncApp.Services
{
    /// <summary>
    /// 数据库连接工厂实现，负责创建不同类型数据库的连接
    /// </summary>
    public class DatabaseConnectionFactory : IDatabaseConnectionFactory
    {
        private readonly ILogger<DatabaseConnectionFactory> _logger;

        public DatabaseConnectionFactory(ILogger<DatabaseConnectionFactory> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 创建数据库连接
        /// </summary>
        public IDbConnection CreateConnection(string connectionString, string providerName)
        {
            _logger.LogDebug($"创建数据库连接，提供程序: {providerName}");
            
            IDbConnection connection = providerName.ToLower() switch
            {
                "mysql.data.mysqlclient" => new MySqlConnection(connectionString),
                "microsoft.data.sqlclient" => new SqlConnection(connectionString),
                _ => throw new ArgumentException($"不支持的数据库提供程序: {providerName}")
            };

            return connection;
        }

        /// <summary>
        /// 创建命令对象
        /// </summary>
        public IDbCommand CreateCommand(IDbConnection connection, string commandText, CommandType commandType = CommandType.Text)
        {
            IDbCommand command = connection.CreateCommand();
            command.CommandText = commandText;
            command.CommandType = commandType;
            return command;
        }

        /// <summary>
        /// 创建参数对象
        /// </summary>
        public IDbDataParameter CreateParameter(IDbCommand command, string parameterName, object value)
        {
            IDbDataParameter parameter = command.CreateParameter();
            parameter.ParameterName = parameterName;
            parameter.Value = value ?? DBNull.Value;
            return parameter;
        }

        /// <summary>
        /// 获取数据库类型的参数前缀
        /// </summary>
        public string GetParameterPrefix(string providerName)
        {
            return providerName.ToLower() switch
            {
                "mysql.data.mysqlclient" => "?",
                "microsoft.data.sqlclient" => "@",
                _ => "@"
            };
        }
    }
}