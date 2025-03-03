using System;
using System.Collections.Generic;
using UnityEngine;
using System.Collections;

namespace VoxxWeatherPlugin.Utils
{
    
    [Serializable]
    public struct EntitySnowData
    {
        public Vector3 w; // XZ position of the entity
        public Vector2 uv;
        public int textureIndex; // Index into the texture array
        public float snowThickness; // Snow thickness at the entity's position

        public EntitySnowData()
        {
            Reset();
        }

        public void Reset()
        {
            w = Vector3.zero;
            uv = Vector2.zero;
            textureIndex = -1; // -1 means not on valid ground
            snowThickness = 0; 
        }
    }

#if DEBUG

    [Serializable]
    public struct SerializableKeyValuePair<TKey, TValue>
    {
        public TKey Key;
        public TValue Value;

        public SerializableKeyValuePair(TKey key, TValue value)
        {
            Key = key;
            Value = value;
        }
    }

    [Serializable]
    public class SerializableDictionary<TKey, TValue> : ISerializationCallbackReceiver
    {
        [SerializeField]
        private List<SerializableKeyValuePair<TKey, TValue>> list = new List<SerializableKeyValuePair<TKey, TValue>>();

        private Dictionary<TKey, TValue> dictionary = new Dictionary<TKey, TValue>();

        public void OnBeforeSerialize()
        {
            list.Clear();
            foreach (var pair in dictionary)
            {
                list.Add(new SerializableKeyValuePair<TKey, TValue>(pair.Key, pair.Value));
            }
        }

        public void OnAfterDeserialize()
        {
            dictionary.Clear();
            foreach (var pair in list)
            {
                if (!dictionary.ContainsKey(pair.Key))
                {
                    dictionary.Add(pair.Key, pair.Value);
                }
                else
                {
                    Debug.LogWarning($"Duplicate key found during deserialization: {pair.Key}. Overwriting existing value.");
                    dictionary[pair.Key] = pair.Value;
                }
            }
        }

        // Mirroring Dictionary Functionality:

        public void Add(TKey key, TValue value) => dictionary.Add(key, value);

        public bool ContainsKey(TKey key) => dictionary.ContainsKey(key);

        public bool TryGetValue(TKey key, out TValue value) => dictionary.TryGetValue(key, out value);

        public bool Remove(TKey key) => dictionary.Remove(key);

        public void Clear() => dictionary.Clear();

        public int Count => dictionary.Count;

        public TValue this[TKey key]
        {
            get => dictionary[key];
            set => dictionary[key] = value;
        }

        public Dictionary<TKey, TValue>.KeyCollection Keys => dictionary.Keys;
        public Dictionary<TKey, TValue>.ValueCollection Values => dictionary.Values;

        // Additional helpful methods

        public bool ContainsValue(TValue value)
        {
            return dictionary.ContainsValue(value);
        }

        public void CopyTo(SerializableKeyValuePair<TKey, TValue>[] array, int index) => ((ICollection<SerializableKeyValuePair<TKey, TValue>>)dictionary).CopyTo(array, index);

        // IEnumerator IEnumerable.GetEnumerator() => dictionary.GetEnumerator();

        // public IEnumerator<SerializableKeyValuePair<TKey, TValue>> GetEnumerator() => dictionary.GetEnumerator();

        // public TValue GetValueOrDefault(TKey key, TValue defaultValue = default) => ContainsKey(key)? dictionary[key] : defaultValue;

    }

    public struct SnowTrackerDebugData
    {
        public bool isActive;
        public float particleSize;
        public float lifetimeMultiplier;
        public float footprintStrength;
        public int particleNumber;
    }

#endif
    
}