using TreeDataStructures.Core;

namespace TreeDataStructures.Implementations.AVL;

public class AvlTree<TKey, TValue> : BinarySearchTreeBase<TKey, TValue, AvlNode<TKey, TValue>>
    where TKey : IComparable<TKey>
{
    protected override AvlNode<TKey, TValue> CreateNode(TKey key, TValue value)
        => new(key, value);
    
    protected override void OnNodeAdded(AvlNode<TKey, TValue> newNode)
    {
        BalanceAfterInsert(newNode);
    }
    
    protected override void OnNodeRemoved(AvlNode<TKey, TValue>? deletedNode, AvlNode<TKey, TValue>? replacement)
    {
        
        AvlNode<TKey, TValue>? startNode;
        
        if (replacement is null)
        {
            startNode = deletedNode?.Parent;
        }
        else
        {
            startNode = replacement.Parent;
        }
        
        startNode ??= Root;
        
        if (startNode is not null)
        {
            BalanceAfterDelete(startNode);
        }
    }
    
    private void BalanceAfterInsert(AvlNode<TKey, TValue> node)
    {
        AvlNode<TKey, TValue>? current = node;
        
        while (current is not null)
        {
            UpdateHeight(current);
            int balance = current.BalanceFactor;
            
            if (balance > 1)
            {
                if (current.Left!.BalanceFactor >= 0)
                {
                    RotateRightAndFix(current);
                }
                else
                {
                    RotateLeft(current.Left!);
                    UpdateHeight(current.Left!);
                    UpdateHeight(current);
                    RotateRightAndFix(current);
                }
                break;
            }
            else if (balance < -1)
            {
                if (current.Right!.BalanceFactor <= 0)
                {
                    RotateLeftAndFix(current);
                }
                else
                {
                    RotateRight(current.Right!);
                    UpdateHeight(current.Right!);
                    UpdateHeight(current);
                    RotateLeftAndFix(current);
                }
                break;
            }
            
            current = current.Parent;
        }
    }
    
    private void BalanceAfterDelete(AvlNode<TKey, TValue> node)
    {
        AvlNode<TKey, TValue>? current = node;
        
        while (current is not null)
        {
            UpdateHeight(current);
            int balance = current.BalanceFactor;
            
            if (balance > 1)
            {
                if (current.Left!.BalanceFactor >= 0)
                {
                    RotateRightAndFix(current);
                }
                else
                {
                    RotateLeft(current.Left!);
                    UpdateHeight(current.Left!);
                    UpdateHeight(current);
                    RotateRightAndFix(current);
                }
            }
            else if (balance < -1)
            {
                if (current.Right!.BalanceFactor <= 0)
                {
                    RotateLeftAndFix(current);
                }
                else
                {
                    RotateRight(current.Right!);
                    UpdateHeight(current.Right!);
                    UpdateHeight(current);
                    RotateLeftAndFix(current);
                }
            }
            
            current = current.Parent;
        }
    }
    
    private void UpdateHeight(AvlNode<TKey, TValue> node)
    {
        int leftHeight = node.Left?.Height ?? 0;
        int rightHeight = node.Right?.Height ?? 0;
        node.Height = 1 + Math.Max(leftHeight, rightHeight);
    }
    
    private void RotateRightAndFix(AvlNode<TKey, TValue> node)
    {
        RotateRight(node);
        UpdateHeight(node);
        if (node.Parent is AvlNode<TKey, TValue> newRoot)
        {
            UpdateHeight(newRoot);
        }
    }
    
    private void RotateLeftAndFix(AvlNode<TKey, TValue> node)
    {
        RotateLeft(node);
        UpdateHeight(node);
        if (node.Parent is AvlNode<TKey, TValue> newRoot)
        {
            UpdateHeight(newRoot);
        }
    }
}