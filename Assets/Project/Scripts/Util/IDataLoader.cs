using System;
using UnityEngine;

public interface IDataLoader
{
    bool TryLoad<T>(string key, out T data);
    bool TryLoad(string key, Type type, out object data);
}