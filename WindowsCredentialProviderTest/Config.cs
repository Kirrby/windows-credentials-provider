using System;
using System.IO;
using System.Xml.Serialization;

namespace WindowsCredentialProviderTest
{
    [Serializable]
    public class CredentialProviderConfig
    {
        public int StatePollingIntervalMs { get; set; } = 5000; // 状态轮询间隔（毫秒）
        public string DefaultUsername { get; set; } = "2933277055@qq.com"; // 默认用户名
        public string DefaultPassword { get; set; } = "/1391453181620qq"; // 默认密码
        public string StateFilePath { get; set; } = @"C:\ProgramData\HeartMonitor\state.json"; // 状态文件路径
        public bool EnableLogging { get; set; } = true; // 是否启用日志
        public string LogFilePath { get; set; } = @"C:\ProgramData\HeartMonitor\debug_log.txt"; // 日志文件路径
        public bool ShowAvatar { get; set; } = false; // 是否显示头像
        public string AvatarPath { get; set; } = ""; // 头像路径
        public string CustomUsername { get; set; } = "User"; // 自定义用户名
        public string WelcomeMessage { get; set; } = "Welcome back"; // 欢迎语
        public string TimeFormat { get; set; } = "HH:mm:ss"; // 时间格式
        public string AvailableSymbol { get; set; } = "●"; // 可用状态符号
        public string UnavailableSymbol { get; set; } = "○"; // 不可用状态符号
        public string StatusFormat { get; set; } = "{0} {1}"; // 状态格式: 符号+时间

        public static CredentialProviderConfig LoadConfig()
        {
            string configDir = @"C:\ProgramData\HeartMonitor";
            string configPath = Path.Combine(configDir, "config.xml");

            // 确保目录存在
            if (!Directory.Exists(configDir))
            {
                try
                {
                    Directory.CreateDirectory(configDir);
                }
                catch
                {
                    // 如果无法创建目录，使用默认配置
                    return new CredentialProviderConfig();
                }
            }

            if (!File.Exists(configPath))
            {
                // 如果配置文件不存在，创建默认配置
                var defaultConfig = new CredentialProviderConfig();
                defaultConfig.SaveConfig(configPath);
                return defaultConfig;
            }

            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(CredentialProviderConfig));
                using (FileStream fileStream = new FileStream(configPath, FileMode.Open))
                {
                    return (CredentialProviderConfig)serializer.Deserialize(fileStream);
                }
            }
            catch
            {
                // 如果配置文件损坏，返回默认配置
                return new CredentialProviderConfig();
            }
        }

        public void SaveConfig(string path)
        {
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(CredentialProviderConfig));
                using (FileStream fileStream = new FileStream(path, FileMode.Create))
                {
                    serializer.Serialize(fileStream, this);
                }
            }
            catch
            {
                // 配置保存失败时忽略错误
            }
        }
    }
}