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
            var idToName = new Dictionary<int, string>();
            var idToChildren = new Dictionary<int, List<int>>();
            var used = new Dictionary<int, bool>();
            var hasFather = new Dictionary<int, bool>();
            
            foreach (var hierarchyString in hierarchy)
            {
                var id = ExtractValueFromHierarchy("id", hierarchyString);
                var name = ExtractStringValueFromHierarchy("name", hierarchyString);
                var fatherId = ExtractValueFromHierarchy("fatherId", hierarchyString);
                
                idToName[id] = name;
                used[id] = false;
                hasFather[id] = false;
                
                if (fatherId == 0) continue;
                
                hasFather[id] = true;
                
                if (idToChildren.ContainsKey(fatherId))
                {
                    idToChildren[fatherId].Add(id);
                }
                else
                {
                    var newList = new List<int> { id };
                    idToChildren.Add(fatherId, newList);
                }
                //writer.WriteLine($"{hierarchyString.Trim()}");
            }

            foreach (var kvp in idToName)
            {
                var id = kvp.Key;
                if (hasFather[id]) continue;
                WriteRecursively(ref writer, id, ref idToChildren, ref used, "", ref idToName);
            }
        }

        private static void WriteRecursively(ref StreamWriter writer, int id,
            ref Dictionary<int, List<int>> idToChildren,
            ref Dictionary<int, bool> used, string level, ref Dictionary<int, string> idToName)
        {
            if (used[id]) return;
            used[id] = true;
            writer.WriteLine(level + $"{idToName[id]}");
            level += "--";

            if (!idToChildren.ContainsKey(id)) return;
            
            foreach (var child in idToChildren[id])
            {
                WriteRecursively(ref writer, child, ref idToChildren, ref used, level, ref idToName);
            }
        }
        
        static int ExtractValueFromHierarchy(string key, string hierarchyString)
        {
            var match = Regex.Match(hierarchyString, $@"{key}: (\d+)");
            return match.Success ? int.Parse(match.Groups[1].Value) : 0;
        }

        static string ExtractStringValueFromHierarchy(string key, string hierarchyString)
        {
            var match = Regex.Match(hierarchyString, $@"{key}: (.+?),");
            return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
        }

        static List<string> GetScenePaths(string projectPath)
        {
            var scenePaths = new List<string>();
            var files = Directory.GetFiles(projectPath, "*.unity", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                scenePaths.Add(file);
            }

            return scenePaths;
        }

        private static List<string> GetSceneHierarchy(string scenePath)
        {
            var sceneHierarchy = new List<string>();
            var lines = File.ReadAllLines(scenePath);
            var inGameObject = false;
            var inTransform = false;
            var gotName = false;
            var gotId = false;
            string gameObjectName = null;
            string gameObjectId = null;
            
            foreach (var line in lines)
            {
                if (line.Trim() == "GameObject:")
                {
                    inGameObject = true;
                    gotName = false;
                    gotId = false;
                    continue;
                }
                
                if (!inGameObject)
                {
                    continue;
                }
                
                if (!inTransform)
                {
                    if (line.Trim().StartsWith("Transform:"))
                    {
                        inTransform = true;
                        continue;
                    }

                    if (!gotId && line.Trim().StartsWith("- component: {fileID: "))
                    {
                        gameObjectId = ExtractGameObjectId(line);
                        if (!string.IsNullOrEmpty(gameObjectId))
                        {
                            gotId = true;
                        }
                    }

                    if (!line.Trim().StartsWith("m_Name:") || gotName) continue;
                    
                    gameObjectName = ExtractGameObjectName(line);
                    if (!string.IsNullOrEmpty(gameObjectName))
                    {
                        gotName = true;
                    }
                    
                }
                else
                {
                    if (line.Trim().StartsWith("m_Father:"))
                    {
                        var fatherId = ExtractFatherId(line);
                        if (!string.IsNullOrEmpty(fatherId))
                        {
                            sceneHierarchy.Add("id: " + gameObjectId + ", name: " + gameObjectName + ", fatherId: " + fatherId);
                        }
                    }

                    if (line.Trim().StartsWith("--- !u"))
                    {
                        inGameObject = false;
                        inTransform = false;
                    }
                }
            }

            return sceneHierarchy;
        }

        private static string ExtractGameObjectName(string line)
        {
            return line.Substring(line.IndexOf("m_Name:") + 8).Trim();
        }
        
        private static string ExtractGameObjectId(string line)
        {
            var match = Regex.Match(line, @"fileID: (\d+)");
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
            return null;
        }
        
        private static string ExtractFatherId(string input)
        {
            var match = Regex.Match(input, @"fileID: (\d+)");

            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            return null;
        }
    }
}