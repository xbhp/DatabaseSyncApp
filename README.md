# 数据库同步应用程序

这是一个基于.NET的控制台应用程序，用于在不同类型的数据库（MySQL和SQL Server 12+）之间同步数据。应用程序使用JSON配置文件来定义同步任务，包括数据库连接信息和表字段映射关系。
DatabaseSync，mysql to sqlserver。Console program, configurable, can be modified into scheduled tasks, etc., and supports older versions of CDC...

## 功能特点

- 支持MySQL和SQL Server数据库之间的双向同步
- 使用JSON配置文件定义同步任务和表字段映射
- 支持基于时间戳或行版本号的变更跟踪方法
- 批量处理数据，提高同步效率
- 详细的日志记录，方便排查问题
- 支持多个同步任务并行执行
- 自动记录同步状态，支持断点续传

## 系统要求

- .NET 6.0或更高版本
- MySQL数据库
- SQL Server 12或更高版本

## 配置说明

应用程序使用`dbsync.config.json`文件来配置同步任务。配置文件包含以下主要部分：

### 同步任务配置

```json
{
  "syncTasks": [
    {
      "taskName": "任务名称",
      "sourceDb": {
        "type": "数据库类型",
        "connectionString": "连接字符串",
        "providerName": "提供程序名称"
      },
      "targetDb": {
        "type": "数据库类型",
        "connectionString": "连接字符串",
        "providerName": "提供程序名称"
      },
      "tableMappings": [
        {
          "sourceTable": "源表名",
          "targetTable": "目标表名",
          "primaryKey": "主键列名",
          "trackingColumn": "跟踪列名",
          "columnMappings": [
            { "source": "源列名", "target": "目标列名", "isPrimaryKey": true/false }
          ]
        }
      ],
      "syncSettings": {
        "batchSize": 1000,
        "syncInterval": 300,
        "retryCount": 3,
        "retryDelaySeconds": 10,
        "syncMethod": "Timestamp/RowVersion"
      }
    }
  ],
  "globalSettings": {
    "logLevel": "Information",
    "logFilePath": "logs/dbsync.log",
    "enableDetailedLogging": true,
    "maxLogFileSizeMB": 10,
    "maxLogFileCount": 5
  }
}
```

### 配置项说明

- **taskName**: 同步任务的名称
- **sourceDb/targetDb**: 源数据库和目标数据库的配置
  - **type**: 数据库类型，支持MySQL和SQLServer
  - **connectionString**: 数据库连接字符串
  - **providerName**: 数据库提供程序名称
- **tableMappings**: 表映射配置
  - **sourceTable/targetTable**: 源表和目标表的名称
  - **primaryKey**: 主键列名
  - **trackingColumn**: 用于跟踪变更的列名（通常是时间戳或版本号列）
  - **columnMappings**: 列映射配置
    - **source/target**: 源列和目标列的名称
    - **isPrimaryKey**: 是否为主键列
- **syncSettings**: 同步设置
  - **batchSize**: 每批处理的记录数
  - **syncInterval**: 同步间隔（秒）
  - **retryCount**: 失败重试次数
  - **retryDelaySeconds**: 重试延迟时间（秒）
  - **syncMethod**: 同步方法，支持Timestamp和RowVersion

## 使用方法

1. 配置`dbsync.config.json`文件，定义同步任务
2. 运行应用程序：`dotnet run`

## 注意事项

- 对于老版本的SQL Server，由于CDC支持不佳，建议使用时间戳或行版本号方法进行变更跟踪
- 确保源表和目标表具有相同的结构，或者正确配置列映射
- 对于大量数据的同步，建议适当调整批处理大小和同步间隔
- 同步状态保存在`sync_state.txt`文件中，请勿手动修改该文件

## 开发说明

项目结构：

- **Core**: 包含核心接口定义
- **Models**: 包含数据模型定义
- **Services**: 包含服务实现
- **Program.cs**: 应用程序入口点
- **dbsync.config.json**: 同步配置文件