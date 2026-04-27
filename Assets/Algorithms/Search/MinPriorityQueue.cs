using System;
using System.Collections.Generic;

namespace Algorithms.Search
{
    /// <summary>
    /// =====================================================================
    ///  MinPriorityQueue&lt;T&gt; — 최소 우선순위 큐 (이진 힙 기반)
    /// =====================================================================
    ///
    /// ▶ 무엇인가
    ///   priority 가 가장 작은 원소를 O(log n) 에 꺼낼 수 있는 자료구조.
    ///   Dijkstra / A* 같은 최단 경로 알고리즘의 *심장* 부속품.
    ///
    /// ▶ 왜 직접 구현하나
    ///   .NET 6 부터 System.Collections.Generic.PriorityQueue&lt;TElement, TPriority&gt; 가
    ///   BCL 에 포함되었으나, Unity 의 .NET Standard 2.1 환경에서는 사용할 수 없다.
    ///   학습 목적상 의미체계가 동일한 이진 힙을 직접 구현해 알고리즘 안에서 어떻게
    ///   동작하는지 눈으로 확인할 수 있게 한다.
    ///
    /// ▶ 동작 원리 (Min-Heap, 배열 표현)
    ///   완전 이진 트리를 1차원 배열에 매핑한다 (인덱스 0 = 루트).
    ///   인덱스 i 의 자식은 2i+1, 2i+2. 부모는 (i-1)/2.
    ///   불변식 : *부모 우선순위 ≤ 자식 우선순위*. 항상 루트가 최소.
    ///
    ///   - Enqueue : 끝에 추가한 뒤 부모와 비교하며 위로 끌어 올린다 (Sift Up).
    ///   - Dequeue : 루트 반환 → 마지막 원소를 루트에 → 자식과 비교하며 아래로 내린다 (Sift Down).
    ///
    /// ▶ 시간 복잡도
    ///   - Enqueue : O(log n)
    ///   - Dequeue : O(log n)
    ///   - Peek    : O(1)
    ///   - Count   : O(1)
    ///
    /// ▶ Dijkstra 에서의 사용 패턴
    ///   "lazy deletion" — 같은 정점이 거리 갱신 때마다 PQ 에 여러 번 들어갈 수 있다.
    ///   꺼낼 때 "이미 처리됐는지" HashSet 으로 검사해 stale 항목을 무시.
    ///   힙 내부에서 직접 키를 갱신/삭제하는 것보다 단순하고 충분히 빠르다.
    /// </summary>
    public class MinPriorityQueue<T>
    {
        // 힙 = (원소, 우선순위) 쌍의 배열. 인덱스 0 이 루트(=최소 우선순위).
        // ValueTuple struct 라 GC 부담 없음.
        private readonly List<(T element, float priority)> _heap = new();

        /// <summary>현재 큐에 들어 있는 원소 개수.</summary>
        public int Count => _heap.Count;

        /// <summary>큐가 비어 있는지 여부.</summary>
        public bool IsEmpty => _heap.Count == 0;

        /// <summary>
        /// 원소를 우선순위와 함께 큐에 넣는다.
        /// </summary>
        public void Enqueue(T element, float priority)
        {
            // [1] 일단 끝에 추가.
            _heap.Add((element, priority));

            // [2] 부모와 비교하며 위로 끌어 올린다 → 힙 불변식 복구.
            SiftUp(_heap.Count - 1);
        }

        /// <summary>
        /// 우선순위가 가장 작은 원소를 꺼내 반환한다. 비어 있으면 예외.
        /// </summary>
        public T Dequeue()
        {
            if (_heap.Count == 0)
            {
                throw new InvalidOperationException("MinPriorityQueue 가 비어 있습니다.");
            }

            // [1] 루트 = 현재 최소.
            var min = _heap[0].element;

            // [2] 마지막 원소를 루트로 옮긴 뒤 마지막 자리 제거.
            int last = _heap.Count - 1;
            _heap[0] = _heap[last];
            _heap.RemoveAt(last);

            // [3] 새 루트를 자식과 비교하며 아래로 내린다 → 힙 불변식 복구.
            if (_heap.Count > 0)
            {
                SiftDown(0);
            }

            return min;
        }

        /// <summary>
        /// 우선순위가 가장 작은 원소를 꺼내지 않고 들여다본다.
        /// </summary>
        public T Peek()
        {
            if (_heap.Count == 0)
            {
                throw new InvalidOperationException("MinPriorityQueue 가 비어 있습니다.");
            }
            return _heap[0].element;
        }

        // ─────────────────────────────────────────────
        // 내부 — 힙 불변식 유지 (Sift Up / Sift Down)
        // ─────────────────────────────────────────────

        /// <summary>
        /// 인덱스 i 의 원소를 부모와 비교하며 위로 올린다.
        /// 부모가 더 크면 swap, i 를 부모로 갱신하고 반복.
        /// </summary>
        private void SiftUp(int i)
        {
            while (i > 0)
            {
                int parent = (i - 1) / 2;

                // 부모의 우선순위가 자기보다 작거나 같으면 위치 OK → 종료.
                if (_heap[i].priority >= _heap[parent].priority) break;

                // 부모와 swap.
                (_heap[i], _heap[parent]) = (_heap[parent], _heap[i]);
                i = parent;
            }
        }

        /// <summary>
        /// 인덱스 i 의 원소를 두 자식 중 더 작은 쪽과 비교하며 아래로 내린다.
        /// 두 자식보다 모두 작으면 위치 OK → 종료.
        /// </summary>
        private void SiftDown(int i)
        {
            int n = _heap.Count;
            while (true)
            {
                int left  = 2 * i + 1;
                int right = 2 * i + 2;
                int smallest = i;

                // 왼쪽 자식이 더 작으면 후보 갱신.
                if (left  < n && _heap[left].priority  < _heap[smallest].priority) smallest = left;
                // 오른쪽 자식이 더 작으면 후보 갱신.
                if (right < n && _heap[right].priority < _heap[smallest].priority) smallest = right;

                // 자기가 가장 작으면 위치 OK → 종료.
                if (smallest == i) break;

                // 더 작은 자식과 swap.
                (_heap[i], _heap[smallest]) = (_heap[smallest], _heap[i]);
                i = smallest;
            }
        }
    }
}
