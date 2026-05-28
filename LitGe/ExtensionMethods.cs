namespace LitGe;

public static class ExtensionMethods
{
    public static void ForEach<T>(this IEnumerable<T> enumeration, Action<T> action)
    {
        foreach (T item in enumeration)
        {
            action(item);
        }
    }

    public static IEnumerable<T> FlattenHierarchy<T>(
        this T node,
        Func<T, IEnumerable<T>> getChildEnumerator
    )
    {
        yield return node;
        if (getChildEnumerator(node) != null)
        {
            foreach (T child in getChildEnumerator(node))
            {
                foreach (T childOrDescendant in child.FlattenHierarchy(getChildEnumerator))
                {
                    yield return childOrDescendant;
                }
            }
        }
    }

    public static IEnumerable<T> Flatten<T>(this IEnumerable<T> e, Func<T, IEnumerable<T>> f)
    {
        return e.SelectMany(c => f(c).Flatten(f)).Concat(e);
    }

    public static bool IsNullOrValue(int? value, int valueToCheck)
    {
        return (value ?? valueToCheck) == valueToCheck;
    }
}
