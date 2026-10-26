namespace MiniInfos.UI.Windows;

/// <summary>
/// Which provider pages a card shows: one, or two side by side ("Visualização dividida"). The rules
/// live here, apart from the window, so they are tested on their own.
/// </summary>
/// <param name="Shown">The pages on screen, left to right, by their place in the provider list.</param>
/// <param name="Count">How many providers there are.</param>
public sealed record PageSelection(IReadOnlyList<int> Shown, int Count)
{
    public bool IsSplit => Shown.Count > 1;

    /// <summary>Whether turning can bring another page: some provider is not on screen.</summary>
    public bool CanTurn => Count > Shown.Count;

    public static PageSelection Single(int page, int count) => new([Wrap(page, count)], count);

    /// <summary>The selection a card saved, kept only where its pages still exist.</summary>
    /// <param name="page">The saved page, or -1 when there is none.</param>
    /// <param name="besidePage">The saved page shown beside it, or -1.</param>
    public static PageSelection Restore(int page, int besidePage, int count)
    {
        var single = Single(Math.Max(0, page), count);
        return besidePage >= 0 && besidePage < count ? single.SplitWith(besidePage) : single;
    }

    public bool Shows(int page) => Shown.Contains(page);

    /// <summary>
    /// Splits the card: the clicked page goes beside the first one. Splitting from the first page's
    /// own icon brings the page after it; when split already, the clicked page takes the second place.
    /// </summary>
    public PageSelection SplitWith(int clicked)
    {
        if (Count < 2) return this;

        var first = Shown[0];
        var beside = clicked == first ? Wrap(first + 1, Count) : Wrap(clicked, Count);
        return new PageSelection([first, beside], Count);
    }

    /// <summary>
    /// The arrows, the arrow keys and a swipe: a single page turns to the next one; a split card
    /// turns its last page to the next one not on screen, and stays put when every page is.
    /// </summary>
    public PageSelection Turn(int step)
    {
        if (!IsSplit) return Single(Shown[0] + step, Count);

        for (var next = Wrap(Shown[^1] + step, Count); next != Shown[^1]; next = Wrap(next + step, Count))
        {
            if (!Shows(next)) return new PageSelection([.. Shown.Take(Shown.Count - 1), next], Count);
        }

        return this;
    }

    public bool SameAs(PageSelection other) => Shown.SequenceEqual(other.Shown);

    private static int Wrap(int page, int count) => (page % count + count) % count;
}
