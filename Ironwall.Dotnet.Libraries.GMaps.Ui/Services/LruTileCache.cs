using System.Collections.Generic;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services;

/// <summary>
/// LRU(Least Recently Used) 타일 이미지 캐시
/// 오버레이 맵 타일을 메모리에 캐시하여 SQLite/디스크 I/O를 줄인다.
/// </summary>
public class LruTileCache
{
    private readonly int _maxSize;
    private readonly Dictionary<string, LinkedListNode<CacheEntry>> _map;
    private readonly LinkedList<CacheEntry> _list;
    private readonly object _syncRoot = new();

    private record CacheEntry(string Key, ImageSource Image);

    public LruTileCache(int maxSize = 500)
    {
        _maxSize = maxSize;
        _map = new Dictionary<string, LinkedListNode<CacheEntry>>(maxSize);
        _list = new LinkedList<CacheEntry>();
    }

    public int Count { get { lock (_syncRoot) return _map.Count; } }
    public int MaxSize => _maxSize;

    /// <summary>캐시에서 타일 조회 (히트 시 MRU로 이동)</summary>
    public ImageSource? Get(string key)
    {
        lock (_syncRoot)
        {
            if (_map.TryGetValue(key, out var node))
            {
                _list.Remove(node);
                _list.AddFirst(node);
                return node.Value.Image;
            }
            return null;
        }
    }

    /// <summary>캐시에 타일 저장 (최대 크기 초과 시 LRU 제거)</summary>
    public void Put(string key, ImageSource image)
    {
        lock (_syncRoot)
        {
            if (_map.TryGetValue(key, out var existing))
            {
                _list.Remove(existing);
                _map.Remove(key);
            }

            var entry = new CacheEntry(key, image);
            var node = _list.AddFirst(entry);
            _map[key] = node;

            while (_map.Count > _maxSize)
            {
                var last = _list.Last;
                if (last == null) break;
                _map.Remove(last.Value.Key);
                _list.RemoveLast();
            }
        }
    }

    /// <summary>특정 CustomMap의 캐시 제거 (키 prefix 매칭)</summary>
    public void Invalidate(int customMapId)
    {
        lock (_syncRoot)
        {
            var prefix = $"{customMapId}_";
            var toRemove = new System.Collections.Generic.List<string>();

            foreach (var key in _map.Keys)
            {
                if (key.StartsWith(prefix))
                    toRemove.Add(key);
            }

            foreach (var key in toRemove)
            {
                if (_map.TryGetValue(key, out var node))
                {
                    _list.Remove(node);
                    _map.Remove(key);
                }
            }
        }
    }

    /// <summary>전체 캐시 초기화</summary>
    public void Clear()
    {
        lock (_syncRoot)
        {
            _map.Clear();
            _list.Clear();
        }
    }
}
