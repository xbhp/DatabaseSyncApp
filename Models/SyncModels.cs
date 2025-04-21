using System.Collections.Generic;

namespace DatabaseSyncApp.Models
{
    public class SyncConfiguration
    {
        public List<SyncTask> SyncTasks { get; set; } = new List<SyncTask>();
        public GlobalSettings GlobalSettings { get; set; } = new GlobalSettings();
    }

    public class SyncTask
    {
        public string TaskName { get; set; } = string.Empty;
        public DatabaseConfig SourceDb { get; set; } = new DatabaseConfig();
        public DatabaseConfig TargetDb { get; set; } = new DatabaseConfig();
        public List<TableMapping> TableMappings { get; set; } = new List<TableMapping>();
        public SyncSettings SyncSettings { get; set; } = new SyncSettings();
    }

    public class DatabaseConfig
    {
        public string Type { get; set; } = string.Empty;
        public string ConnectionString { get; set; } = string.Empty;
        public string ProviderName { get; set; } = string.Empty;
    }

    public class TableMapping
    {
        public string SourceTable { get; set; } = string.Empty;
        public string TargetTable { get; set; } = string.Empty;
        public string PrimaryKey { get; set; } = string.Empty;
        public string TrackingColumn { get; set; } = string.Empty;
        /// <summary>
        /// 同步方法，可以覆盖SyncSettings中的全局设置
        /// </summary>
        public string SyncMethod { get; set; } = string.Empty;
        public List<ColumnMapping> ColumnMappings { get; set; } = new List<ColumnMapping>();
    }

    public class ColumnMapping
    {
        public string Source { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
        public bool IsPrimaryKey { get; set; } = false;
    }

    public class SyncSettings
    {
        public int BatchSize { get; set; } = 1000;
        public int SyncInterval { get; set; } = 300;
        public int RetryCount { get; set; } = 3;
        public int RetryDelaySeconds { get; set; } = 10;
        /// <summary>
        /// 同步方法：Timestamp(时间戳)、RowVersion(行版本)、CDC(变更数据捕获)
        /// </summary>
        public string SyncMethod { get; set; } = "Timestamp";
        
        /// <summary>
        /// CDC配置选项
        /// </summary>
        public CdcSettings CdcSettings { get; set; } = new CdcSettings();
    }
    
    /// <summary>
    /// CDC(变更数据捕获)配置
    /// </summary>
    public class CdcSettings
    {
        /// <summary>
        /// 是否启用CDC
        /// </summary>
        public bool EnableCdc { get; set; } = false;
        
        /// <summary>
        /// CDC捕获实例名称
        /// </summary>
        public string CaptureInstance { get; set; } = string.Empty;
        
        /// <summary>
        /// 是否使用旧版SQL Server CDC (2008/2005)
        /// </summary>
        public bool UseLegacySqlServerCdc { get; set; } = false;
        
        /// <summary>
        /// 自定义CDC查询
        /// </summary>
        public string CustomCdcQuery { get; set; } = string.Empty;
    }

    public class GlobalSettings
    {
        public string LogLevel { get; set; } = "Information";
        public string LogFilePath { get; set; } = "logs/dbsync.log";
        public bool EnableDetailedLogging { get; set; } = true;
        public int MaxLogFileSizeMB { get; set; } = 10;
        public int MaxLogFileCount { get; set; } = 5;
    }
}