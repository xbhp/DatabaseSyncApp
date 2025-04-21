using DatabaseSyncApp.Models;
using System.Threading.Tasks;

namespace DatabaseSyncApp.Core
{
    /// <summary>
    /// 同步服务接口，定义数据同步的核心功能
    /// </summary>
    public interface ISyncService
    {
        /// <summary>
        /// 执行同步任务
        /// </summary>
        /// <param name="syncTask">同步任务配置</param>
        /// <returns>异步任务</returns>
        Task ExecuteSyncTaskAsync(SyncTask syncTask);

        /// <summary>
        /// 同步单个表
        /// </summary>
        /// <param name="syncTask">同步任务配置</param>
        /// <param name="tableMapping">表映射配置</param>
        /// <returns>异步任务</returns>
        Task SyncTableAsync(SyncTask syncTask, TableMapping tableMapping);

        /// <summary>
        /// 获取上次同步时间
        /// </summary>
        /// <param name="taskName">任务名称</param>
        /// <param name="tableName">表名称</param>
        /// <returns>上次同步时间</returns>
        string GetLastSyncValue(string taskName, string tableName);

        /// <summary>
        /// 更新上次同步时间
        /// </summary>
        /// <param name="taskName">任务名称</param>
        /// <param name="tableName">表名称</param>
        /// <param name="syncValue">同步值</param>
        void UpdateLastSyncValue(string taskName, string tableName, string syncValue);
    }
}