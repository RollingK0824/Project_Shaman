using UnityEngine;

public interface IDataLoader
{
    bool TryLoad<T>(string key, out T data);
}