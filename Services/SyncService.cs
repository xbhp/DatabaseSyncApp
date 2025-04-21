using DatabaseSyncApp.Core;
using DatabaseSyncApp.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DatabaseSyncApp.Services
{
    /// <summary>
    /// 同步服务实现，负责执行数据库之间的同步操作
    /// </summary>
    public class SyncService : ISyncService
    {
        private readonly ILogger<SyncService> _logger;
        private readonly IDatabaseConnectionFactory _connectionFactory;
        private readonly string _syncStateFilePath = "sync_state.txt";

        public SyncService(ILogger<SyncService> logger, IDatabaseConnectionFactory connectionFactory)
        {
            _logger = logger;
            _connectionFactory = connectionFactory;
            EnsureSyncStateFileExists();
        }

        /// <summary>
        /// 确保同步状态文件存在
        /// </summary>
        private void EnsureSyncStateFileExists()
        {
            if (!File.Exists(_syncStateFilePath))
            {
                File.WriteAllText(_syncStateFilePath, string.Empty);
            }
        }

        /// <summary>
        /// 执行同步任务
        /// </summary>
        public async Task ExecuteSyncTaskAsync(SyncTask syncTask)
        {
            _logger.LogInformation($"开始执行同步任务: {syncTask.TaskName}");

            foreach (var tableMapping in syncTask.TableMappings)
            {
                try
                {
                    await SyncTableAsync(syncTask, tableMapping);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"同步表 {tableMapping.SourceTable} 到 {tableMapping.TargetTable} 失败: {ex.Message}");
                }
            }

            _logger.LogInformation($"同步任务 {syncTask.TaskName} 完成");
        }

        /// <summary>
        /// 同步单个表
        /// </summary>
        public async Task SyncTableAsync(SyncTask syncTask, TableMapping tableMapping)
        {
            _logger.LogInformation($"开始同步表: {tableMapping.SourceTable} -> {tableMapping.TargetTable}");

            // 确定使用的同步方法，优先使用表级别的设置，如果没有则使用任务级别的设置
            string syncMethod = !string.IsNullOrEmpty(tableMapping.SyncMethod) 
                ? tableMapping.SyncMethod 
                : syncTask.SyncSettings.SyncMethod;
                
            _logger.LogDebug($"同步方法: {syncMethod}");

            // 获取上次同步值
            string lastSyncValue = GetLastSyncValue(syncTask.TaskName, tableMapping.SourceTable);
            _logger.LogDebug($"上次同步值: {lastSyncValue}");

            // 创建数据库连接
            using var sourceConnection = _connectionFactory.CreateConnection(
                syncTask.SourceDb.ConnectionString, 
                syncTask.SourceDb.ProviderName);
            using var targetConnection = _connectionFactory.CreateConnection(
                syncTask.TargetDb.ConnectionString, 
                syncTask.TargetDb.ProviderName);

            // 打开连接
            sourceConnection.Open();
            targetConnection.Open();

            // 获取源数据库中的变更数据
            var changedRows = await GetChangedRowsAsync(
                sourceConnection, 
                syncTask, 
                tableMapping, 
                lastSyncValue);

            if (changedRows.Count == 0)
            {
                _logger.LogInformation($"表 {tableMapping.SourceTable} 没有需要同步的数据");
                return;
            }

            _logger.LogInformation($"找到 {changedRows.Count} 条需要同步的记录");

            // 同步数据到目标数据库
            int syncedCount = await SyncDataToTargetAsync(
                targetConnection, 
                syncTask, 
                tableMapping, 
                changedRows);

            _logger.LogInformation($"成功同步 {syncedCount} 条记录");

            // 获取最新的同步值
            string newSyncValue;
                
            if (syncMethod.Equals("CDC", StringComparison.OrdinalIgnoreCase) && 
                syncTask.SyncSettings.CdcSettings.EnableCdc)
            {
                // CDC方式，尝试从结果中获取最后一个LSN值
                newSyncValue = GetLatestSyncValue(changedRows, null);
            }
            else
            {
                // 时间戳或行版本方式
                newSyncValue = GetLatestSyncValue(changedRows, tableMapping.TrackingColumn);
            }
            
            if (!string.IsNullOrEmpty(newSyncValue))
            {
                // 更新同步状态
                UpdateLastSyncValue(syncTask.TaskName, tableMapping.SourceTable, newSyncValue);
                _logger.LogDebug($"更新同步值为: {newSyncValue}");
            }
        }

        /// <summary>
        /// 获取变更的数据行
        /// </summary>
        private async Task<List<Dictionary<string, object>>> GetChangedRowsAsync(
            IDbConnection connection, 
            SyncTask syncTask, 
            TableMapping tableMapping, 
            string lastSyncValue)
        {
            var result = new List<Dictionary<string, object>>();
            string sourceTable = tableMapping.SourceTable;
            string trackingColumn = tableMapping.TrackingColumn;
            string paramPrefix = _connectionFactory.GetParameterPrefix(syncTask.SourceDb.ProviderName);
            
            // 确定使用的同步方法，优先使用表级别的设置，如果没有则使用任务级别的设置
            string syncMethod = !string.IsNullOrEmpty(tableMapping.SyncMethod) 
                ? tableMapping.SyncMethod 
                : syncTask.SyncSettings.SyncMethod;
            
            // 如果是CDC方式，使用CDC特定的查询
            if (syncMethod.Equals("CDC", StringComparison.OrdinalIgnoreCase) && 
                syncTask.SyncSettings.CdcSettings.EnableCdc)
            {
                return await GetChangedRowsWithCdcAsync(connection, syncTask, tableMapping, lastSyncValue);
            }
            
            // 非CDC方式，使用原有的时间戳或行版本方式
            // 构建查询SQL
            StringBuilder sql = new StringBuilder();
            sql.Append($"SELECT ");

            // 添加所有需要的列
            var columnList = tableMapping.ColumnMappings.Select(c => c.Source).ToList();
            sql.Append(string.Join(", ", columnList));

            sql.Append($" FROM {sourceTable} WHERE 1=1");

            // 添加时间戳或版本号过滤条件
            if (!string.IsNullOrEmpty(lastSyncValue) && !string.IsNullOrEmpty(trackingColumn))
            {
                if (syncMethod.Equals("Timestamp", StringComparison.OrdinalIgnoreCase))
                {
                    sql.Append($" AND {trackingColumn} > {paramPrefix}lastSyncValue");
                }
                else if (syncMethod.Equals("RowVersion", StringComparison.OrdinalIgnoreCase))
                {
                    sql.Append($" AND {trackingColumn} > {paramPrefix}lastSyncValue");
                }
            }

            // 添加排序
            sql.Append($" ORDER BY {trackingColumn} ASC");

            // 添加批次大小限制
            if (syncTask.SourceDb.Type.Equals("MySQL", StringComparison.OrdinalIgnoreCase))
            {
                sql.Append($" LIMIT {syncTask.SyncSettings.BatchSize}");
            }
            else if (syncTask.SourceDb.Type.Equals("SQLServer", StringComparison.OrdinalIgnoreCase))
            {
                // 对于SQL Server 2012及以上版本，使用OFFSET-FETCH
                sql = new StringBuilder();
                sql.Append($"SELECT TOP {syncTask.SyncSettings.BatchSize} ");
                sql.Append(string.Join(", ", columnList));
                sql.Append($" FROM {sourceTable} WHERE 1=1");

                if (!string.IsNullOrEmpty(lastSyncValue) && !string.IsNullOrEmpty(trackingColumn))
                {
                    if (syncMethod.Equals("Timestamp", StringComparison.OrdinalIgnoreCase))
                    {
                        sql.Append($" AND {trackingColumn} > {paramPrefix}lastSyncValue");
                    }
                    else if (syncMethod.Equals("RowVersion", StringComparison.OrdinalIgnoreCase))
                    {
                        sql.Append($" AND {trackingColumn} > {paramPrefix}lastSyncValue");
                    }
                }

                sql.Append($" ORDER BY {trackingColumn} ASC");
            }

            _logger.LogDebug($"执行查询: {sql}");

            // 创建命令
            using var command = _connectionFactory.CreateCommand(connection, sql.ToString());

            // 添加参数
            if (!string.IsNullOrEmpty(lastSyncValue) && !string.IsNullOrEmpty(trackingColumn))
            {
                var parameter = _connectionFactory.CreateParameter(command, $"{paramPrefix}lastSyncValue", lastSyncValue);
                command.Parameters.Add(parameter);
            }

            // 执行查询
            using var reader = await Task.Run(() => command.ExecuteReader());

            // 处理结果
            while (reader.Read())
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    string columnName = reader.GetName(i);
                    object value = reader.GetValue(i);
                    row[columnName] = value == DBNull.Value ? null : value;
                }
                result.Add(row);
            }

            return result;
        }

        /// <summary>
        /// 同步数据到目标数据库
        /// </summary>
        private async Task<int> SyncDataToTargetAsync(
            IDbConnection connection, 
            SyncTask syncTask, 
            TableMapping tableMapping, 
            List<Dictionary<string, object>> changedRows)
        {
            int syncedCount = 0;
            string targetTable = tableMapping.TargetTable;
            string paramPrefix = _connectionFactory.GetParameterPrefix(syncTask.TargetDb.ProviderName);

            // 开始事务
            using var transaction = connection.BeginTransaction();

            try
            {
                foreach (var row in changedRows)
                {
                    // 检查记录是否存在
                    bool recordExists = await CheckRecordExistsAsync(
                        connection, 
                        transaction, 
                        syncTask, 
                        tableMapping, 
                        row);

                    if (recordExists)
                    {
                        // 更新记录
                        await UpdateRecordAsync(
                            connection, 
                            transaction, 
                            syncTask, 
                            tableMapping, 
                            row);
                    }
                    else
                    {
                        // 插入记录
                        await InsertRecordAsync(
                            connection, 
                            transaction, 
                            syncTask, 
                            tableMapping, 
                            row);
                    }

                    syncedCount++;
                }

                // 提交事务
                transaction.Commit();
                return syncedCount;
            }
            catch (Exception ex)
            {
                // 回滚事务
                transaction.Rollback();
                _logger.LogError(ex, $"同步数据到目标数据库失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 检查记录是否存在
        /// </summary>
        private async Task<bool> CheckRecordExistsAsync(
            IDbConnection connection, 
            IDbTransaction transaction, 
            SyncTask syncTask, 
            TableMapping tableMapping, 
            Dictionary<string, object> row)
        {
            string targetTable = tableMapping.TargetTable;
            string paramPrefix = _connectionFactory.GetParameterPrefix(syncTask.TargetDb.ProviderName);

            // 获取主键列
            var primaryKeyMapping = tableMapping.ColumnMappings.FirstOrDefault(c => c.IsPrimaryKey);
            if (primaryKeyMapping == null)
            {
                throw new InvalidOperationException($"表 {tableMapping.SourceTable} 没有定义主键列");
            }

            // 构建查询SQL
            string sql = $"SELECT COUNT(1) FROM {targetTable} WHERE {primaryKeyMapping.Target} = {paramPrefix}primaryKeyValue";

            // 创建命令
            using var command = _connectionFactory.CreateCommand(connection, sql);
            command.Transaction = transaction;

            // 添加参数
            object primaryValue = row[primaryKeyMapping.Source];
            var parameter = _connectionFactory.CreateParameter(command, $"{paramPrefix}primaryKeyValue", primaryValue);
            command.Parameters.Add(parameter);
            // 执行查询
            int count = Convert.ToInt32(await Task.Run(() => command.ExecuteScalar()));
            return count > 0;
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        private async Task UpdateRecordAsync(
            IDbConnection connection, 
            IDbTransaction transaction, 
            SyncTask syncTask, 
            TableMapping tableMapping, 
            Dictionary<string, object> row)
        {
            string targetTable = tableMapping.TargetTable;
            string paramPrefix = _connectionFactory.GetParameterPrefix(syncTask.TargetDb.ProviderName);

            // 获取主键列
            var primaryKeyMapping = tableMapping.ColumnMappings.FirstOrDefault(c => c.IsPrimaryKey);
            if (primaryKeyMapping == null)
            {
                throw new InvalidOperationException($"表 {tableMapping.SourceTable} 没有定义主键列");
            }

            // 构建更新SQL
            StringBuilder sql = new StringBuilder();
            sql.Append($"UPDATE {targetTable} SET ");

            // 添加更新列
            var updateColumns = tableMapping.ColumnMappings.Where(c => !c.IsPrimaryKey).ToList();
            for (int i = 0; i < updateColumns.Count; i++)
            {
                var column = updateColumns[i];
                sql.Append($"{column.Target} = {paramPrefix}{column.Target}");
                if (i < updateColumns.Count - 1)
                {
                    sql.Append(", ");
                }
            }

            // 添加条件
            sql.Append($" WHERE {primaryKeyMapping.Target} = {paramPrefix}{primaryKeyMapping.Target}");

            // 创建命令
            using var command = _connectionFactory.CreateCommand(connection, sql.ToString());
            command.Transaction = transaction;

            // 添加参数
            foreach (var column in tableMapping.ColumnMappings)
            {
                object value = row[column.Source];
                var parameter = _connectionFactory.CreateParameter(command, $"{paramPrefix}{column.Target}", value);
                command.Parameters.Add(parameter);
            }

            // 执行更新
            await Task.Run(() => command.ExecuteNonQuery());
        }

        /// <summary>
        /// 插入记录
        /// </summary>
        private async Task InsertRecordAsync(
            IDbConnection connection, 
            IDbTransaction transaction, 
            SyncTask syncTask, 
            TableMapping tableMapping, 
            Dictionary<string, object> row)
        {
            string targetTable = tableMapping.TargetTable;
            string paramPrefix = _connectionFactory.GetParameterPrefix(syncTask.TargetDb.ProviderName);
            
            // 检查是否有标识列
            var identityColumn = tableMapping.ColumnMappings.FirstOrDefault(c => c.IsPrimaryKey);
            bool isSqlServer = syncTask.TargetDb.ProviderName.ToLower().Contains("sqlclient");
            bool needIdentityInsert = isSqlServer && identityColumn != null;
            
            try
            {
                // 如果是SQL Server且有标识列，需要开启IDENTITY_INSERT
                if (needIdentityInsert)
                {
                    string setIdentityInsertOn = $"SET IDENTITY_INSERT {targetTable} ON";
                    using var identityCommand = _connectionFactory.CreateCommand(connection, setIdentityInsertOn);
                    identityCommand.Transaction = transaction;
                    await Task.Run(() => identityCommand.ExecuteNonQuery());
                    _logger.LogDebug($"已开启表 {targetTable} 的IDENTITY_INSERT");
                }

                // 构建插入SQL
                StringBuilder sql = new StringBuilder();
                sql.Append($"INSERT INTO {targetTable} (");

                // 添加列名
                var columns = tableMapping.ColumnMappings.Select(c => c.Target).ToList();
                sql.Append(string.Join(", ", columns));

                // 添加参数
                sql.Append(") VALUES (");
                for (int i = 0; i < columns.Count; i++)
                {
                    sql.Append($"{paramPrefix}{columns[i]}");
                    if (i < columns.Count - 1)
                    {
                        sql.Append(", ");
                    }
                }
                sql.Append(")");

                // 创建命令
                using var command = _connectionFactory.CreateCommand(connection, sql.ToString());
                command.Transaction = transaction;

                // 添加参数
                foreach (var column in tableMapping.ColumnMappings)
                {
                    object value = row[column.Source];
                    var parameter = _connectionFactory.CreateParameter(command, $"{paramPrefix}{column.Target}", value);
                    command.Parameters.Add(parameter);
                }

                // 执行插入
                await Task.Run(() => command.ExecuteNonQuery());
            }
            finally
            {
                // 如果是SQL Server且有标识列，需要关闭IDENTITY_INSERT
                if (needIdentityInsert)
                {
                    string setIdentityInsertOff = $"SET IDENTITY_INSERT {targetTable} OFF";
                    using var identityCommand = _connectionFactory.CreateCommand(connection, setIdentityInsertOff);
                    identityCommand.Transaction = transaction;
                    await Task.Run(() => identityCommand.ExecuteNonQuery());
                    _logger.LogDebug($"已关闭表 {targetTable} 的IDENTITY_INSERT");
                }
            }
        }

        /// <summary>
        /// 使用CDC方式获取变更的数据行
        /// </summary>
        private async Task<List<Dictionary<string, object>>> GetChangedRowsWithCdcAsync(
            IDbConnection connection, 
            SyncTask syncTask, 
            TableMapping tableMapping, 
            string lastSyncValue)
        {
            var result = new List<Dictionary<string, object>>();
            string sourceTable = tableMapping.SourceTable;
            string paramPrefix = _connectionFactory.GetParameterPrefix(syncTask.SourceDb.ProviderName);
            bool isLegacySqlServer = syncTask.SyncSettings.CdcSettings.UseLegacySqlServerCdc;
            string captureInstance = !string.IsNullOrEmpty(syncTask.SyncSettings.CdcSettings.CaptureInstance) 
                ? syncTask.SyncSettings.CdcSettings.CaptureInstance 
                : sourceTable;
            
            _logger.LogInformation($"使用CDC方式同步表 {sourceTable}，捕获实例: {captureInstance}");
            
            // 如果有自定义CDC查询，则使用自定义查询
            if (!string.IsNullOrEmpty(syncTask.SyncSettings.CdcSettings.CustomCdcQuery))
            {
                string customQuery = syncTask.SyncSettings.CdcSettings.CustomCdcQuery;
                _logger.LogDebug($"使用自定义CDC查询: {customQuery}");
                
                using var customCommand = _connectionFactory.CreateCommand(connection, customQuery);
                
                // 添加参数
                if (!string.IsNullOrEmpty(lastSyncValue))
                {
                    var parameter = _connectionFactory.CreateParameter(customCommand, $"{paramPrefix}lastSyncValue", lastSyncValue);
                    customCommand.Parameters.Add(parameter);
                }
                
                using var customReader = await Task.Run(() => customCommand.ExecuteReader());
                return ProcessReaderResults(customReader);
            }
            
            // 构建CDC查询
            StringBuilder sql = new StringBuilder();
            
            // 获取所需列
            var columnList = tableMapping.ColumnMappings.Select(c => c.Source).ToList();
            
            if (isLegacySqlServer)
            {
                // 老版本SQL Server (2005/2008) CDC查询
                // 使用cdc.fn_cdc_get_all_changes_<capture_instance>函数
                sql.Append("DECLARE @begin_lsn binary(10), @end_lsn binary(10)\n");
                
                if (string.IsNullOrEmpty(lastSyncValue))
                {
                    // 如果没有上次同步值，获取最早的变更
                    sql.Append("SELECT @begin_lsn = sys.fn_cdc_get_min_lsn('" + captureInstance + "')\n");
                }
                else
                {
                    // 使用上次同步值作为起始LSN
                    sql.Append($"SELECT @begin_lsn = CONVERT(binary(10), '{lastSyncValue}', 1)\n");
                }
                
                // 获取当前最大LSN作为结束LSN
                sql.Append("SELECT @end_lsn = sys.fn_cdc_get_max_lsn()\n");
                
                // 查询变更数据
                sql.Append($"SELECT TOP {syncTask.SyncSettings.BatchSize} ");
                sql.Append(string.Join(", ", columnList));
                sql.Append($" FROM cdc.fn_cdc_get_all_changes_{captureInstance}(@begin_lsn, @end_lsn, 'all')\n");
                sql.Append("ORDER BY __$start_lsn ASC");
            }
            else
            {
                // 现代SQL Server (2012+) CDC查询
                // 使用sys.sp_cdc_get_all_changes存储过程
                sql.Append($"EXEC sys.sp_cdc_get_all_changes_{captureInstance} ");
                
                if (string.IsNullOrEmpty(lastSyncValue))
                {
                    // 如果没有上次同步值，获取最早的变更
                    sql.Append("@from_lsn = NULL, ");
                }
                else
                {
                    // 使用上次同步值作为起始LSN
                    sql.Append($"@from_lsn = CONVERT(binary(10), '{lastSyncValue}', 1), ");
                }
                
                // 使用当前最大LSN作为结束LSN
                sql.Append("@to_lsn = sys.fn_cdc_get_max_lsn(), ");
                sql.Append("@row_filter_option = 'all'");
            }
            
            _logger.LogDebug($"执行CDC查询: {sql}");
            
            // 创建命令
            using var command = _connectionFactory.CreateCommand(connection, sql.ToString());
            
            // 执行查询
            using var reader = await Task.Run(() => command.ExecuteReader());
            return ProcessReaderResults(reader);
        }
        
        /// <summary>
        /// 处理数据读取器结果
        /// </summary>
        private List<Dictionary<string, object>> ProcessReaderResults(IDataReader reader)
        {
            var result = new List<Dictionary<string, object>>();
            
            while (reader.Read())
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    string columnName = reader.GetName(i);
                    object value = reader.GetValue(i);
                    
                    // 保留CDC的LSN列，但跳过其他CDC系统列
                    if (columnName.StartsWith("__$") && columnName != "__$start_lsn")
                    {
                        continue;
                    }
                    
                    row[columnName] = value == DBNull.Value ? null : value;
                }
                result.Add(row);
            }
            
            return result;
        }
        
        /// <summary>
        /// 获取最新的同步值
        /// </summary>
        private string GetLatestSyncValue(List<Dictionary<string, object>> rows, string trackingColumn)
        {
            if (rows.Count == 0)
            {
                return string.Empty;
            }

            // 获取最后一行
            var lastRow = rows.Last();
            
            // 检查是否有CDC的LSN值
            if (lastRow.TryGetValue("__$start_lsn", out object lsnValue) && lsnValue != null)
            {
                // CDC方式，使用LSN作为同步值
                return Convert.ToBase64String((byte[])lsnValue);
            }
            else if (!string.IsNullOrEmpty(trackingColumn) && lastRow.TryGetValue(trackingColumn, out object value) && value != null)
            {
                // 时间戳或行版本方式，使用跟踪列值
                return value.ToString();
            }

            return string.Empty;
        }

        /// <summary>
        /// 获取上次同步值
        /// </summary>
        public string GetLastSyncValue(string taskName, string tableName)
        {
            string key = $"{taskName}:{tableName}";
            var lines = File.ReadAllLines(_syncStateFilePath);

            foreach (var line in lines)
            {
                if (line.StartsWith(key + "="))
                {
                    return line.Substring(key.Length + 1);
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// 更新上次同步值
        /// </summary>
        public void UpdateLastSyncValue(string taskName, string tableName, string syncValue)
        {
            string key = $"{taskName}:{tableName}";
            var lines = File.ReadAllLines(_syncStateFilePath).ToList();

            bool found = false;
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].StartsWith(key + "="))
                {
                    lines[i] = $"{key}={syncValue}";
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                lines.Add($"{key}={syncValue}");
            }

            File.WriteAllLines(_syncStateFilePath, lines);
        }
    }
}