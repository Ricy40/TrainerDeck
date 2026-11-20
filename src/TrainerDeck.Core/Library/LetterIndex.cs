namespace TrainerDeck.Core.Library;

public static class LetterIndex
{
    public const string Letters = "#ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public static HashSet<char> Available(IEnumerable<char> itemLetters) => new(itemLetters.Where(c => Letters.Contains(c)));

    public static char Step(char current, int delta, IReadOnlySet<char> available)
    {
        int i = Letters.IndexOf(current);
        if (i < 0)
        {
            i = 0;
        }

        int dir = Math.Sign(delta);
        int steps = Math.Abs(delta);
        int result = i;
        for (int j = i + dir; j >= 0 && j < Letters.Length && steps > 0; j += dir)
        {
            if (available.Contains(Letters[j]))
            {
                result = j;
                steps--;
            }
        }

        return Letters[result];
    }

    public static int FirstIndex(IReadOnlyList<char> itemLetters, char letter)
    {
        if (itemLetters.Count == 0)
        {
            return -1;
        }

        for (int i = 0; i < itemLetters.Count; i++)
        {
            if (itemLetters[i] == letter)
            {
                return i;
            }
        }

        int target = Letters.IndexOf(letter);
        for (int i = 0; i < itemLetters.Count; i++)
        {
            if (Letters.IndexOf(itemLetters[i]) > target)
            {
                return i;
            }
        }

        return itemLetters.Count - 1;
    }
}
