using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;
using DatabaseSyncApp.Core;
using DatabaseSyncApp.Services;
using DatabaseSyncApp.Models;

namespace DatabaseSyncApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                // 设置配置
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("dbsync.config.json", optional: false, reloadOnChange: true)
                    .Build();

                // 设置依赖注入
                var serviceProvider = new ServiceCollection()
                    .AddLogging(builder =>
                    {
                        builder.AddConsole();
                        builder.SetMinimumLevel(LogLevel.Information);
                    })
                    .AddSingleton<IConfiguration>(configuration)
                    .AddSingleton<IConfigurationService, ConfigurationService>()
                    .AddSingleton<IDatabaseConnectionFactory, DatabaseConnectionFactory>()
                    .AddSingleton<ISyncService, SyncService>()
                    .BuildServiceProvider();

                var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
                logger.LogInformation("数据库同步应用程序启动...");

                // 获取配置服务
                var configService = serviceProvider.GetRequiredService<IConfigurationService>();
                var syncTasks = configService.GetSyncTasks();

                if (syncTasks.Count == 0)
                {
                    logger.LogWarning("未找到同步任务配置。请检查dbsync.config.json文件。");
                    return;
                }

                logger.LogInformation($"找到{syncTasks.Count}个同步任务。");

                // 获取同步服务
                var syncService = serviceProvider.GetRequiredService<ISyncService>();

                // 执行所有同步任务
                foreach (var task in syncTasks)
                {
                    logger.LogInformation($"开始执行同步任务: {task.TaskName}");
                    try
                    {
                        await syncService.ExecuteSyncTaskAsync(task);
                        logger.LogInformation($"同步任务 {task.TaskName} 完成。");
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, $"同步任务 {task.TaskName} 执行失败: {ex.Message}");
                    }
                }

                logger.LogInformation("所有同步任务已完成。");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"程序执行过程中发生错误: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }

            Console.WriteLine("按任意键退出...");
            Console.ReadKey();
        }
    }
}