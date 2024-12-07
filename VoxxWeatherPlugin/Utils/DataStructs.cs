using System;
using System.Collections.Generic;
using UnityEngine;
using System.Collections;

namespace VoxxWeatherPlugin.Utils
{
    internal struct EdgePair : IEquatable<EdgePair>
    {
        public readonly int P1;
        public readonly int P2;

        public EdgePair(int p1, int p2)
        {
            P1 = Mathf.Min(p1, p2);
            P2 = Mathf.Max(p1, p2);
        }

        public bool Equals(EdgePair other)
        {
            return (P1 == other.P1 && P2 == other.P2);
        }

        public override bool Equals(object obj)
        {
            return obj is EdgePair other && Equals(other);
        }


        public override int GetHashCode()
        {
            return (P1, P2).GetHashCode(); // Use ValueTuple's GetHashCode for efficiency
        }
    }

    internal struct EntitySnowData
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

    internal class QuadTree
    {
        public Bounds bounds;
        public QuadTree[]? children;
        public bool isLeaf = true;

        public QuadTree(Bounds bounds)
        {
            this.bounds = bounds;
        }

        public bool Contains(Vector3 point)
        {
            return bounds.Contains(point);
        }

        public void Subdivide()
        {
            isLeaf = false;
            children = new QuadTree[4];
            float quarterX = bounds.size.x * 0.25f;
            float quarterZ = bounds.size.z * 0.25f;

            // Create four child quadrants
            for (int i = 0; i < 4; i++)
            {
                Vector3 center = bounds.center + new Vector3(
                    ((i % 2) * 2 - 1) * quarterX,
                    0,
                    ((i / 2) * 2 - 1) * quarterZ
                );

                Bounds childBounds = new Bounds(
                    center,
                    new Vector3(bounds.size.x * 0.5f, bounds.size.y, bounds.size.z * 0.5f)
                );

                children[i] = new QuadTree(childBounds);
            }
        }

        public void Subdivide(Bounds levelBounds, Vector2 stepSize, float minCellStep, float maxCellStep, float falloffSpeed, float maxDistance)
        {
            // Get the point relative to the center
            Vector3 closestPoint = levelBounds.ClosestPoint(bounds.center) - levelBounds.center;
            // Size of the side where the closest point is
            float sideSize = Mathf.Max(Mathf.Abs(closestPoint.x), Mathf.Abs(closestPoint.z)); 
            // Size of the side where the quad is
            Vector3 distanceToCenter =  bounds.center - levelBounds.center;
            float distance = Mathf.Max(Mathf.Abs(distanceToCenter.x), Mathf.Abs(distanceToCenter.z));

            float actualCellStep = minCellStep;
            if (distance > sideSize)
            {
                actualCellStep = Mathf.Lerp(minCellStep, maxCellStep, falloffSpeed*(distance - sideSize) / maxDistance);
            }
            actualCellStep = Mathf.Max(actualCellStep, 1);

            // If the quad is too large for desired step size, subdivide
            if (bounds.size.x > actualCellStep * stepSize.x || bounds.size.z > actualCellStep * stepSize.y)
            {
                Subdivide();
                foreach (var child in children!)
                {
                    child.Subdivide(levelBounds, stepSize, minCellStep, maxCellStep, falloffSpeed, maxDistance);
                }
            }
        }

        public void GetLeafNodes(List<QuadTree> leafNodes)
        {
            if (isLeaf)
            {
                leafNodes.Add(this);
            }
            else
            {
                if (children == null)
                    return;
                    
                foreach (var child in children)
                {
                    child.GetLeafNodes(leafNodes);
                }
            }
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

        public TValue GetValueOrDefault(TKey key, TValue defaultValue = default(TValue)) =>ContainsKey(key)? dictionary[key] : defaultValue;

    }

    [Serializable]
    public struct SnowTrackerData
    {
        public bool isActive;
        public float particleSize;
        public float lifetimeMultiplier;
        public float footprintStrength;
        public int particleNumber;
    }

#endif
    
}