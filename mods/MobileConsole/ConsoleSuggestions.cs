using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Runs;

namespace MobileConsole;

internal static class ConsoleSuggestions
{
    internal readonly record struct Option(string Label, string Text);

    public static IEnumerable<Option> Get(DevConsole console, string text)
    {
        const string removeCard = "remove_card";
        var parts = text.Split(' ', 2);
        var command = parts[0].Replace('-', '_');
        if (command.Equals(removeCard, StringComparison.OrdinalIgnoreCase))
        {
            return DeckCards(parts.Length == 1 ? "" : parts[1].Trim());
        }

        var normalized = parts.Length == 1 ? command : $"{command} {parts[1]}";
        var result = console.GetCompletionResults(normalized);
        return result.Candidates
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(candidate => new Option(candidate, $"{result.CommandPrefix}{candidate} "))
            .Where(option => !option.Text.Equals(normalized, StringComparison.OrdinalIgnoreCase))
            .Take(60);
    }

    private static IEnumerable<Option> DeckCards(string filter)
    {
        var run = RunManager.Instance;
        if (!run.IsInProgress || LocalContext.GetMe(run.DebugOnlyGetState()) is not { } player)
            return Array.Empty<Option>();

        var idFilter = filter.Replace('-', '_').Replace(' ', '_');
        return PileType.Deck.GetPile(player).Cards
            .DistinctBy(card => card.Id.Entry)
            .Where(card => card.Title.Contains(filter, StringComparison.CurrentCultureIgnoreCase) ||
                card.Id.Entry.Contains(idFilter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(card => card.Title, StringComparer.CurrentCultureIgnoreCase)
            .Select(card => new Option(card.Title, $"remove_card {card.Id.Entry} deck"));
    }
}
