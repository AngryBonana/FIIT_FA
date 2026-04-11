using System.Diagnostics.CodeAnalysis;
using TreeDataStructures.Core;

namespace TreeDataStructures.Implementations.Treap;

public class Treap<TKey, TValue> : BinarySearchTreeBase<TKey, TValue, TreapNode<TKey, TValue>>
    where TKey : IComparable<TKey>
{
    /// <summary>
    /// Разрезает дерево с корнем <paramref name="root"/> на два поддерева:
    /// Left: все ключи <= <paramref name="key"/>
    /// Right: все ключи > <paramref name="key"/>
    /// </summary>
    protected virtual (TreapNode<TKey, TValue>? Left, TreapNode<TKey, TValue>? Right) Split(TreapNode<TKey, TValue>? root, TKey key)
    {
        if (root is null)
            return (null, null);
        
        if (Comparer.Compare(root.Key, key) <= 0)
        {
            var (leftSub, rightSub) = Split(root.Right, key);
            root.Right = leftSub;
            if (leftSub is not null) leftSub.Parent = root;
            
            if (rightSub is not null) rightSub.Parent = null;
            
            return (root, rightSub);
        }
        else
        {
            var (leftSub, rightSub) = Split(root.Left, key);
            root.Left = rightSub;
            if (rightSub is not null) rightSub.Parent = root;
            
            if (leftSub is not null) leftSub.Parent = null;
            
            return (leftSub, root);
        }
    }

    private (TreapNode<TKey, TValue>? Left, TreapNode<TKey, TValue>? Right) SplitStrict(TreapNode<TKey, TValue>? root, TKey key)
    {
        if (root is null)
            return (null, null);
        
        if (Comparer.Compare(root.Key, key) < 0)
        {
            var (leftSub, rightSub) = SplitStrict(root.Right, key);
            root.Right = leftSub;
            if (leftSub is not null) leftSub.Parent = root;
            
            if (rightSub is not null) rightSub.Parent = null;
            
            return (root, rightSub);
        }
        else
        {
            var (leftSub, rightSub) = SplitStrict(root.Left, key);
            root.Left = rightSub;
            if (rightSub is not null) rightSub.Parent = root;
            
            if (leftSub is not null) leftSub.Parent = null;
            
            return (leftSub, root);
        }
    }

    /// <summary>
    /// Сливает два дерева в одно.
    /// Важное условие: все ключи в <paramref name="left"/> должны быть меньше ключей в <paramref name="right"/>.
    /// Слияние происходит на основе Priority (куча).
    /// </summary>
    protected virtual TreapNode<TKey, TValue>? Merge(TreapNode<TKey, TValue>? left, TreapNode<TKey, TValue>? right)
    {
        if (left is null) return right;
        if (right is null) return left;
        
        if (left.Priority > right.Priority)
        {
            left.Right = Merge(left.Right, right);
            if (left.Right is not null) left.Right.Parent = left;
            return left;
        }
        else
        {
            right.Left = Merge(left, right.Left);
            if (right.Left is not null) right.Left.Parent = right;
            return right;
        }
    }
    

    public override void Add(TKey key, TValue value)
    {
        var existing = FindNode(key);
        if (existing is not null)
        {
            existing.Value = value;
            return;
        }
        
        var (left, right) = Split(Root, key);
        var newNode = CreateNode(key, value);
        Root = Merge(Merge(left, newNode), right);
        Count++;
    }

    public override bool Remove(TKey key)
    {
        if (!ContainsKey(key)) return false;
        
        var (lessOrEqual, greater) = Split(Root, key);
        
        var (less, equal) = SplitStrict(lessOrEqual, key);
        
        Root = Merge(less, greater);
        
        Count--;
        return true;
    }

    protected override TreapNode<TKey, TValue> CreateNode(TKey key, TValue value) => new(key, value);

    protected override void OnNodeAdded(TreapNode<TKey, TValue> newNode) {}
    protected override void OnNodeRemoved(TreapNode<TKey, TValue>? parent, TreapNode<TKey, TValue>? child) {}

}