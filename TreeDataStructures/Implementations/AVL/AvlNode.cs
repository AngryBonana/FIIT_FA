using TreeDataStructures.Core;

namespace TreeDataStructures.Implementations.AVL;

public class AvlNode<TKey, TValue>(TKey key, TValue value)
    : Node<TKey, TValue, AvlNode<TKey, TValue>>(key, value)
{
    public int Height { get; set; } = 1;
    
    public int BalanceFactor => (Left?.Height ?? 0) - (Right?.Height ?? 0);
}