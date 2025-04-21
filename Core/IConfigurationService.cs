using DatabaseSyncApp.Models;
using System.Collections.Generic;

namespace DatabaseSyncApp.Core
{
    /// <summary>
    /// 配置服务接口，负责加载和解析配置文件
    /// </summary>
    public interface IConfigurationService
    {
        /// <summary>
        /// 获取所有同步任务配置
        /// </summary>
        /// <returns>同步任务列表</returns>
        List<SyncTask> GetSyncTasks();

        /// <summary>
        /// 获取全局设置
        /// </summary>
        /// <returns>全局设置对象</returns>
        GlobalSettings GetGlobalSettings();

        /// <summary>
        /// 获取指定名称的同步任务配置
        /// </summary>
        /// <param name="taskName">任务名称</param>
        /// <returns>同步任务配置</returns>
        SyncTask GetSyncTaskByName(string taskName);

        /// <summary>
        /// 重新加载配置
        /// </summary>
        void ReloadConfiguration();
    }
}