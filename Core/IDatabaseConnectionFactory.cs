using System.Data;

namespace DatabaseSyncApp.Core
{
    /// <summary>
    /// 数据库连接工厂接口，负责创建不同类型数据库的连接
    /// </summary>
    public interface IDatabaseConnectionFactory
    {
        /// <summary>
        /// 创建数据库连接
        /// </summary>
        /// <param name="connectionString">连接字符串</param>
        /// <param name="providerName">提供程序名称</param>
        /// <returns>数据库连接</returns>
        IDbConnection CreateConnection(string connectionString, string providerName);

        /// <summary>
        /// 创建命令对象
        /// </summary>
        /// <param name="connection">数据库连接</param>
        /// <param name="commandText">SQL命令文本</param>
        /// <param name="commandType">命令类型</param>
        /// <returns>命令对象</returns>
        IDbCommand CreateCommand(IDbConnection connection, string commandText, CommandType commandType = CommandType.Text);

        /// <summary>
        /// 创建参数对象
        /// </summary>
        /// <param name="command">命令对象</param>
        /// <param name="parameterName">参数名称</param>
        /// <param name="value">参数值</param>
        /// <returns>参数对象</returns>
        IDbDataParameter CreateParameter(IDbCommand command, string parameterName, object value);

        /// <summary>
        /// 获取数据库类型的参数前缀
        /// </summary>
        /// <param name="providerName">提供程序名称</param>
        /// <returns>参数前缀</returns>
        string GetParameterPrefix(string providerName);
    }
}