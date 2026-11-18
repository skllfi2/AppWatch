using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AppWatch.Core.Domain;

public static class ObservableCollectionExtensions
{
    /// <summary>
    /// Synchronizes target collection with source items by performing minimal differential updates
    /// (inserting/removing/reordering only what changed), avoiding disruptive group.Clear() UI churn.
    /// </summary>
    public static void SyncWith<T>(this ObservableCollection<T> target, IEnumerable<T> source, IEqualityComparer<T>? comparer = null)
    {
        comparer ??= EqualityComparer<T>.Default;
        var desired = source.ToList();

        // 1. Remove items from target that are no longer in desired
        for (int i = target.Count - 1; i >= 0; i--)
        {
            if (!desired.Contains(target[i], comparer))
            {
                target.RemoveAt(i);
            }
        }

        // 2. Insert or move items to match desired order
        for (int i = 0; i < desired.Count; i++)
        {
            var item = desired[i];
            if (i >= target.Count)
            {
                target.Add(item);
            }
            else if (!comparer.Equals(target[i], item))
            {
                int existingIndex = -1;
                for (int j = i + 1; j < target.Count; j++)
                {
                    if (comparer.Equals(target[j], item))
                    {
                        existingIndex = j;
                        break;
                    }
                }

                if (existingIndex >= 0)
                {
                    target.Move(existingIndex, i);
                }
                else
                {
                    target.Insert(i, item);
                }
            }
        }
    }
}
