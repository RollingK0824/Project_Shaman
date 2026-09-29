using System;
using System.IO;
using UnityEngine;

namespace ProjectShaman.AI.Core
{
    public static class AILog
    {
        public const string FACTORY = "Factory";
        public const string CORE = "Core";
        public const string DATA_TO_AI = "Data→AI";
        public const string FACTORY_TO_NET = "Factory→Net";
        public const string NET = "Net";
        public const string STUB = "Stub";
        public const string TIME_TO_AI = "Time→AI";
        public const string MANAGER = "Manager";
        public const string ROUTINE = "Routine";
        public const string PLACE = "Place";
        public const string WORK = "Work";
        public const string TOOL = "Tool";
        public const string EVENT = "Event";
        public const string GHOST = "Ghost";

        public static bool IsEnabled = true;
        public static bool IsFileLogEnabled = true;

        private static string _filePath;
        private static bool _isFileReady;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetFileLog()
        {
            _isFileReady = false;
        }

        private static void WriteFile(string level, string line)
        {
            if (!IsFileLogEnabled)
            {
                return;
            }

            try
            {
                if (!_isFileReady)
                {
                    string logsDirectory = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Logs");
                    Directory.CreateDirectory(logsDirectory);
                    _filePath = Path.Combine(logsDirectory, "AI_Session.log");
                    File.WriteAllText(_filePath, $"=== AI session {DateTime.Now:yyyy-MM-dd HH:mm:ss} ==={Environment.NewLine}");
                    _isFileReady = true;
                }

                File.AppendAllText(_filePath, $"{Time.time,8:F2} {level} {line}{Environment.NewLine}");
            }
            catch (Exception)
            {
                IsFileLogEnabled = false;
            }
        }

        public static void Log(string boundary, string message)
        {
            if (!IsEnabled)
            {
                return;
            }

            string line = $"[AI][{boundary}] {message}";
            Debug.Log(line);
            WriteFile("I", line);
        }

        public static void Log(string boundary, string villagerId, string message)
        {
            if (!IsEnabled)
            {
                return;
            }

            string line = $"[AI][{boundary}][{villagerId}] {message}";
            Debug.Log(line);
            WriteFile("I", line);
        }

        public static void Warn(string boundary, string message)
        {
            string line = $"[AI][{boundary}] {message}";
            Debug.LogWarning(line);
            WriteFile("W", line);
        }

        public static void Error(string boundary, string message)
        {
            string line = $"[AI][{boundary}] {message}";
            Debug.LogError(line);
            WriteFile("E", line);
        }
    }
}
