using System.Diagnostics;
using Debug = UnityEngine.Debug;

// Debug.Log that only exists in the Editor and Development Builds. In release builds the compiler removes
// the whole call, including building its message string. Warnings and errors stay on Debug
public static class GameLog
{
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Log(object message)
    {
        Debug.Log(message);
    }
}
