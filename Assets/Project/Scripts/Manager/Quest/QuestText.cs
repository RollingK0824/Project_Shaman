using System;
using System.Collections.Generic;
using System.Text;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Interfaces;
using ProjectShaman.Map;

// Fills quest text placeholders from ids, so only ids cross the network and every peer formats locally.
// {giver} {target} {context} → display name of that id, {area} → area the context id sits in.
// Quest targets and villagers are looked up directly; other name owners (items, tools, areas) register resolvers.
// Unknown ids fall back to the raw id.
public static class QuestText
{
    private static readonly List<Func<string, string>> _nameResolvers = new List<Func<string, string>>();
    private static readonly List<Func<string, string>> _areaResolvers = new List<Func<string, string>>();

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLoad()
    {
        _nameResolvers.Clear();
        _areaResolvers.Clear();
    }

    public static void AddNameResolver(Func<string, string> resolver)
    {
        if (resolver != null && !_nameResolvers.Contains(resolver))
        {
            _nameResolvers.Add(resolver);
        }
    }

    public static void RemoveNameResolver(Func<string, string> resolver)
    {
        _nameResolvers.Remove(resolver);
    }

    public static void AddAreaResolver(Func<string, string> resolver)
    {
        if (resolver != null && !_areaResolvers.Contains(resolver))
        {
            _areaResolvers.Add(resolver);
        }
    }

    public static void RemoveAreaResolver(Func<string, string> resolver)
    {
        _areaResolvers.Remove(resolver);
    }

    public static string NameOf(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return string.Empty;
        }

        if (QuestTargets.TryGet(id, out IQuestTarget target))
        {
            return target.DisplayName;
        }

        VillagerRegistry registry = AIManager.Existing != null ? AIManager.Existing.Registry : null;
        if (registry != null && registry.TryGet(id, out IVillagerView villager) && !string.IsNullOrEmpty(villager.DisplayName))
        {
            return villager.DisplayName;
        }

        return Resolve(_nameResolvers, id) ?? id;
    }

    // Quest target → area around its position, area id → that area's name, otherwise registered resolvers
    public static string AreaOf(string contextId)
    {
        if (QuestTargets.TryGet(contextId, out IQuestTarget target))
        {
            string around = MapAreaRegistry.Describe(target.Position);
            if (!string.IsNullOrEmpty(around))
            {
                return around;
            }
        }

        return MapAreaRegistry.NameOf(contextId) ?? Resolve(_areaResolvers, contextId) ?? "어딘가";
    }

    public static string Format(string template, IQuestView quest)
    {
        if (string.IsNullOrEmpty(template) || template.IndexOf('{') < 0 || quest == null)
        {
            return template ?? string.Empty;
        }

        StringBuilder builder = new StringBuilder(template);
        Replace(builder, "{giver}", () => NameOf(quest.GiverId));
        Replace(builder, "{target}", () => NameOf(quest.TargetId));
        Replace(builder, "{context}", () => NameOf(quest.ContextId));
        Replace(builder, "{area}", () => AreaOf(quest.ContextId));
        return builder.ToString();
    }

    private static void Replace(StringBuilder builder, string token, Func<string> value)
    {
        if (builder.ToString().Contains(token))
        {
            builder.Replace(token, value());
        }
    }

    private static string Resolve(List<Func<string, string>> resolvers, string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        foreach (Func<string, string> resolver in resolvers)
        {
            try
            {
                string result = resolver(id);
                if (!string.IsNullOrEmpty(result))
                {
                    return result;
                }
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
            }
        }

        return null;
    }
}
