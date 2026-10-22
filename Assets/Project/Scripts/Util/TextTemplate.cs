using System;
using System.Text;

// Shared template filler for quest and dialogue text.
// {key} → value, {key:이} → value + the particle that fits its last syllable (이/가, 을/를, 은/는, 과/와, 아/야, 으로/로).
// Language-specific parts live in the template data and this particle helper only.
public static class TextTemplate
{
    public static string Fill(string template, Func<string, string> resolve)
    {
        if (string.IsNullOrEmpty(template) || template.IndexOf('{') < 0 || resolve == null)
        {
            return template ?? string.Empty;
        }

        StringBuilder builder = new StringBuilder(template.Length + 16);
        int i = 0;

        while (i < template.Length)
        {
            int open = template.IndexOf('{', i);
            int close = open >= 0 ? template.IndexOf('}', open + 1) : -1;

            if (open < 0 || close < 0)
            {
                builder.Append(template, i, template.Length - i);
                break;
            }

            builder.Append(template, i, open - i);
            string token = template.Substring(open + 1, close - open - 1);
            int colon = token.IndexOf(':');
            string key = colon >= 0 ? token.Substring(0, colon) : token;
            string value = resolve(key);

            if (value == null)
            {
                // Unknown key stays visible so missing data is easy to spot
                builder.Append('{').Append(token).Append('}');
            }
            else
            {
                builder.Append(value);
                if (colon >= 0)
                {
                    builder.Append(Particle(value, token.Substring(colon + 1)));
                }
            }

            i = close + 1;
        }

        return builder.ToString();
    }

    // particle is written in its after-consonant form (이, 을, 은, 과, 아, 으로)
    public static string Particle(string word, string particle)
    {
        bool hasFinal = HasFinalConsonant(word, out bool finalIsRieul);

        switch (particle)
        {
            case "이": case "가": return hasFinal ? "이" : "가";
            case "을": case "를": return hasFinal ? "을" : "를";
            case "은": case "는": return hasFinal ? "은" : "는";
            case "과": case "와": return hasFinal ? "과" : "와";
            case "아": case "야": return hasFinal ? "아" : "야";
            case "으로": case "로": return hasFinal && !finalIsRieul ? "으로" : "로";
            case "이다": return hasFinal ? "이다" : "다";
            default: return particle;
        }
    }

    public static bool HasFinalConsonant(string word, out bool finalIsRieul)
    {
        finalIsRieul = false;
        if (string.IsNullOrEmpty(word))
        {
            return false;
        }

        char last = word[word.Length - 1];
        if (last < '가' || last > '힣')
        {
            return false;
        }

        int final = (last - '가') % 28;
        finalIsRieul = final == 8;
        return final != 0;
    }
}
