namespace WpfDesktop.Models;

public class AppSettings
{
    public string Language { get; set; } = "zh-CN";
    public string Theme { get; set; } = "Dark";
    public string PythonRoot { get; set; } = "";
    public int MaxLogLines { get; set; } = 5000;

    /// <summary>
    /// 数据存储根目录
    /// </summary>
    public string DataRoot { get; set; } = "%LOCALAPPDATA%\\ComfyShell";

    /// <summary>
    /// 应用程序名称
    /// </summary>
    public string AppName { get; set; } = "ComfyShell";
}
