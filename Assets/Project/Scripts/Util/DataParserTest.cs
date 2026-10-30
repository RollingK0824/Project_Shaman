using UnityEngine;
public class DataParserTest : MonoBehaviour
{
    private void Start()
    {
        IDataLoader loader = new JsonResourcesDataLoader();
        string resourcePath = "Data/AIData_Sample";
        if (loader.TryLoad<AIDataContainer>(resourcePath, out var data))
        {
            GameLog.Log($"<color=green>[SUCCESS]</color> Version: {data.version}");
            GameLog.Log($"Loaded Names: {data.names.Count}");
            GameLog.Log($"Loaded Jobs: {data.jobs.Count}");
            GameLog.Log($"Loaded Routines: {data.routines.Count}");
            
            if (data.routines.Count > 0)
            {
                var routine = data.routines[0];
                GameLog.Log($"First Routine: ID='{routine.RoutineId}', Display='{routine.DisplayName}', Place='{routine.PlaceId}', Category='{routine.Category}'");
            }
        }
        else
        {
            Debug.LogError("<color=red>[FAILED]</color> Could not load or parse AI Data!");
        }
    }
}