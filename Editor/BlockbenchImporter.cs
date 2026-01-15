using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace MartinCalander.BlockbenchImporter
{
    /// <summary>
    /// Unity ScriptedImporter for Blockbench (.bbmodel) files.
    /// Automatically imports Blockbench models as Unity meshes.
    /// Communicates with Blockbench Unity Bridge plugin for automatic GLTF export.
    /// </summary>
    [ScriptedImporter(2, "bbmodel")]
    public class BlockbenchImporter : ScriptedImporter
    {
        [SerializeField] private bool autoExportGLTF = true;
        
        public override void OnImportAsset(AssetImportContext ctx)
        {
            string assetPath = ctx.assetPath;
            string fullPath = Path.GetFullPath(assetPath);
            
            // Create a placeholder object for the .bbmodel
            var placeholder = new GameObject("BlockbenchModel");
            var info = placeholder.AddComponent<BlockbenchModelInfo>();
            info.sourcePath = assetPath;
            info.importTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            
            ctx.AddObjectToAsset("main", placeholder);
            ctx.SetMainObject(placeholder);
            
            // Request GLTF export from Blockbench if enabled
            if (autoExportGLTF)
            {
                BlockbenchBridge.RequestExport(fullPath);
            }
        }
    }
    
    /// <summary>
    /// Component to store metadata about imported Blockbench models
    /// </summary>
    public class BlockbenchModelInfo : MonoBehaviour
    {
        public string sourcePath;
        public string importTime;
    }
    
    /// <summary>
    /// TCP socket communication with Blockbench Unity Bridge plugin
    /// </summary>
    public static class BlockbenchBridge
    {
        private const string HOST_PREF_KEY = "BlockbenchBridge_Host";
        private const string PORT_PREF_KEY = "BlockbenchBridge_Port";
        
        public static string Host
        {
            get => EditorPrefs.GetString(HOST_PREF_KEY, "127.0.0.1");
            set => EditorPrefs.SetString(HOST_PREF_KEY, value);
        }
        
        public static int Port
        {
            get => EditorPrefs.GetInt(PORT_PREF_KEY, 29405);
            set => EditorPrefs.SetInt(PORT_PREF_KEY, value);
        }
        
        private const int TIMEOUT_MS = 5000;
        
        /// <summary>
        /// Send a command to Blockbench via TCP and get the response
        /// </summary>
        private static async Task<string> SendCommand(string commandJson)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    // Connect with timeout
                    var connectTask = client.ConnectAsync(Host, Port);
                    if (await Task.WhenAny(connectTask, Task.Delay(TIMEOUT_MS)) != connectTask)
                    {
                        return null; // Connection timeout
                    }
                    
                    await connectTask; // Propagate any exceptions
                    
                    using (var stream = client.GetStream())
                    {
                        stream.ReadTimeout = TIMEOUT_MS;
                        stream.WriteTimeout = TIMEOUT_MS;
                        
                        // Send command (with newline delimiter)
                        var sendData = Encoding.UTF8.GetBytes(commandJson + "\n");
                        await stream.WriteAsync(sendData, 0, sendData.Length);
                        await stream.FlushAsync();
                        
                        // Read response
                        var buffer = new byte[8192];
                        var responseBuilder = new StringBuilder();
                        
                        // Read until we get a newline
                        while (true)
                        {
                            var readTask = stream.ReadAsync(buffer, 0, buffer.Length);
                            if (await Task.WhenAny(readTask, Task.Delay(TIMEOUT_MS)) != readTask)
                            {
                                break; // Read timeout
                            }
                            
                            var bytesRead = await readTask;
                            if (bytesRead == 0) break;
                            
                            var chunk = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                            responseBuilder.Append(chunk);
                            
                            if (chunk.Contains("\n")) break;
                        }
                        
                        return responseBuilder.ToString().Trim();
                    }
                }
            }
            catch (SocketException)
            {
                return null; // Connection refused - Blockbench not running
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BlockbenchBridge] Error: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Request Blockbench to export a specific bbmodel file as GLTF
        /// </summary>
        public static async void RequestExport(string bbmodelPath)
        {
            var commandId = Guid.NewGuid().ToString();
            var commandJson = $"{{\"action\":\"export\",\"bbmodelPath\":\"{bbmodelPath.Replace("\\", "\\\\")}\",\"id\":\"{commandId}\"}}";
            
            var response = await SendCommand(commandJson);
            
            if (response != null)
            {
                if (response.Contains("\"success\":true"))
                {
                    Debug.Log($"[BlockbenchBridge] GLTF export completed for: {bbmodelPath}");
                    
                    // Refresh asset database to pick up the new GLTF file
                    EditorApplication.delayCall += () =>
                    {
                        EditorApplication.delayCall += () =>
                        {
                            AssetDatabase.Refresh();
                        };
                    };
                }
                else
                {
                    Debug.LogWarning($"[BlockbenchBridge] Export failed: {response}");
                }
            }
            // Silent failure if Blockbench not running
        }
        
        [MenuItem("Tools/Blockbench/Force Export GLTF %#b")] // Ctrl+Shift+B
        public static async void ForceExportGLTF()
        {
            var commandId = Guid.NewGuid().ToString();
            var commandJson = $"{{\"action\":\"force-export\",\"id\":\"{commandId}\"}}";
            
            var response = await SendCommand(commandJson);
            
            if (response != null)
            {
                if (response.Contains("\"success\":true"))
                {
                    Debug.Log("[BlockbenchBridge] Force export successful");
                    EditorApplication.delayCall += () => AssetDatabase.Refresh();
                }
                else
                {
                    Debug.LogWarning($"[BlockbenchBridge] Force export failed: {response}");
                }
            }
            else
            {
                Debug.LogError("[BlockbenchBridge] No response from Blockbench. Is it running with the Unity Bridge plugin?");
            }
        }
        
        [MenuItem("Tools/Blockbench/Check Connection")]
        public static async void CheckConnection()
        {
            var commandId = Guid.NewGuid().ToString();
            var commandJson = $"{{\"action\":\"ping\",\"id\":\"{commandId}\"}}";
            
            var response = await SendCommand(commandJson);
            
            if (response != null)
            {
                Debug.Log($"[BlockbenchBridge] Connected! Response: {response}");
            }
            else
            {
                Debug.LogError($"[BlockbenchBridge] No response from Blockbench at {Host}:{Port}. Is it running with the Unity Bridge plugin?");
            }
        }
        
        [MenuItem("Tools/Blockbench/Settings")]
        public static void OpenSettings()
        {
            BlockbenchBridgeSettingsWindow.ShowWindow();
        }
    }
    
    /// <summary>
    /// Settings window for Blockbench Bridge configuration
    /// </summary>
    public class BlockbenchBridgeSettingsWindow : EditorWindow
    {
        private string host;
        private int port;
        
        public static void ShowWindow()
        {
            var window = GetWindow<BlockbenchBridgeSettingsWindow>("Blockbench Bridge Settings");
            window.minSize = new Vector2(300, 150);
            window.Show();
        }
        
        private void OnEnable()
        {
            host = BlockbenchBridge.Host;
            port = BlockbenchBridge.Port;
        }
        
        private void OnGUI()
        {
            EditorGUILayout.LabelField("Blockbench Unity Bridge Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            host = EditorGUILayout.TextField("Host", host);
            port = EditorGUILayout.IntField("Port", port);
            
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Make sure Blockbench is running with the Unity Bridge plugin.\n\n" +
                "Shortcuts:\n" +
                "• Ctrl+Shift+B - Force export current model as GLTF",
                MessageType.Info
            );
            
            EditorGUILayout.Space();
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Save"))
            {
                BlockbenchBridge.Host = host;
                BlockbenchBridge.Port = port;
                Debug.Log("[BlockbenchBridge] Settings saved.");
            }
            
            if (GUILayout.Button("Test Connection"))
            {
                BlockbenchBridge.CheckConnection();
            }
            
            EditorGUILayout.EndHorizontal();
            
            if (GUILayout.Button("Force Export GLTF (Ctrl+Shift+B)"))
            {
                BlockbenchBridge.ForceExportGLTF();
            }
        }
    }
}
