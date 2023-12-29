using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace MySolution
{
    class UnityProjectAnalyzer
    {
        private static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("Usage: ./MySolution.exe <unity_project_path> <output_folder_path>");
                return;
            }

            string projectPath = args[0];
            string outputFolderPath = args[1];

            if (!Directory.Exists(projectPath))
            {
                Console.WriteLine("Error: Unity project path does not exist.");
                return;
            }
            
            if (!Directory.Exists(outputFolderPath))
            {
                Directory.CreateDirectory(outputFolderPath);
            }

            Dictionary<string, List<string>> projectInfo = new Dictionary<string, List<string>>();

            List<string> scenePaths = GetScenePaths(projectPath);

            foreach (string scenePath in scenePaths)
            {
                string sceneName = Path.GetFileNameWithoutExtension(scenePath);
                List<string> sceneHierarchy = GetSceneHierarchy(scenePath);
                projectInfo[sceneName] = sceneHierarchy;
            }
            
            WriteUnityDump(outputFolderPath, projectInfo);

            List<string> unusedScripts = FindUnusedScripts(projectPath);
            
            string unusedScriptsPath = Path.Combine(outputFolderPath, "UnusedScripts.txt");
            File.WriteAllText(unusedScriptsPath, "Relative Path,GUID" + Environment.NewLine);
            File.AppendAllLines(unusedScriptsPath, unusedScripts);
            
            Console.WriteLine($"Unity project analysis completed.");
        }
        
        static List<string> FindUnusedScripts(string projectPath)
        {
            List<string> scriptFiles = Directory.GetFiles(projectPath, "*.cs", SearchOption.AllDirectories).ToList();
            List<string> usedScripts = ExtractUsedScripts(projectPath);
            List<string> unusedScripts = scriptFiles.Except(usedScripts, StringComparer.OrdinalIgnoreCase).ToList();
            List<string> unusedScriptsWithGuid = new List<string>();
            
            foreach (string unusedScript in unusedScripts)
            {
                string metaFilePath = unusedScript + ".meta";
                string scriptGuid = ExtractScriptGuid(metaFilePath);

                unusedScriptsWithGuid.Add($"{GetRelativePath(unusedScript)},{scriptGuid}");
            }
            
            return unusedScriptsWithGuid;
        }
        
        static string GetRelativePath(string targetPath)
        {
            int assetsIndex = targetPath.IndexOf("Assets/", StringComparison.OrdinalIgnoreCase);

            if (assetsIndex != -1)
            {
                return targetPath.Substring(assetsIndex);
            }

            return targetPath;
        }
        
        static string ExtractScriptGuid(string metaFilePath)
        {
            if (File.Exists(metaFilePath))
            {
                string[] metaFileLines = File.ReadAllLines(metaFilePath);
                foreach (string line in metaFileLines)
                {
                    Match match = Regex.Match(line, @"^guid: ([a-fA-F0-9]+)$");
                    if (match.Success)
                    {
                        return match.Groups[1].Value;
                    }
                }
            }

            return null;
        }
        
        static List<string> ExtractUsedScripts(string projectPath)
        {
            List<string> usedScripts = new List<string>();
            
            List<string> scenePaths = Directory.GetFiles(projectPath, "*.unity", SearchOption.AllDirectories).ToList();
            foreach (string scenePath in scenePaths)
            {
                string[] lines = File.ReadAllLines(scenePath);

                foreach (string line in lines)
                {
                    Match match = Regex.Match(line, @"fileID: (\d+), guid: ([a-fA-F0-9]+), type: (\d+)");
                    if (match.Success)
                    {
                        string scriptPath = ResolveScriptPath(projectPath, match.Groups[2].Value);
                        if (!string.IsNullOrEmpty(scriptPath) && !usedScripts.Contains(scriptPath, StringComparer.OrdinalIgnoreCase))
                        {
                            usedScripts.Add(scriptPath);
                        }
                    }
                }
            }

            return usedScripts;
        }
        
        static string ResolveScriptPath(string projectPath, string scriptGuid)
        {
            string[] metaFiles = Directory.GetFiles(projectPath, "*.cs.meta", SearchOption.AllDirectories);

            foreach (string metaFile in metaFiles)
            {
                string[] metaFileLines = File.ReadAllLines(metaFile);
        
                foreach (string line in metaFileLines)
                {
                    Match match = Regex.Match(line, @"^guid: ([a-fA-F0-9]+)$");
            
                    if (match.Success && match.Groups[1].Value.Equals(scriptGuid, StringComparison.OrdinalIgnoreCase))
                    {
                        var scriptFilePath = Path.Combine(Path.GetDirectoryName(metaFile), Path.GetFileNameWithoutExtension(metaFile));
                        if (File.Exists(scriptFilePath))
                        {
                            return scriptFilePath;
                        }
                    }
                }
            }

            return null;
        }
        
        static void WriteUnityDump(string outputFolderPath, Dictionary<string, List<string>> projectInfo)
        {
            foreach (var kvp in projectInfo)
            {
                string outputPath = Path.Combine(outputFolderPath, $"{kvp.Key}.unity.dump");
                using (StreamWriter writer = new StreamWriter(outputPath))
                {
                    WriteHierarchy(writer, kvp.Value);
                    writer.WriteLine();
                }
            }
        }

        static void WriteHierarchy(StreamWriter writer, List<string> hierarchy)
        {
            foreach (var item in hierarchy)
            {
                writer.WriteLine($"{item.Trim()}");
            }
        }

        static List<string> GetScenePaths(string projectPath)
        {
            List<string> scenePaths = new List<string>();
            string[] files = Directory.GetFiles(projectPath, "*.unity", SearchOption.AllDirectories);
            foreach (string file in files)
            {
                scenePaths.Add(file);
            }

            return scenePaths;
        }

        static List<string> GetSceneHierarchy(string scenePath)
        {
            List<string> sceneHierarchy = new List<string>();
            string[] lines = File.ReadAllLines(scenePath);
            var inGameObject = false;

            foreach (var line in lines)
            {
                if (line.Trim() == "GameObject:")
                {
                    inGameObject = true;
                    continue;
                }

                if (inGameObject)
                {
                    if (line.Trim().StartsWith("m_Name:"))
                    {
                        var gameObjectName = ExtractGameObjectName(line);
                        if (!string.IsNullOrEmpty(gameObjectName))
                        {
                            sceneHierarchy.Add(gameObjectName);
                        }
                    }
                    
                    if (line.Trim().StartsWith("--- !u"))
                    {
                        inGameObject = false;
                    }
                }
            }

            return sceneHierarchy;
        }

        static string ExtractGameObjectName(string line)
        {
            return line.Substring(line.IndexOf("m_Name:") + 8).Trim();
        }
    }
}