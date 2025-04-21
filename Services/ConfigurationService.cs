using DatabaseSyncApp.Core;
using DatabaseSyncApp.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DatabaseSyncApp.Services
{
    /// <summary>
    /// 配置服务实现，负责从JSON文件加载同步任务配置
    /// </summary>
    public class ConfigurationService : IConfigurationService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ConfigurationService> _logger;
        private SyncConfiguration _syncConfiguration;

        public ConfigurationService(IConfiguration configuration, ILogger<ConfigurationService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            LoadConfiguration();
        }

        /// <summary>
        /// 加载配置文件
        /// </summary>
        private void LoadConfiguration()
        {
            try
            {
                _logger.LogInformation("正在加载同步配置...");
                string configJson = File.ReadAllText("dbsync.config.json");
                _syncConfiguration = JsonConvert.DeserializeObject<SyncConfiguration>(configJson);
                _logger.LogInformation($"成功加载配置，找到{_syncConfiguration.SyncTasks.Count}个同步任务。");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载配置文件失败: {Message}", ex.Message);
                _syncConfiguration = new SyncConfiguration();
            }
        }

        /// <summary>
        /// 获取所有同步任务配置
        /// </summary>
        public List<SyncTask> GetSyncTasks()
        {
            return _syncConfiguration.SyncTasks;
        }

        /// <summary>
        /// 获取全局设置
        /// </summary>
        public GlobalSettings GetGlobalSettings()
        {
            return _syncConfiguration.GlobalSettings;
        }

        /// <summary>
        /// 获取指定名称的同步任务配置
        /// </summary>
        public SyncTask GetSyncTaskByName(string taskName)
        {
            return _syncConfiguration.SyncTasks.FirstOrDefault(t => t.TaskName.Equals(taskName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 重新加载配置
        /// </summary>
        public void ReloadConfiguration()
        {
            LoadConfiguration();
        }
    }
}