using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // Each grid owns its scratch space. Searches are synchronous on Unity's main
    // thread; returned routes never alias this memory or another actor's route.
    internal sealed class PrototypePathWorkspace
    {
        public readonly float[] Cost;
        public readonly int[] Prior;
        public readonly bool[] Closed;
        public readonly List<Vector2> Raw = new List<Vector2>(128);
        private readonly List<Node> heap = new List<Node>(256);
        private int sequence;
        public int Count => heap.Count;

        private readonly struct Node
        {
            public readonly int Index, Order;
            public readonly float Score;
            public Node(int index, float score, int order)
            { Index = index; Score = score; Order = order; }
        }

        public PrototypePathWorkspace(int count)
        {
            Cost = new float[count]; Prior = new int[count]; Closed = new bool[count];
        }

        public void Reset()
        {
            for (int i = 0; i < Cost.Length; i++)
            { Cost[i] = float.PositiveInfinity; Prior[i] = -1; }
            Array.Clear(Closed, 0, Closed.Length);
            heap.Clear(); Raw.Clear(); sequence = 0;
        }

        // Original list search chose the earliest inserted node on equal scores.
        // Keep that tie break so paths and AI movement are unchanged.
        private static bool Before(Node a, Node b) =>
            a.Score < b.Score || a.Score == b.Score && a.Order < b.Order;

        public void Push(int index, float score)
        {
            var node = new Node(index, score, sequence++);
            int at = heap.Count;
            heap.Add(node);
            while (at > 0)
            {
                int parent = (at - 1) / 2;
                if (!Before(node, heap[parent])) break;
                heap[at] = heap[parent]; at = parent;
            }
            heap[at] = node;
        }

        public int Pop()
        {
            int result = heap[0].Index;
            int last = heap.Count - 1;
            Node node = heap[last];
            heap.RemoveAt(last);
            if (last == 0) return result;
            int at = 0;
            while (at * 2 + 1 < last)
            {
                int child = at * 2 + 1;
                if (child + 1 < last && Before(heap[child + 1], heap[child])) child++;
                if (!Before(heap[child], node)) break;
                heap[at] = heap[child]; at = child;
            }
            heap[at] = node;
            return result;
        }
    }
}
