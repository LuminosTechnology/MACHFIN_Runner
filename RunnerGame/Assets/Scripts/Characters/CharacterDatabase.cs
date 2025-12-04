using UnityEngine;
using UnityEngine.AddressableAssets;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

/// <summary>
/// This allows us to store a database of all characters currently in the bundles, indexed by name.
/// </summary>
public class CharacterDatabase
{
    static protected Dictionary<string, Character> m_CharactersDict;

    static public Dictionary<string, Character> dictionary { get { return m_CharactersDict; } }

    static protected bool m_Loaded = false;
    static public bool loaded { get { return m_Loaded; } }

    static public Character GetCharacter(string type)
    {
        foreach (var o in m_CharactersDict.Keys)
        {
            Debug.Log(o);
        }
        Character c;
        var hasChar = m_CharactersDict.ContainsKey(type);
        // Debug.Log($"{type} {hasChar}");

        if (m_CharactersDict == null || !m_CharactersDict.TryGetValue(type, out c))
            return null;

        return c;
    }

    static public IEnumerator LoadDatabase()
    {
        if (m_CharactersDict == null)
        {
            m_CharactersDict = new Dictionary<string, Character>();

            yield return Addressables.LoadAssetsAsync<GameObject>("characters", op =>
            {
                Character c = op.GetComponent<Character>();
                if (c != null)
                {
                    Debug.Log($"Adding {c.characterName} to database");
                    m_CharactersDict.Add(c.characterName, c);
                }
            });

            m_Loaded = true;
        }
    }
}