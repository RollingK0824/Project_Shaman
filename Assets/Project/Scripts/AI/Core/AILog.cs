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

        public static bool IsEnabled = true;

        public static void Log(string boundary, string message)
        {
            if (!IsEnabled)
            {
                return;
            }

            Debug.Log($"[AI][{boundary}] {message}");
        }

        public static void Log(string boundary, string villagerId, string message)
        {
            if (!IsEnabled)
            {
                return;
            }

            Debug.Log($"[AI][{boundary}][{villagerId}] {message}");
        }

        public static void Warn(string boundary, string message)
        {
            Debug.LogWarning($"[AI][{boundary}] {message}");
        }

        public static void Error(string boundary, string message)
        {
            Debug.LogError($"[AI][{boundary}] {message}");
        }
    }
}
