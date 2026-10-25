using UnityEngine;
using System.IO;
using UnityEditor.Callbacks;
using UnityEditor;

// Steam으로 실행하지 않은 빌드도 SteamAPI.Init()이 되도록 설정
// 프로젝트 루트의 steam_appid.txt로 exe 옆에 복사한다.
public static class SteamAppIdPostBuild
{
    private const string FILE_NAME = "steam_appid.txt";

    [PostProcessBuild]
    private static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.StandaloneWindows && target != BuildTarget.StandaloneWindows64)
        {
            return;
        }

        string source = Path.Combine(Directory.GetCurrentDirectory(), FILE_NAME);

        if (!File.Exists(source))
        {
            Debug.LogWarning($"[SteamAppIdPostBuild] 프로젝트 루트에 {FILE_NAME}이 없습니다.");
            return;
        }

        string destination = Path.Combine(Path.GetDirectoryName(pathToBuiltProject), FILE_NAME);
        File.Copy(source, destination, true);

        Debug.Log($"[SteamAppIdPostBuild] {FILE_NAME} 복사: {destination}");
    }

}
